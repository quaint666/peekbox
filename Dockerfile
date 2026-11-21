# Peekbox: two images from this one file.
#
#   docker build -t peekbox-migrate --target migrate .   the migration bundle: creates and updates the tables, and fills
#                                                        EMPTY tables with the sample content (run it before the web image)
#   docker build -t peekbox .                            the web site (the last stage, so the default)
#
# Stage "assets" compiles the browser TypeScript (tsc fails the build on a type error). Stage "build" publishes the site
# and builds the migration bundle with the dotnet-ef version pinned in dotnet-tools.json. Only their output ships: the
# two final images hold no SDK, no Node and no source.
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

FROM mcr.microsoft.com/dotnet/sdk:10.0.404-alpine3.24@sha256:0294f285d062afacc8b5f6d7febb0cd4ee879320e5585fa15f2f4ebf1feb9ba7 AS build

WORKDIR /src
# Restore first: this layer is reused until a package changes.
COPY Directory.Build.props dotnet-tools.json ./
COPY src/Peekbox.Web/Peekbox.Web.csproj src/Peekbox.Web/
RUN dotnet restore src/Peekbox.Web/Peekbox.Web.csproj && dotnet tool restore

# content/data/*.json and the Markdown texts in content/ (blog posts) are linked into the app by the csproj (the sample
# catalogue, the home page text and the blog); .dockerignore lets those .md files through.
COPY src ./src
COPY content ./content
COPY --from=assets /build/wwwroot/js ./src/Peekbox.Web/wwwroot/js
RUN dotnet publish src/Peekbox.Web/Peekbox.Web.csproj -c Release --no-restore -o /out/web

# The app stops at start without a connection string, so finding the DbContext here needs a placeholder; the real one
# is given when the bundle runs.
RUN ConnectionStrings__Default="Host=placeholder;Database=placeholder" \
    dotnet ef migrations bundle --project src/Peekbox.Web --self-contained -r linux-musl-x64 -o /out/efbundle \
 && [ -x /out/efbundle ]

# The migration image: the self-contained bundle on the plain runtime dependencies. Alpine's dotnet images turn
# globalization off (no ICU, no time zones), and the seed and the site need Vietnamese culture and Asia/Ho_Chi_Minh,
# so the three packages are added and the switch is turned off (docs/architecture.md, section 11). krb5-libs is the
# fourth: without it the database driver prints "Cannot load library libgssapi_krb5.so.2" when it connects.
FROM mcr.microsoft.com/dotnet/runtime-deps:10.0.15-alpine3.24@sha256:52a2a345be27cc05c35f20bdcb4fa7cdfe9dac0ee7d8b8cd9114d2a44a96b7f5 AS migrate
RUN apk add --no-cache icu-libs icu-data-full tzdata krb5-libs
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false
WORKDIR /app
COPY --from=build /out/efbundle ./
# The bundle starts the app's own host to find the DbContext, so it needs what the app reads at start (appsettings.json
# and the home page text) and the sample content the seed reads (Seed/).
COPY --from=build /out/web/appsettings.json ./
COPY --from=build /out/web/Content ./Content
COPY --from=build /out/web/Seed ./Seed
USER app
ENTRYPOINT ["./efbundle"]

# The web image (the last stage, so what a plain `docker build .` makes). Port 8080 and the unprivileged "app" user come
# from the base image. The files stay root-owned: the site never writes to its own folder.
FROM mcr.microsoft.com/dotnet/aspnet:10.0.15-alpine3.24@sha256:21c3f9798416cba4c0576a947c286231cb71ea2884896cb968253f5c9668f930 AS web
RUN apk add --no-cache icu-libs icu-data-full tzdata krb5-libs
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false
WORKDIR /app
COPY --from=build /out/web ./
USER app
EXPOSE 8080
ENTRYPOINT ["dotnet", "Peekbox.Web.dll"]
