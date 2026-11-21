namespace Peekbox.Web.Services;

// What the item page says about ingredients, allergens and storage (F29). These are placeholders, as the function list says:
// one text for each group of items, not the facts of one product, and the page says so. The facts of each item come with the
// admin (M6), which will store them on the item; until then nothing here is written to the database, so every item,
// including those already in a live database, has them at once. ReviewedOn is the day the texts were last read through.
public sealed record ItemInfoText(string? Ingredients, string Allergens, string Storage);

public static class ItemDetails
{
    // The day the texts below were last checked, shown as "Cập nhật lần cuối".
    public static readonly DateOnly ReviewedOn = new(2026, 11, 22);

    // Used for a group that has no text yet (a new category in the seed): it says nothing that could be wrong.
    private static readonly ItemInfoText Fallback = new(
        "Xem trên bao bì khi nhận hàng.", "Xem trên bao bì khi nhận hàng.", "Bảo quản nơi khô ráo, thoáng mát, tránh ánh nắng.");

    private static readonly IReadOnlyDictionary<string, ItemInfoText> ByCategory = new Dictionary<string, ItemInfoText>
    {
        ["banh"] = new("Bột mì, đường, bơ, trứng, hương liệu tự nhiên.", "Có thể chứa gluten, trứng, sữa và các loại hạt.", "Bảo quản nơi khô ráo, thoáng mát, tránh ánh nắng. Đóng kín sau khi mở."),
        ["tra-ca-phe"] = new("Lá trà hoặc hạt cà phê, có thể ướp hương hoa tự nhiên (tùy loại).", "Không có chất gây dị ứng phổ biến.", "Giữ kín, nơi khô ráo, tránh ẩm và mùi mạnh."),
        ["hat-trai-cay-say"] = new("Hạt hoặc trái cây sấy, có thể có muối hoặc đường.", "Có thể chứa các loại hạt.", "Đậy kín, nơi khô mát. Dùng hết trong vài ngày sau khi mở."),
        ["mut-keo"] = new("Đường, trái cây hoặc hạt, chất tạo ngọt.", "Có thể chứa đậu phộng, sữa và các loại hạt.", "Nơi khô ráo, tránh nóng để không bị chảy hoặc dính."),
        ["mat-ong-dac-san"] = new("Mật ong, muối hoặc gia vị tự nhiên (tùy loại).", "Không có chất gây dị ứng phổ biến. Mật ong không dùng cho trẻ dưới 12 tháng tuổi.", "Nhiệt độ phòng, đậy kín, tránh nước."),
        ["socola"] = new("Ca cao, đường, bơ ca cao; có thể có sữa hoặc hạt.", "Có thể chứa sữa, đậu nành và các loại hạt.", "Dưới 25 °C, tránh ánh nắng và nơi nóng."),
        ["suc-khoe"] = new("Yến sào hoặc dược liệu, đường phèn, mật ong (tùy loại).", "Người dị ứng với yến hoặc mật ong nên hỏi lại trước khi dùng.", "Nơi khô mát, đậy kín. Sản phẩm đã mở nên để ngăn mát."),
        ["dac-san-man"] = new("Thịt, cá hoặc hải sản, muối, đường, gia vị.", "Có thể chứa hải sản và đậu nành.", "Nơi khô mát. Sản phẩm tươi giữ ở ngăn mát."),
        ["nhu-yeu-pham-tet"] = new("Gạo, đậu, lạc, cá hoặc muối (tùy loại).", "Dầu lạc có thể gây dị ứng cho người dị ứng đậu phộng.", "Nơi khô mát, tránh ánh nắng."),
        ["do-uong"] = new("Trái cây, hạt hoặc ca cao, đường (tùy loại).", "Có thể chứa sữa hạt hoặc các loại hạt.", "Nơi mát. Đã mở nên để ngăn mát và dùng hết trong vài ngày."),
        // Not food: no ingredients line.
        ["qua-khong-an"] = new(null, "Nến, xà phòng và kem có thể chứa hương liệu. Hãy thử trên một vùng da nhỏ trước.", "Để nơi khô ráo, tránh ánh nắng và nhiệt độ cao."),
    };

    public static ItemInfoText For(string categoryId) => ByCategory.GetValueOrDefault(categoryId, Fallback);

    // The categories that have their own text (a test checks that every category of the seed is among them).
    public static IReadOnlyCollection<string> Categories => ByCategory.Keys.ToList();
}
