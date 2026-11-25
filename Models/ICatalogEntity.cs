namespace LeaveManage.Models;

/// <summary>
/// Danh mục có Id, Code duy nhất và cờ IsActive (Department, Position, LeaveType).
/// Cho phép dùng chung một CatalogRepository&lt;T&gt; generic.
/// </summary>
public interface ICatalogEntity
{
    int Id { get; set; }
    string Code { get; set; }
    bool IsActive { get; set; }
}
