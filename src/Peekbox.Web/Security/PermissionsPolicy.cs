namespace Peekbox.Web.Security;

// The one place the browser features the site never uses are switched off. Program.cs sends it on every response, and the
// tests compare against it. Nothing here asks for the camera, the microphone, the position, a payment or a USB device,
// so no page (and no script that got in) can either. A feature the site starts to use is removed from this list.
public static class PermissionsPolicy
{
    public const string HeaderName = "Permissions-Policy";

    public const string Value = "camera=(), microphone=(), geolocation=(), payment=(), usb=()";
}
