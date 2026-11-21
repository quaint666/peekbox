using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Mvc;

namespace Peekbox.Web.Services;

// How often one visitor (one IP address) may place an order, look one up or start a new hamper (decisions 2026-11-14, M3 and 2026-11-21): a fixed
// window of 15 minutes, counts from appsettings.json ("RateLimits"). It is the framework's own limiter
// (System.Threading.RateLimiting), asked by the controller rather than run as middleware, so that a refused request
// still gets the page with a Vietnamese message (the "Bạn đã thử quá nhiều lần" state) instead of a bare 429.
// Behind a proxy the address is the visitor's only when ForwardedHeaders:Hops is set (Program.cs); without it every visitor has the proxy's address.
public sealed class RateLimits : IDisposable
{
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(15);

    private readonly PartitionedRateLimiter<string> _orders;
    private readonly PartitionedRateLimiter<string> _track;
    private readonly PartitionedRateLimiter<string> _newHampers;

    public RateLimits(IConfiguration configuration)
    {
        _orders = Create(Required(configuration, "RateLimits:OrdersPer15Minutes"));
        _track = Create(Required(configuration, "RateLimits:TrackPer15Minutes"));
        _newHampers = Create(Required(configuration, "RateLimits:NewHampersPer15Minutes"));
    }

    // True when this visitor may place an order now; each call uses one.
    public bool TryOrder(HttpContext context) => Try(_orders, context);

    // True when this visitor may look up an order now; each call uses one.
    public bool TryTrack(HttpContext context) => Try(_track, context);

    // True when this visitor may start another hamper now (a first "Lưu" or "Dùng hộp này" with no box of their own); each
    // call uses one. Without it a script could add rows to the database as fast as it could send forms.
    public bool TryNewHamper(HttpContext context) => Try(_newHampers, context);

    public void Dispose()
    {
        _orders.Dispose();
        _track.Dispose();
        _newHampers.Dispose();
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

public static class RateLimitResults
{
    // A refused new hamper: 429 and a "try later" for the browser; the status-code page (HomeController.NotFoundPage) shows
    // the friendly text, as it does for a 400 and a 404.
    public static IActionResult TooManyNewHampers(this ControllerBase controller)
    {
        controller.Response.Headers.RetryAfter = ((int)RateLimits.Window.TotalSeconds).ToString(System.Globalization.CultureInfo.InvariantCulture);
        return controller.StatusCode(StatusCodes.Status429TooManyRequests);
    }
}
