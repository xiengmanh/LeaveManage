using LeaveManage.Exceptions;

namespace LeaveManage.Constants;

/// <summary>
/// Giới hạn phân trang dùng chung. Số trang quá lớn làm (page - 1) * pageSize tràn số int
/// (trước đây trả 500), nên chặn bằng [Range] trên DTO và kiểm tra lại khi tính offset.
/// </summary>
public static class PagingLimits
{
    public const int MaxPage = 100_000;

    /// <summary>Tính số bản ghi cần bỏ qua; ném 400 nếu vượt giới hạn xử lý.</summary>
    public static int Skip(int page, int pageSize)
    {
        if (page < 1 || page > MaxPage || pageSize < 1)
            throw new BadRequestException($"Số trang phải từ 1 đến {MaxPage}.", "INVALID_PAGE");
        return checked((page - 1) * pageSize);
    }
}
