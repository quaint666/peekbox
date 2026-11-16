using Peekbox.Web.Services;

namespace Peekbox.Web.Models;

// "Tổng kết" on the review page (F36). It is drawn twice, in the column on a wide screen and in the sheet on a phone, so
// IdSuffix keeps the ids inside each copy different.
public sealed record ReviewSummary(Box Box, string IdSuffix);
