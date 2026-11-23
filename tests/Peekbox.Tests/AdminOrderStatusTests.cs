using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Peekbox.Web.Data;
using Peekbox.Web.Security.Admin;
using Peekbox.Web.Services;

using static Peekbox.Tests.AdminOrdersKit;

namespace Peekbox.Tests;

// Moving an order one step forward, and its tracking code and note (F57). The change is one conditional UPDATE on the status the
// form was opened with, so two people moving the same order cannot both win; the parallel tests fire real simultaneous requests.
[Collection(DatabaseCollection.Name)]
public class AdminOrderStatusTests(PeekboxFixture fixture)
{
    private async Task<Visitor> SignedInAsync(string role = AdminRoles.Owner) =>
        await AdminTestKit.SignedInAsync(fixture, await AdminTestKit.CreateUserAsync(fixture, role));

    // Opens the orders page (so the form token is the person's) and posts the order's form.
    private static async Task<HttpResponseMessage> SaveAsync(Visitor visitor, int id, string from, string? to, string tracking = "", string note = "", string filterDate = "", string filterStatus = "", string filterQ = "")
    {
        await visitor.PageAsync($"/admin/orders?date={filterDate}&order={id}");
        var form = new Dictionary<string, string> { ["from"] = from, ["trackingCode"] = tracking, ["adminNote"] = note, ["filterDate"] = filterDate, ["filterStatus"] = filterStatus, ["filterQ"] = filterQ };
        if (to is not null)
        {
            form["to"] = to;
        }

        return await visitor.PostAsync($"/admin/orders/{id}/save", form);
    }

    private static async Task<string> FollowAsync(Visitor visitor, HttpResponseMessage response) =>
        await visitor.PageAsync(Assert.IsType<string>(response.Headers.Location?.OriginalString));

    // ---- the steps ----

    [Fact]
    public async Task An_order_moves_one_step_and_the_page_comes_back_to_the_same_list_and_order_with_a_notice()
    {
        var day = AdminOrdersKit.Day();
        var id = await OrderAsync(fixture, day);
        var visitor = await SignedInAsync();

        var response = await SaveAsync(visitor, id, "new", "preparing", filterDate: day.ToString("yyyy-MM-dd"), filterStatus: "new", filterQ: "Trần");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal($"/admin/orders?date={day:yyyy-MM-dd}&status=new&q=Tr%E1%BA%A7n&order={id}", response.Headers.Location?.OriginalString);
        Assert.Equal(OrderStatus.Preparing, (await ReloadAsync(fixture, id)).Status);
        var page = await FollowAsync(visitor, response);
        Assert.Contains($"Đã lưu đơn PEEK{id:D6}, trạng thái: Đang chuẩn bị.", page);
        Assert.DoesNotContain("Đã lưu đơn", await visitor.PageAsync(response.Headers.Location!.OriginalString));   // shown once
    }

    [Fact]
    public async Task An_order_goes_all_the_way_to_delivered_one_step_at_a_time()
    {
        var id = await OrderAsync(fixture, AdminOrdersKit.Day(), method: PaymentMethod.ZaloPay, payment: PaymentStatus.Paid);
        var visitor = await SignedInAsync();

        foreach (var (from, to) in new[] { ("new", "preparing"), ("preparing", "delivering"), ("delivering", "delivered") })
        {
            Assert.Equal(HttpStatusCode.Redirect, (await SaveAsync(visitor, id, from, to)).StatusCode);
            Assert.Equal(to, Key((await ReloadAsync(fixture, id)).Status));
        }

        Assert.Equal(PaymentStatus.Paid, (await ReloadAsync(fixture, id)).PaymentStatus);   // paid online: unchanged
    }

    [Fact]
    public async Task A_cash_on_delivery_order_is_marked_paid_when_it_is_delivered_and_not_before()
    {
        var id = await OrderAsync(fixture, AdminOrdersKit.Day(), OrderStatus.Delivering, PaymentMethod.CashOnDelivery, PaymentStatus.Pending);
        var visitor = await SignedInAsync();
        Assert.Equal(PaymentStatus.Pending, (await ReloadAsync(fixture, id)).PaymentStatus);

        await SaveAsync(visitor, id, "delivering", "delivered");

        var order = await ReloadAsync(fixture, id);
        Assert.Equal(OrderStatus.Delivered, order.Status);
        Assert.Equal(PaymentStatus.Paid, order.PaymentStatus);
    }

    [Theory]
    [InlineData("new", "delivering")]        // a step skipped
    [InlineData("new", "delivered")]
    [InlineData("preparing", "new")]         // backwards
    [InlineData("delivering", "preparing")]
    [InlineData("new", "cancelled")]         // cancelling is F70
    [InlineData("delivering", "cancelled")]
    public async Task Only_the_next_step_forward_is_accepted_and_the_order_is_left_alone_otherwise(string from, string to)
    {
        var status = Enum.Parse<OrderStatus>(from, ignoreCase: true);
        var id = await OrderAsync(fixture, AdminOrdersKit.Day(), status, PaymentMethod.CashOnDelivery, PaymentStatus.Pending);
        var visitor = await SignedInAsync();

        var response = await SaveAsync(visitor, id, from, to, tracking: "SHOULD-NOT-BE-SAVED");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("Chỉ chuyển được sang bước kế tiếp.", await FollowAsync(visitor, response));
        var order = await ReloadAsync(fixture, id);
        Assert.Equal(status, order.Status);
        Assert.Null(order.TrackingCode);   // nothing was saved, the texts included
    }

    [Theory]
    [InlineData("delivered")]
    [InlineData("cancelled")]
    public async Task A_final_order_has_its_status_off_and_still_takes_a_tracking_code_and_a_note(string finalStatus)
    {
        var status = Enum.Parse<OrderStatus>(finalStatus, ignoreCase: true);
        var day = AdminOrdersKit.Day();
        var id = await OrderAsync(fixture, day, status);
        var visitor = await SignedInAsync();

        var page = await visitor.PageAsync($"/admin/orders?date={day:yyyy-MM-dd}&order={id}");
        Assert.Matches(@"<select class=""input"" id=""order-status"" name=""to""[^>]*disabled", page);
        Assert.Contains("Đơn này đã ở trạng thái cuối", page);

        // The disabled select sends nothing: the form saves the two texts and leaves the status.
        await SaveAsync(visitor, id, finalStatus, null, tracking: "GHTK123", note: "Gọi trước khi giao");

        var order = await ReloadAsync(fixture, id);
        Assert.Equal(status, order.Status);
        Assert.Equal("GHTK123", order.TrackingCode);
        Assert.Equal("Gọi trước khi giao", order.AdminNote);
    }

    [Theory]
    [InlineData("", "preparing")]
    [InlineData("banana", "preparing")]
    [InlineData("new", "banana")]
    [InlineData("NEW", "PREPARING")]
    [InlineData("0", "1")]
    public async Task Status_words_that_are_not_ours_are_a_message_not_a_failure(string from, string to)
    {
        var id = await OrderAsync(fixture, AdminOrdersKit.Day());
        var visitor = await SignedInAsync();

        var response = await SaveAsync(visitor, id, from, to);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);   // never a 500
        Assert.Equal(OrderStatus.New, (await ReloadAsync(fixture, id)).Status);
    }

    [Fact]
    public async Task An_order_that_does_not_exist_is_a_message_and_a_form_without_a_token_is_refused()
    {
        var visitor = await SignedInAsync();
        await visitor.PageAsync("/admin/orders");

        var response = await visitor.PostAsync("/admin/orders/999999999/save", new() { ["from"] = "new", ["to"] = "preparing" });
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("Không tìm thấy đơn PEEK999999999.", await FollowAsync(visitor, response));

        var id = await OrderAsync(fixture, AdminOrdersKit.Day());
        var noToken = await visitor.PostAsync($"/admin/orders/{id}/save", new() { ["from"] = "new", ["to"] = "preparing" }, withToken: false);
        Assert.Equal(HttpStatusCode.BadRequest, noToken.StatusCode);
        Assert.Equal(OrderStatus.New, (await ReloadAsync(fixture, id)).Status);
    }

    // ---- paying ----

    [Theory]
    [InlineData(PaymentStatus.Failed)]
    [InlineData(PaymentStatus.Pending)]
    public async Task A_ZaloPay_order_that_is_not_paid_can_be_prepared_but_not_sent_out(PaymentStatus payment)
    {
        var id = await OrderAsync(fixture, AdminOrdersKit.Day(), OrderStatus.New, PaymentMethod.ZaloPay, payment);
        var visitor = await SignedInAsync();
        Assert.Equal(OrderStatus.Preparing, await MoveAsync(visitor, id, "new", "preparing"));

        var response = await SaveAsync(visitor, id, "preparing", "delivering", note: "x");

        Assert.Contains($"Đơn PEEK{id:D6} thanh toán ZaloPay chưa thành công nên chưa thể chuyển sang Đang giao.", await FollowAsync(visitor, response));
        var order = await ReloadAsync(fixture, id);
        Assert.Equal(OrderStatus.Preparing, order.Status);
        Assert.Null(order.AdminNote);   // all or nothing
    }

    private async Task<OrderStatus> MoveAsync(Visitor visitor, int id, string from, string to)
    {
        await SaveAsync(visitor, id, from, to);
        return (await ReloadAsync(fixture, id)).Status;
    }

    [Fact]
    public async Task A_cash_on_delivery_order_that_is_not_paid_can_go_out_for_delivery()
    {
        var id = await OrderAsync(fixture, AdminOrdersKit.Day(), OrderStatus.Preparing, PaymentMethod.CashOnDelivery, PaymentStatus.Pending);

        Assert.Equal(OrderStatus.Delivering, await MoveAsync(await SignedInAsync(), id, "preparing", "delivering"));
    }

    // ---- tracking code and note ----

    [Fact]
    public async Task The_tracking_code_and_the_note_are_saved_trimmed_and_an_empty_one_clears_the_column()
    {
        var id = await OrderAsync(fixture, AdminOrdersKit.Day(), trackingCode: "OLD", adminNote: "old note");
        var visitor = await SignedInAsync();

        await SaveAsync(visitor, id, "new", null, tracking: "  GHTK-001  ", note: "dòng một\r\ndòng hai\r\n ");
        var saved = await ReloadAsync(fixture, id);
        Assert.Equal("GHTK-001", saved.TrackingCode);
        Assert.Equal("dòng một\ndòng hai", saved.AdminNote);   // line breaks as \n, the ends trimmed

        await SaveAsync(visitor, id, "new", null, tracking: "   ", note: "");
        var cleared = await ReloadAsync(fixture, id);
        Assert.Null(cleared.TrackingCode);   // the database check forbids an empty string: null means none
        Assert.Null(cleared.AdminNote);
    }

    [Fact]
    public async Task A_tracking_code_or_a_note_that_is_too_long_saves_nothing_and_says_so()
    {
        var id = await OrderAsync(fixture, AdminOrdersKit.Day(), trackingCode: "KEEP");
        var visitor = await SignedInAsync();

        var tooLongCode = await SaveAsync(visitor, id, "new", "preparing", tracking: new string('1', OrderAdminRules.TrackingCodeMaxLength + 1));
        Assert.Contains($"Mã vận đơn tối đa {OrderAdminRules.TrackingCodeMaxLength} ký tự, ghi chú tối đa {OrderAdminRules.AdminNoteMaxLength} ký tự.", await FollowAsync(visitor, tooLongCode));
        var tooLongNote = await SaveAsync(visitor, id, "new", "preparing", note: new string('n', OrderAdminRules.AdminNoteMaxLength + 1));
        Assert.Contains("ghi chú tối đa", await FollowAsync(visitor, tooLongNote));

        var order = await ReloadAsync(fixture, id);
        Assert.Equal(OrderStatus.New, order.Status);       // the step was not made either
        Assert.Equal("KEEP", order.TrackingCode);

        // Exactly the longest allowed is fine.
        await SaveAsync(visitor, id, "new", null, tracking: new string('1', OrderAdminRules.TrackingCodeMaxLength), note: new string('n', OrderAdminRules.AdminNoteMaxLength));
        Assert.Equal(OrderAdminRules.AdminNoteMaxLength, (await ReloadAsync(fixture, id)).AdminNote!.Length);
    }

    [Fact]
    public async Task The_database_refuses_an_empty_or_too_long_tracking_code_and_note_whatever_the_code_does()
    {
        var id = await OrderAsync(fixture, AdminOrdersKit.Day());

        async Task<string> Constraint(string column, string value)
        {
            var error = await Record.ExceptionAsync(() => fixture.WithDbAsync(db => column == "TrackingCode"
                ? db.Orders.Where(o => o.Id == id).ExecuteUpdateAsync(s => s.SetProperty(o => o.TrackingCode, value))
                : db.Orders.Where(o => o.Id == id).ExecuteUpdateAsync(s => s.SetProperty(o => o.AdminNote, value))));
            var pg = Assert.IsType<Npgsql.PostgresException>(error?.InnerException ?? error);
            return pg.ConstraintName ?? pg.SqlState!;   // a text longer than the column stops at the column itself (22001), before any check
        }

        Assert.Equal("CK_Orders_TrackingCode", await Constraint("TrackingCode", ""));
        Assert.Equal("22001", await Constraint("TrackingCode", new string('x', 61)));
        Assert.Equal("CK_Orders_AdminNote", await Constraint("AdminNote", ""));
        Assert.Equal("22001", await Constraint("AdminNote", new string('x', 501)));
    }

    [Fact]
    public async Task A_customer_never_sees_the_note_or_the_tracking_code_and_does_see_the_new_status()
    {
        var id = await OrderAsync(fixture, AdminOrdersKit.Day(), trackingCode: "GHTK-SECRET-1", adminNote: "ghi chú nội bộ riêng");
        var visitor = await SignedInAsync();
        await SaveAsync(visitor, id, "new", "preparing");
        var token = (await ReloadAsync(fixture, id)).Token;

        var customerPage = await new Visitor(fixture.Client()).PageAsync($"/orders/{token}");

        Assert.DoesNotContain("GHTK-SECRET-1", customerPage);
        Assert.DoesNotContain("ghi chú nội bộ riêng", customerPage);
        Assert.Contains("Đang chuẩn bị", customerPage);
    }

    // ---- two people at once ----

    [Fact]
    public async Task A_form_opened_before_somebody_else_moved_the_order_saves_nothing_and_says_so()
    {
        var id = await OrderAsync(fixture, AdminOrdersKit.Day());
        var first = await SignedInAsync();
        var second = await SignedInAsync();
        await second.PageAsync($"/admin/orders?date=&order={id}");   // the second person opens the order while it is Mới

        await SaveAsync(first, id, "new", "preparing");                                    // the first moves it
        var response = await second.PostAsync($"/admin/orders/{id}/save",                  // the second sends the form they opened
            new() { ["from"] = "new", ["to"] = "preparing", ["trackingCode"] = "LATE", ["adminNote"] = "", ["filterDate"] = "", ["filterStatus"] = "", ["filterQ"] = "" });

        Assert.Contains("vừa được đổi trạng thái ở nơi khác, nên chưa lưu gì", await FollowAsync(second, response));
        var order = await ReloadAsync(fixture, id);
        Assert.Equal(OrderStatus.Preparing, order.Status);
        Assert.Null(order.TrackingCode);
    }

    // Ten tabs press "Lưu" at the same moment: exactly one makes the step. Seen to fail without the check on the status read.
    [Fact]
    public async Task Ten_requests_at_the_same_moment_make_the_step_once()
    {
        var id = await OrderAsync(fixture, AdminOrdersKit.Day());

        var results = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => Task.Run(async () =>
        {
            using var scope = fixture.Factory.Services.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<AdminOrders>().SaveAsync(id, OrderStatus.New, OrderStatus.Preparing, "T", "n");
        })));

        Assert.Single(results, r => r == StatusChange.Saved);
        Assert.Equal(9, results.Count(r => r == StatusChange.Stale));
        Assert.Equal(OrderStatus.Preparing, (await ReloadAsync(fixture, id)).Status);
    }

    [Fact]
    public async Task Ten_requests_at_the_same_moment_deliver_a_cash_order_once_and_mark_it_paid_once()
    {
        var id = await OrderAsync(fixture, AdminOrdersKit.Day(), OrderStatus.Delivering, PaymentMethod.CashOnDelivery, PaymentStatus.Pending);

        var results = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => Task.Run(async () =>
        {
            using var scope = fixture.Factory.Services.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<AdminOrders>().SaveAsync(id, OrderStatus.Delivering, OrderStatus.Delivered, null, null);
        })));

        Assert.Single(results, r => r == StatusChange.Saved);
        var order = await ReloadAsync(fixture, id);
        Assert.Equal((OrderStatus.Delivered, PaymentStatus.Paid), (order.Status, order.PaymentStatus));
    }

    [Fact]
    public async Task Two_different_steps_asked_at_the_same_moment_for_the_same_order_make_one_step_only()
    {
        var id = await OrderAsync(fixture, AdminOrdersKit.Day(), OrderStatus.New, PaymentMethod.CashOnDelivery, PaymentStatus.Pending);

        // "New to Preparing" five times and "New to Delivering" (a skipped step) five times.
        var results = await Task.WhenAll(Enumerable.Range(0, 10).Select(i => Task.Run(async () =>
        {
            using var scope = fixture.Factory.Services.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<AdminOrders>().SaveAsync(id, OrderStatus.New, i % 2 == 0 ? OrderStatus.Preparing : OrderStatus.Delivering, null, null);
        })));

        Assert.Single(results, r => r == StatusChange.Saved);
        Assert.Equal(5, results.Count(r => r == StatusChange.NotAllowed));
        Assert.Equal(OrderStatus.Preparing, (await ReloadAsync(fixture, id)).Status);
    }
}
