namespace Peekbox.Web.Security.Admin;

// The three roles of the admin (F69, issue #105). The names are what the database holds; the labels are what the account
// chip shows. A person has exactly one. Which screen needs which role is in AdminPolicies.
public static class AdminRoles
{
    public const string Owner = "Owner";
    public const string Manager = "Manager";
    public const string Staff = "Staff";

    // In the order of the roles table's ids (1, 2, 3), from the most to the least allowed.
    public static readonly IReadOnlyList<string> All = [Owner, Manager, Staff];

    public static string Label(string role) => role switch
    {
        Owner => "Chủ cửa hàng",
        Manager => "Quản lý",
        Staff => "Nhân viên",
        _ => role,
    };

    // The id of each role row, fixed so the migration can seed them and a test can name them.
    public static int Id(string role) => role switch
    {
        Owner => 1,
        Manager => 2,
        Staff => 3,
        _ => throw new ArgumentException($"{role} is not an admin role.", nameof(role)),
    };
}
