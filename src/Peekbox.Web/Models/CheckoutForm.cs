using System.Text.RegularExpressions;
using Peekbox.Web.Data;
using Peekbox.Web.Services;

namespace Peekbox.Web.Models;

// What the checkout form posts (F41, F42, F44). Strings as typed; CheckoutCheck reads them. Bound by name, without
// asp-for and data annotations, as the builder's form is: the messages need the visitor's own input ("Bạn đã nhập 7 chữ
// số"), which an attribute cannot say, and asp-for would print English text.
public sealed class CheckoutForm
{
    public string? SenderName { get; set; }

    public string? SenderPhone { get; set; }

    public string? SenderEmail { get; set; }

    public string? RecipientName { get; set; }

    public string? RecipientPhone { get; set; }

    public int? Ward { get; set; }

    public string? Street { get; set; }

    public DeliveryWindow Window { get; set; } = DeliveryWindow.Morning;   // as drawn: Sáng is chosen at first

    public PaymentMethod Payment { get; set; } = PaymentMethod.ZaloPay;

    // F43: the note for the card, and the checkbox "hide prices inside the hamper" (a checkbox sends nothing when off).
    public string? CardMessage { get; set; }

    public bool HidePrices { get; set; }
}

// One problem with one field. Summary is the line in the red box at the top; Message is the text under the field.
public sealed record FieldError(string Field, string Summary, string Message);

// The checks of the checkout form (F46), written once. A form with no errors gives the OrderRequest that OrderService
// places. The field ids are the input ids on the page, so the summary's links jump to the field.
public static class CheckoutCheck
{
    public const int NameMax = 100;
    public const int EmailMax = 254;
    public const int StreetMax = 300;

    // One @, something before it, a dot somewhere after it, no spaces: "ten@example" is refused (the error mockup).
    private static readonly Regex Email = new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.CultureInvariant);

    public static IReadOnlyList<FieldError> Check(CheckoutForm form, IReadOnlySet<int> wardIds, out OrderRequest? request)
    {
        var errors = new List<FieldError>();
        var senderName = Name(form.SenderName, "sender-name", "Nhập họ và tên người gửi", errors);
        var senderPhone = Phone(form.SenderPhone, "sender-phone", "người gửi", errors);
        var email = (form.SenderEmail ?? "").Trim();
        if (email.Length == 0)
        {
            errors.Add(new FieldError("sender-email", "Nhập email nhận xác nhận đơn hàng", "Nhập email để nhận xác nhận đơn hàng."));
        }
        else if (email.Length > EmailMax || !Email.IsMatch(email))
        {
            errors.Add(new FieldError("sender-email", "Email chưa đúng dạng", "Email chưa đúng dạng. Ví dụ: ten@example.com"));
        }

        var recipientName = Name(form.RecipientName, "recipient-name", "Nhập họ và tên người nhận", errors);
        var recipientPhone = Phone(form.RecipientPhone, "recipient-phone", "người nhận", errors);
        if (form.Ward is not { } ward || !wardIds.Contains(ward))
        {
            errors.Add(new FieldError("ward", "Chọn phường giao hàng", "Chọn phường giao hàng."));
        }

        var street = (form.Street ?? "").Trim();
        if (street.Length == 0)
        {
            errors.Add(new FieldError("street", "Nhập số nhà và tên đường", "Nhập số nhà và tên đường, ví dụ: 12 đường mẫu."));
        }
        else if (street.Length > StreetMax)
        {
            errors.Add(new FieldError("street", "Địa chỉ quá dài", $"Địa chỉ dài tối đa {StreetMax} ký tự."));
        }

        // An enum bound from a made-up value ("9", "Night") is not one of the choices.
        if (!Enum.IsDefined(form.Window))
        {
            errors.Add(new FieldError("window-morning", "Chọn khung giờ giao", "Chọn khung giờ giao."));
        }

        if (!Enum.IsDefined(form.Payment))
        {
            errors.Add(new FieldError("payment-zalopay", "Chọn cách thanh toán", "Chọn cách thanh toán."));
        }

        // Line breaks are kept (a card can have lines) but as one kind, and blank ones at the ends are dropped.
        var card = (form.CardMessage ?? "").Replace("\r\n", "\n").Replace('\r', '\n').Trim();
        if (card.Length > CardMessageRules.MaxLength)
        {
            errors.Add(new FieldError("card-message", "Lời nhắn quá dài", $"Lời nhắn dài tối đa {CardMessageRules.MaxLength} ký tự. Bạn đã nhập {card.Length} ký tự."));
        }

        request = errors.Count > 0
            ? null
            : new OrderRequest(senderName, senderPhone, email, recipientName, recipientPhone, form.Ward!.Value, street, form.Window, form.Payment,
                card.Length == 0 ? null : card, form.HidePrices);
        return errors;
    }

    private static string Name(string? text, string field, string summary, List<FieldError> errors)
    {
        var name = (text ?? "").Trim();
        if (name.Length == 0)
        {
            errors.Add(new FieldError(field, summary, summary + "."));
        }
        else if (name.Length > NameMax)
        {
            errors.Add(new FieldError(field, "Họ và tên quá dài", $"Họ và tên dài tối đa {NameMax} ký tự."));
        }

        return name;
    }

    // A typed phone with the spaces, dots and dashes between the digits dropped ("0900 111 111", "0900.111.111" give
    // "0900111111"). Checkout and the track page both read a phone this way.
    public static string PhoneDigits(string? text) => new((text ?? "").Trim().Where(c => c is not (' ' or '.' or '-')).ToArray());

    // A Vietnamese phone number: 10 digits starting with 0, separators allowed (PhoneDigits). [0-9], not
    // char.IsDigit: that also takes digits of other scripts.
    private static string Phone(string? text, string field, string whose, List<FieldError> errors)
    {
        var digits = PhoneDigits(text);
        FieldError? error =
            digits.Length == 0 ? new(field, $"Nhập số điện thoại {whose}", $"Nhập số điện thoại {whose}.")
            : !digits.All(char.IsAsciiDigit) ? new(field, $"Số điện thoại {whose} chưa đúng", "Số điện thoại chỉ gồm chữ số.")
            : digits.Length != 10 ? new(field, $"Số điện thoại {whose} cần 10 chữ số", $"Số điện thoại cần 10 chữ số. Bạn đã nhập {digits.Length} chữ số.")
            : digits[0] != '0' ? new(field, $"Số điện thoại {whose} chưa đúng", "Số điện thoại bắt đầu bằng số 0.")
            : null;
        if (error is not null)
        {
            errors.Add(error);
        }

        return digits;
    }
}
