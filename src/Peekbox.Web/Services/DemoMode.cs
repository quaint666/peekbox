namespace Peekbox.Web.Services;

// Demo mode (Demo:Enabled in configuration, off by default): the copy of the site that people look at while it is
// built. The builder, the cart and every page work as usual; the checkout shows a notice and takes no order.
public sealed record DemoMode(bool Enabled);
