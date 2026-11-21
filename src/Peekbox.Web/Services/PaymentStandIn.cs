namespace Peekbox.Web.Services;

// ZaloPay is a stand-in until it is built (decision 2026-11-11): an order paid with it is marked paid at once. For F49 the
// stand-in can be told to fail instead ("Payment:SimulateFailure", false by default), which is the test switch the
// function list asks for: with it on, every ZaloPay payment fails, so the payment-failed page can be reached on staging
// and in tests. Read once at start. Cash on delivery is never affected.
public sealed record PaymentStandIn(bool Fails)
{
    public static PaymentStandIn FromConfiguration(IConfiguration configuration) => new(configuration.GetValue<bool>("Payment:SimulateFailure"));
}
