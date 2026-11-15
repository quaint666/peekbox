namespace Peekbox.Web.Models;

public sealed record BuilderViewModel
{
    // The quick amounts of F14 (docs/functions.md). The only place they are written.
    public static readonly IReadOnlyList<long> QuickAmounts = [300_000, 500_000, 800_000, 1_000_000];
}
