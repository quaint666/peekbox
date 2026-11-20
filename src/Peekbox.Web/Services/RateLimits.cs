using System.Threading.RateLimiting;

namespace Peekbox.Web.Services;

// How often one visitor (one IP address) may place an order or look one up (decisions 2026-11-14 and M3): a fixed
// window of 15 minutes, counts from appsettings.json ("RateLimits"). It is the framework's own limiter
// (System.Threading.RateLimiting), asked by the controller rather than run as middleware, so that a refused request
// still gets the page with a Vietnamese message (the "Bạn đã thử quá nhiều lần" state) instead of a bare 429.
// Behind a proxy the address is the visitor's only when ForwardedHeaders:Hops is set (Program.cs); without it every visitor has the proxy's address.
public sealed class RateLimits : IDisposable
{
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(15);

    private readonly PartitionedRateLimiter<string> _orders;
    private readonly PartitionedRateLimiter<string> _track;

    public RateLimits(IConfiguration configuration)
    {
        _orders = Create(Required(configuration, "RateLimits:OrdersPer15Minutes"));
        _track = Create(Required(configuration, "RateLimits:TrackPer15Minutes"));
    }

    // True when this visitor may place an order now; each call uses one.
    public bool TryOrder(HttpContext context) => Try(_orders, context);

    // True when this visitor may look up an order now; each call uses one.
    public bool TryTrack(HttpContext context) => Try(_track, context);

    public void Dispose()
    {
        _orders.Dispose();
        _track.Dispose();
    }

    private static bool Try(PartitionedRateLimiter<string> limiter, HttpContext context)
    {
        using var lease = limiter.AttemptAcquire(context.Connection.RemoteIpAddress?.ToString() ?? "unknown");
        return lease.IsAcquired;
    }

    private static PartitionedRateLimiter<string> Create(int permits) =>
        PartitionedRateLimiter.Create<string, string>(ip => RateLimitPartition.GetFixedWindowLimiter(ip, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = permits,
            Window = Window,
            QueueLimit = 0,
            AutoReplenishment = true,
        }));

    private static int Required(IConfiguration configuration, string key)
    {
        var value = configuration.GetValue<int?>(key);
        return value is > 0 ? value.Value : throw new InvalidOperationException($"{key} must be a whole number above 0 in appsettings.json.");
    }
}
