namespace Peekbox.Web.Navigation;

// Where a builder form sends the visitor back to after it saves: the amount buttons ("from") and step 3's "Tiếp tục"
// ("next"). Sent in the form's address or as a button value, in lower case (Value()); read by name, any case. Any other
// value is refused (400).
public enum ReturnTo
{
    Builder,   // the list, at the same card (the default)
    Item,      // the item's own page
    Review,    // the review page
    Cart,      // the cart
}

public static class ReturnToValues
{
    // "item", "review": what a form sends.
    public static string Value(this ReturnTo to) => to.ToString().ToLowerInvariant();
}
