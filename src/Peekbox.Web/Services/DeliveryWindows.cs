using Peekbox.Web.Data;

namespace Peekbox.Web.Services;

// The three delivery windows as the customer reads them (checkout, confirmation), from the checkout mockup.
public static class DeliveryWindows
{
    public static readonly IReadOnlyList<DeliveryWindow> All = [DeliveryWindow.Morning, DeliveryWindow.Afternoon, DeliveryWindow.Evening];

    public static string Label(DeliveryWindow window) => window switch
    {
        DeliveryWindow.Morning => "Sáng 8:00 – 12:00",
        DeliveryWindow.Afternoon => "Chiều 13:00 – 17:00",
        DeliveryWindow.Evening => "Tối 17:00 – 21:00",
        _ => throw new ArgumentOutOfRangeException(nameof(window), window, null),
    };
}
