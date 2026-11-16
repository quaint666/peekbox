using Peekbox.Web.Navigation;
using Peekbox.Web.Services;

namespace Peekbox.Web.Models;

// The item lines of the review page and the cart (_HamperLines): read-only, except "Bỏ món này" on a line that expires
// before the delivery date (F35). From says where that button brings the visitor back to (the review page or the cart).
public sealed record HamperLines(Box Box, ReturnTo From);
