namespace Peekbox.Web.Services;

// The shop's contact details, shown in the footer, the "Cần giúp?" line and the 404 page. Read once from the "Shop"
// section of appsettings.json (or Shop__Phone and so on in the environment), so they are typed in one place. The owner
// later types them into the admin Settings page instead (decision 2026-11-14); then only this class changes.
// The values in the file are made up (a demo shop), like Bloomy's.
public sealed record ShopInfo(string Phone, string Email, string Address, string SupportHours, string ZaloUrl)
{
    // The number for a tel: link: digits only.
    public string PhoneLink => "tel:" + new string(Phone.Where(char.IsAsciiDigit).ToArray());

    public string EmailLink => "mailto:" + Email;

    // The Zalo link as the contact panel shows it: without "https://" (zalo.me/0900000001).
    public string ZaloLabel => ZaloUrl.Replace("https://", "").Replace("http://", "").TrimEnd('/');

    public static ShopInfo FromConfiguration(IConfiguration configuration) => new(
        Required(configuration, "Shop:Phone"),
        Required(configuration, "Shop:Email"),
        Required(configuration, "Shop:Address"),
        Required(configuration, "Shop:SupportHours"),
        Required(configuration, "Shop:ZaloUrl"));

    private static string Required(IConfiguration configuration, string key)
    {
        var value = configuration[key];
        return string.IsNullOrWhiteSpace(value) ? throw new InvalidOperationException($"{key} must be set in appsettings.json.") : value.Trim();
    }
}
