using System.Globalization;
using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Peekbox.Web.Data;
using Peekbox.Web.Services;

using static Peekbox.Tests.TestDates;

namespace Peekbox.Tests;

// The rules of a discount code on their own (F38): what a typed code becomes, why a code is refused (in the order they are
// checked), and what it takes off.
public class CouponRulesTests
{
    private static readonly DateOnly Today = new(2026, 12, 1);

    private static Coupon Percent(long value = 10) => new() { Code = "A", Kind = CouponKind.Percent, Value = value };

    [Theory]
    [InlineData("chao10", "CHAO10")]
    [InlineData("  chao 10 \t", "CHAO10")]
    [InlineData("Qua20k", "QUA20K")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void A_typed_code_is_trimmed_without_inner_spaces_and_upper_case(string? typed, string expected) =>
        Assert.Equal(expected, CouponRules.Normalize(typed));

    [Fact]
    public void A_code_that_does_not_exist_is_unknown()
    {
        Assert.Equal(CouponProblem.Unknown, CouponRules.Check(null, 500_000, Today));
        Assert.Equal(CouponProblem.None, CouponRules.Check(Percent(), 500_000, Today));
    }

    [Fact]
    public void A_code_works_on_its_first_and_last_day_and_not_a_day_outside()
    {
        var coupon = Percent();
        coupon.ValidFrom = Today;
        coupon.ValidTo = Today.AddDays(3);

        Assert.Equal(CouponProblem.NotStarted, CouponRules.Check(coupon, 1, Today.AddDays(-1)));
        Assert.Equal(CouponProblem.None, CouponRules.Check(coupon, 1, Today));
        Assert.Equal(CouponProblem.None, CouponRules.Check(coupon, 1, Today.AddDays(3)));
        Assert.Equal(CouponProblem.Expired, CouponRules.Check(coupon, 1, Today.AddDays(4)));
    }

    [Fact]
    public void A_code_is_used_up_when_its_uses_reach_the_maximum_and_the_minimum_is_for_the_goods()
    {
        var coupon = Percent();
        coupon.MaxUses = 2;
        coupon.Uses = 1;
        coupon.MinGoodsVnd = 100_000;

        Assert.Equal(CouponProblem.BelowMinimum, CouponRules.Check(coupon, 99_999, Today));
        Assert.Equal(CouponProblem.None, CouponRules.Check(coupon, 100_000, Today));
        coupon.Uses = 2;
        Assert.Equal(CouponProblem.UsedUp, CouponRules.Check(coupon, 100_000, Today));
        coupon.MaxUses = null;
        Assert.Equal(CouponProblem.None, CouponRules.Check(coupon, 100_000, Today));
    }

    [Fact]
    public void The_first_problem_wins_in_the_order_expired_then_used_up_then_minimum()
    {
        var coupon = Percent();
        coupon.ValidTo = Today.AddDays(-1);
        coupon.MaxUses = 1;
        coupon.Uses = 1;
        coupon.MinGoodsVnd = 1_000_000;

        Assert.Equal(CouponProblem.Expired, CouponRules.Check(coupon, 1, Today));
        coupon.ValidTo = null;
        Assert.Equal(CouponProblem.UsedUp, CouponRules.Check(coupon, 1, Today));
    }

    [Theory]
    [InlineData(10, 180_000, 18_000)]
    [InlineData(10, 155_555, 15_555)]   // rounded down to whole dong
    [InlineData(10, 9, 0)]
    [InlineData(100, 75_000, 75_000)]
    public void A_percentage_comes_off_the_goods_rounded_down(long percent, long goods, long expected) =>
        Assert.Equal(expected, CouponRules.Discount(Percent(percent), goods));

    [Theory]
    [InlineData(20_000, 180_000, 20_000)]
    [InlineData(20_000, 15_000, 15_000)]   // never more than the goods cost
    public void A_fixed_amount_never_takes_more_than_the_goods_cost(long amount, long goods, long expected) =>
        Assert.Equal(expected, CouponRules.Discount(new Coupon { Code = "B", Kind = CouponKind.Fixed, Value = amount }, goods));

    [Fact]
    public void Each_problem_has_its_words_and_the_labels_say_what_the_code_gives()
    {
        var coupon = new Coupon { Code = "C", Kind = CouponKind.Fixed, Value = 20_000, MinGoodsVnd = 100_000, ValidFrom = new DateOnly(2026, 12, 20) };

        Assert.Equal("Nhập mã giảm giá.", CouponRules.Message(CouponProblem.Empty, null));
        Assert.Equal("Mã giảm giá không hợp lệ.", CouponRules.Message(CouponProblem.Unknown, null));
        Assert.Equal("Mã giảm giá chưa dùng được, bắt đầu từ 20/12/2026.", CouponRules.Message(CouponProblem.NotStarted, coupon));
        Assert.Equal("Mã giảm giá đã hết hạn.", CouponRules.Message(CouponProblem.Expired, coupon));
        Assert.Equal("Mã giảm giá đã hết lượt dùng.", CouponRules.Message(CouponProblem.UsedUp, coupon));
        Assert.Equal("Mã này dùng cho đơn có tiền hàng từ 100.000đ.", CouponRules.Message(CouponProblem.BelowMinimum, coupon));
        Assert.Equal("Giảm 20.000đ", CouponRules.Label(coupon));
        Assert.Equal("Giảm 10%", CouponRules.Label(Percent()));
    }
}

// F38 through HTTP: the cart's code field, the checkout and the order with a code. Seed: CHAO10 (10 %) and QUA20K (20.000đ off
// goods of 100.000đ or more); Trà sen 180.000, Bánh quy bơ 75.000, hard box with bow 40.000; the first ring-1 ward 25.000.
[Collection(DatabaseCollection.Name)]
public class CouponTests(PeekboxFixture fixture)
{
    private async Task<string> HamperAsync(string slug = "tra-sen", string? code = null)
    {
        var token = Guid.NewGuid().ToString("N");
        await fixture.WithDbAsync(async db =>
        {
            var hamper = new Hamper
            {
                Token = token, BudgetVnd = 500_000, DeliveryDate = Delivery.AddDays(60), PackagingId = "hard-bow", CouponCode = code,
                CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow,
            };
            hamper.Items.Add(new HamperItem { ProductId = (await db.Products.SingleAsync(p => p.Slug == slug)).Id, Quantity = 1 });
            db.Hampers.Add(hamper);
            return await db.SaveChangesAsync();
        });
        return token;
    }

    private async Task<string> NewCodeAsync(Action<Coupon>? change = null)
    {
        var coupon = new Coupon { Code = "T" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant(), Kind = CouponKind.Percent, Value = 10 };
        change?.Invoke(coupon);
        await fixture.WithDbAsync(async db =>
        {
            db.Coupons.Add(coupon);
            return await db.SaveChangesAsync();
        });
        return coupon.Code;
    }

    private async Task<string?> HamperCodeAsync(string hamperToken) =>
        await fixture.WithDbAsync(db => db.Hampers.AsNoTracking().Where(h => h.Token == hamperToken).Select(h => h.CouponCode).SingleAsync());

    private async Task<Visitor> OnCartAsync(string hamperToken)
    {
        var visitor = new Visitor(fixture.Client(), hamperToken);
        await visitor.PageAsync("/cart");
        return visitor;
    }

    private static Dictionary<string, string> Code(string code) => new() { ["code"] = code };

    private static async Task<string> FollowAsync(Visitor visitor, HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/cart#ma-giam-gia", response.Headers.Location?.OriginalString);
        return await visitor.PageAsync("/cart");
    }

    [Fact]
    public async Task A_code_that_works_comes_off_the_goods_in_the_cart_and_can_be_taken_off()
    {
        var hamper = await HamperAsync();
        var visitor = await OnCartAsync(hamper);

        var html = await FollowAsync(visitor, await visitor.PostAsync("/cart/coupon", Code(" chao 10 ")));

        Assert.Equal("CHAO10", await HamperCodeAsync(hamper));
        Assert.Contains("<div class=\"cart-sums__discount\"><dt>Mã CHAO10 (giảm 10%)</dt><dd>−18.000đ</dd></div>", html);
        Assert.Contains("<strong>202.000đ</strong>", html);   // 180.000 + 40.000 packaging - 18.000
        Assert.Contains("Bỏ mã", html);
        Assert.DoesNotContain("id=\"coupon-code\"", html);
        BuilderItemsTests.AssertWellFormed("cart with a code", html);

        html = await FollowAsync(visitor, await visitor.PostAsync("/cart/coupon/remove", []));

        Assert.Null(await HamperCodeAsync(hamper));
        Assert.DoesNotContain("cart-sums__discount", html);
        Assert.Contains("<strong>220.000đ</strong>", html);
        Assert.Contains("id=\"coupon-code\"", html);
    }

    [Fact]
    public async Task A_fixed_code_takes_its_amount_and_a_code_below_its_minimum_is_refused()
    {
        var visitor = await OnCartAsync(await HamperAsync());
        Assert.Contains("−20.000đ", await FollowAsync(visitor, await visitor.PostAsync("/cart/coupon", Code("QUA20K"))));

        var cheap = await HamperAsync("banh-quy-bo");   // 75.000: under the 100.000 minimum
        var other = await OnCartAsync(cheap);
        var html = await FollowAsync(other, await other.PostAsync("/cart/coupon", Code("QUA20K")));

        Assert.Contains("Mã này dùng cho đơn có tiền hàng từ 100.000đ.", html);
        Assert.Null(await HamperCodeAsync(cheap));
    }

    [Fact]
    public async Task A_refused_code_says_why_under_the_field_and_keeps_what_was_typed()
    {
        var hamper = await HamperAsync();
        var visitor = await OnCartAsync(hamper);
        var today = TodayInVietnam;
        var expired = await NewCodeAsync(c => c.ValidTo = today.AddDays(-1));
        var later = await NewCodeAsync(c => c.ValidFrom = today.AddDays(5));
        var usedUp = await NewCodeAsync(c => { c.MaxUses = 3; c.Uses = 3; });

        var cases = new (string Typed, string Message)[]
        {
            ("KHONGCO", "Mã giảm giá không hợp lệ."),
            ("   ", "Nhập mã giảm giá."),
            (expired, "Mã giảm giá đã hết hạn."),
            (later, $"Mã giảm giá chưa dùng được, bắt đầu từ {Peekbox.Web.Formatting.VnDate.Day(today.AddDays(5))}."),
            (usedUp, "Mã giảm giá đã hết lượt dùng."),
            (new string('x', 200), "Mã giảm giá không hợp lệ."),
        };
        foreach (var (typed, message) in cases)
        {
            var html = await FollowAsync(visitor, await visitor.PostAsync("/cart/coupon", Code(typed)));

            Assert.Contains($"<p class=\"field__error\" id=\"coupon-code-error\" role=\"alert\">{message}</p>", html);
            Assert.Contains("aria-invalid=\"true\" aria-describedby=\"coupon-code-error\"", html);
            Assert.Null(await HamperCodeAsync(hamper));
        }

        // The error is shown once: a reload has none, and what was typed is not kept either.
        Assert.DoesNotContain("coupon-code-error", await visitor.PageAsync("/cart"));
    }

    [Fact]
    public async Task Every_try_counts_and_too_many_are_refused_with_a_note()
    {
        await using var strict = fixture.Factory.WithWebHostBuilder(builder => builder.UseSetting("RateLimits:CouponTriesPer15Minutes", "2"));
        var visitor = new Visitor(fixture.Client(strict), await HamperAsync());
        await visitor.PageAsync("/cart");

        await visitor.PostAsync("/cart/coupon", Code("AAA"));
        await visitor.PostAsync("/cart/coupon", Code("BBB"));
        var html = await FollowAsync(visitor, await visitor.PostAsync("/cart/coupon", Code("CHAO10")));

        Assert.Contains("Bạn đã thử quá nhiều lần. Hãy đợi vài phút rồi thử lại.", html);
        Assert.DoesNotContain("cart-sums__discount", html);   // the right code was not even tried
    }

    [Fact]
    public async Task A_post_without_the_form_token_is_refused_and_an_empty_cart_has_no_code()
    {
        var visitor = await OnCartAsync(await HamperAsync());
        Assert.Equal(HttpStatusCode.BadRequest, (await visitor.PostAsync("/cart/coupon", Code("CHAO10"), withToken: false)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await visitor.PostAsync("/cart/coupon/remove", [], withToken: false)).StatusCode);

        var nobody = new Visitor(fixture.Client());
        await nobody.PageAsync("/cart");
        Assert.Equal("/cart", (await nobody.PostAsync("/cart/coupon", Code("CHAO10"))).Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task A_code_that_stops_working_blocks_the_checkout_until_it_is_taken_off()
    {
        var code = await NewCodeAsync();
        var hamper = await HamperAsync(code: code);
        var visitor = await OnCartAsync(hamper);
        Assert.Contains($"Mã {code} (giảm 10%)", await visitor.PageAsync("/cart"));

        await fixture.WithDbAsync(db => db.Coupons.Where(c => c.Code == code).ExecuteUpdateAsync(s => s.SetProperty(c => c.ValidTo, TodayInVietnam.AddDays(-1))));
        var html = await visitor.PageAsync("/cart");

        Assert.Contains("Mã giảm giá đã hết hạn.", html);
        Assert.Contains("Mã giảm giá không còn áp dụng. Bỏ mã để tiếp tục.", html);
        Assert.Contains("cart-checkout\" disabled", html);
        Assert.DoesNotContain("cart-sums__discount", html);
        Assert.Contains("<strong>220.000đ</strong>", html);
        Assert.Equal("/cart", (await visitor.GetAsync("/checkout")).Headers.Location?.OriginalString);

        await FollowAsync(visitor, await visitor.PostAsync("/cart/coupon/remove", []));
        Assert.Equal(HttpStatusCode.OK, (await visitor.GetAsync("/checkout")).StatusCode);
    }

    private static Dictionary<string, string> Form(int wardId) => new()
    {
        ["senderName"] = "Nguyễn Văn A", ["senderPhone"] = "0900000000", ["senderEmail"] = "ten@example.com",
        ["recipientName"] = "Trần Thị B", ["recipientPhone"] = "0900111111", ["ward"] = wardId.ToString(CultureInfo.InvariantCulture),
        ["street"] = "12 đường mẫu", ["window"] = "Evening", ["payment"] = "CashOnDelivery",
    };

    private async Task<Dictionary<string, int>> StockAsync() =>
        await fixture.WithDbAsync(db => db.Products.AsNoTracking().ToDictionaryAsync(p => p.Slug, p => p.Stock));

    [Fact]
    public async Task The_order_keeps_the_code_and_the_discount_and_takes_one_use()
    {
        var stock = await StockAsync();
        try
        {
            var code = await NewCodeAsync(c => { c.MaxUses = 5; c.Value = 10; });
            var hamper = await HamperAsync(code: code);
            var visitor = new Visitor(fixture.Client(), hamper);
            var checkout = await visitor.PageAsync("/checkout");
            Assert.Contains($"<dt>Mã {code} (giảm 10%)</dt><dd>−18.000đ</dd>", checkout);
            Assert.Contains("<strong>202.000đ</strong>", checkout);   // 180.000 + 40.000 - 18.000, fee not chosen yet
            var ward = await fixture.WithDbAsync(db => db.Wards.AsNoTracking().OrderBy(w => w.SortOrder).FirstAsync(w => w.Ring == 1));

            var location = (await visitor.PostAsync("/checkout", Form(ward.Id))).Headers.Location?.OriginalString;

            var order = await fixture.WithDbAsync(db => db.Orders.AsNoTracking().SingleAsync(o => "/orders/" + o.Token == location));
            Assert.Equal((code, 18_000L, 180_000L, 220_000L - 18_000L + 25_000L), (order.CouponCode, order.DiscountVnd, order.GoodsVnd, order.TotalVnd));
            Assert.Equal(1, await fixture.WithDbAsync(db => db.Coupons.Where(c => c.Code == code).Select(c => c.Uses).SingleAsync()));
            var html = await visitor.PageAsync(location!);
            Assert.Contains($"<dt>Mã {code}</dt><dd>−18.000đ</dd>", html);
            Assert.Contains("<dt>Tổng cộng</dt><dd>227.000đ</dd>", html);

            // A code is kept on the order as it was: taking it off the hamper later is no longer possible (the hamper is an order).
            Assert.Equal(code, (await HamperCodeAsync(hamper)));
        }
        finally
        {
            await TestOrders.RestoreStockAsync(fixture, stock);
        }
    }

    [Fact]
    public async Task An_order_without_a_code_has_no_discount()
    {
        var stock = await StockAsync();
        try
        {
            var (token, _, _) = await TestOrders.PlaceAsync(fixture, 1, PaymentMethod.CashOnDelivery);
            var order = await fixture.WithDbAsync(db => db.Orders.AsNoTracking().SingleAsync(o => o.Token == token));
            Assert.Equal((null, 0L), (order.CouponCode, order.DiscountVnd));
            Assert.DoesNotContain("order-sums__discount", await fixture.Client().GetStringAsync($"/orders/{token}"));
        }
        finally
        {
            await TestOrders.RestoreStockAsync(fixture, stock);
        }
    }

    [Fact]
    public async Task Two_orders_racing_for_the_last_use_cannot_both_get_it()
    {
        var stock = await StockAsync();
        try
        {
            var code = await NewCodeAsync(c => c.MaxUses = 1);
            var ward = await fixture.WithDbAsync(db => db.Wards.AsNoTracking().OrderBy(w => w.SortOrder).FirstAsync(w => w.Ring == 1));
            var hampers = new[] { await HamperAsync(code: code), await HamperAsync(code: code), await HamperAsync(code: code) };

            var results = await Task.WhenAll(hampers.Select(async token =>
            {
                using var scope = fixture.Factory.Services.CreateScope();
                return await scope.ServiceProvider.GetRequiredService<OrderService>().PlaceAsync(token, new OrderRequest(
                    "Nguyễn Văn A", "0900000000", "ten@example.com", "Trần Thị B", "0900111111", ward.Id, "12 đường mẫu", DeliveryWindow.Morning, PaymentMethod.CashOnDelivery));
            }));

            Assert.Equal(1, results.Count(r => r.Outcome == PlaceOutcome.Placed));
            Assert.Equal(2, results.Count(r => r.Outcome == PlaceOutcome.NotReady));
            Assert.Equal(1, await fixture.WithDbAsync(db => db.Coupons.Where(c => c.Code == code).Select(c => c.Uses).SingleAsync()));
            Assert.Equal(1, await fixture.WithDbAsync(db => db.Orders.CountAsync(o => o.CouponCode == code)));
        }
        finally
        {
            await TestOrders.RestoreStockAsync(fixture, stock);
        }
    }

    [Fact]
    public async Task The_seeded_codes_are_there_and_the_database_refuses_a_code_or_an_order_that_makes_no_sense()
    {
        var seeded = await fixture.WithDbAsync(db => db.Coupons.AsNoTracking().Where(c => c.Code == "CHAO10" || c.Code == "QUA20K").OrderBy(c => c.Code).ToListAsync());
        Assert.Equal(["CHAO10", "QUA20K"], seeded.Select(c => c.Code));
        Assert.Equal((CouponKind.Percent, 10L, 0L), (seeded[0].Kind, seeded[0].Value, seeded[0].MinGoodsVnd));
        Assert.Equal((CouponKind.Fixed, 20_000L, 100_000L), (seeded[1].Kind, seeded[1].Value, seeded[1].MinGoodsVnd));

        await Assert.ThrowsAsync<DbUpdateException>(() => NewCodeAsync(c => c.Value = 101));              // over 100 %
        await Assert.ThrowsAsync<DbUpdateException>(() => NewCodeAsync(c => c.Code = "CHAO10"));          // a repeat
        await Assert.ThrowsAsync<DbUpdateException>(() => NewCodeAsync(c => { c.Kind = CouponKind.Fixed; c.Value = 0; }));
    }
}
