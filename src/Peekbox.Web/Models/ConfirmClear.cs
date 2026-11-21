using Peekbox.Web.Navigation;

namespace Peekbox.Web.Models;

// The "Xóa cả hộp quà?" question (Views/Shared/_ConfirmClear.cshtml): the popover's id (unique on the page) and where the
// visitor goes after "Xóa".
public sealed record ConfirmClear(string Id, ReturnTo From);
