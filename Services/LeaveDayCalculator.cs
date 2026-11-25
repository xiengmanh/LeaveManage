namespace LeaveManage.Services;

/// <summary>
/// Quy tắc đếm ngày nghỉ của dự án: chỉ đếm ngày làm việc thứ 2 – thứ 6,
/// tính cả ngày bắt đầu và ngày kết thúc. Thứ 7, Chủ nhật không bị trừ phép.
/// (Chưa trừ ngày lễ — ghi rõ trong tài liệu là giới hạn của phiên bản này.)
/// </summary>
public static class LeaveDayCalculator
{
    public static int CountWorkingDays(DateTime start, DateTime end)
    {
        var days = 0;
        for (var day = start.Date; day <= end.Date; day = day.AddDays(1))
        {
            if (day.DayOfWeek != DayOfWeek.Saturday && day.DayOfWeek != DayOfWeek.Sunday)
                days++;
        }
        return days;
    }
}
