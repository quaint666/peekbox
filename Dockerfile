# Peekbox: one image from this file.
#
#   docker build -t peekbox .                                       the site, and the migration bundle at /app/efbundle
#   docker run --entrypoint /app/efbundle -e ConnectionStrings__Default=... peekbox
#                                                                   the bundle: creates and updates the tables, and fills
#                                                                   EMPTY tables with the sample content (run it before
#                                                                   the site starts; a second run changes nothing)
#
# Stage "assets" compiles the browser TypeScript (tsc fails the build on a type error). Stage "publish" publishes the site;
# stage "bundle" builds the migration bundle with the dotnet-ef version pinned in dotnet-tools.json. They share the restored
# packages (stage "restore") and BuildKit builds them at the same time. The bundle stage copies only the C# folders and not
# the views, scripts or wwwroot, so an edit to a page, a style or a script does not rebuild the bundle (about 50 s). Only the
# output of "publish" and "bundle" ships: the final image holds no SDK, no Node and no source.
#
# Every base image is pinned by tag and digest (checked on 2026-11-20). Bump them on purpose, together: the runtime must
# not be older than the EF packages in the csproj (10.0.15).
FROM node:26.13.1-alpine3.24@sha256:6bd9ab43c0ec0190ca7b2d193906cb8b7edf6e7f4afa43ddeccd6452710bffb5 AS assets

WORKDIR /build
COPY src/Peekbox.Web/package.json src/Peekbox.Web/package-lock.json ./
RUN npm ci --ignore-scripts
COPY src/Peekbox.Web/tsconfig.json ./
COPY src/Peekbox.Web/Scripts ./Scripts
# Output goes to wwwroot/js (tsconfig); the check fails the build if nothing came out.
RUN npm run build && [ -n "$(find wwwroot/js -name '*.js' | head -n 1)" ]

FROM mcr.microsoft.com/dotnet/sdk:10.0.404-alpine3.24@sha256:0294f285d062afacc8b5f6d7febb0cd4ee879320e5585fa15f2f4ebf1feb9ba7 AS restore

WORKDIR /src
# Restore first: this layer is reused until a package changes.
COPY Directory.Build.props dotnet-tools.json ./
COPY src/Peekbox.Web/Peekbox.Web.csproj src/Peekbox.Web/
RUN dotnet restore src/Peekbox.Web/Peekbox.Web.csproj && dotnet tool restore

FROM restore AS publish
# content/data/*.json and the Markdown texts in content/ (blog posts, the About page and the legal texts) are linked into
# the app by the csproj (the sample catalogue, the home page text and the texts); .dockerignore lets those .md files through.
COPY src ./src
COPY content ./content
COPY --from=assets /build/wwwroot/js ./src/Peekbox.Web/wwwroot/js
RUN dotnet publish src/Peekbox.Web/Peekbox.Web.csproj -c Release --no-restore -o /out/web

FROM restore AS bundle
# Only what the bundle compiles: the C# folders and the files the project names. A NEW folder of C# in src/Peekbox.Web goes
# in this list too (a missing one fails the bundle build with a compile error, never silently). Views, Scripts and wwwroot
# are left out on purpose: they are not part of the DbContext, and leaving them out keeps this stage cached when only they change.
COPY src/Peekbox.Web/Program.cs src/Peekbox.Web/appsettings.json src/Peekbox.Web/
COPY src/Peekbox.Web/Content ./src/Peekbox.Web/Content
COPY src/Peekbox.Web/Controllers ./src/Peekbox.Web/Controllers
COPY src/Peekbox.Web/Data ./src/Peekbox.Web/Data
COPY src/Peekbox.Web/Formatting ./src/Peekbox.Web/Formatting
COPY src/Peekbox.Web/Migrations ./src/Peekbox.Web/Migrations
COPY src/Peekbox.Web/Models ./src/Peekbox.Web/Models
COPY src/Peekbox.Web/Navigation ./src/Peekbox.Web/Navigation
COPY src/Peekbox.Web/Properties ./src/Peekbox.Web/Properties
COPY src/Peekbox.Web/Security ./src/Peekbox.Web/Security
COPY src/Peekbox.Web/Services ./src/Peekbox.Web/Services
COPY src/Peekbox.Web/ViewComponents ./src/Peekbox.Web/ViewComponents
COPY content ./content
# The app stops at start without a connection string, so finding the DbContext here needs a placeholder; the real one
# is given when the bundle runs. The bundle is NOT self-contained: it runs on the .NET runtime the site image already has
# (34 MB instead of 135 MB with its own runtime), so it rides in the site image (decision 2026-11-22).
RUN ConnectionStrings__Default="Host=placeholder;Database=placeholder" \
    dotnet ef migrations bundle --project src/Peekbox.Web -r linux-musl-x64 -o /out/efbundle \
 && [ -x /out/efbundle ]

# The one image: the site, and the migration bundle at /app/efbundle (run it with `--entrypoint /app/efbundle`; the
# deployment runs it before the site starts). Port 8080 and the unprivileged "app" user come from the base image. The files
# stay root-owned: the site never writes to its own folder.
#
# Alpine's dotnet images turn globalization off (no ICU, no time zones), and the seed and the site need Vietnamese culture
# and Asia/Ho_Chi_Minh, so the packages are added and the switch is turned off (docs/architecture.md, section 11).
# krb5-libs: without it the database driver prints "Cannot load library libgssapi_krb5.so.2" when it connects.
# The bundle starts the app's own host to find the DbContext, so the image holds what the app reads at start
# (appsettings.json, Content/) and the sample content the seed reads (Seed/); they are all in /out/web.
FROM mcr.microsoft.com/dotnet/aspnet:10.0.15-alpine3.24@sha256:21c3f9798416cba4c0576a947c286231cb71ea2884896cb968253f5c9668f930 AS web
RUN apk add --no-cache icu-libs icu-data-full tzdata krb5-libs
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false
WORKDIR /app
COPY --from=publish /out/web ./
COPY --from=bundle /out/efbundle /app/efbundle
USER app
EXPOSE 8080
ENTRYPOINT ["dotnet", "Peekbox.Web.dll"]
