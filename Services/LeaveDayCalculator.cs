namespace LeaveManage.Services;

/// <summary>
/// Quy tắc đếm ngày nghỉ: chỉ đếm ngày làm việc thứ 2 – thứ 6, tính cả ngày bắt đầu và ngày kết thúc.
/// Thứ 7, Chủ nhật và các ngày lễ trong bảng Holidays không bị trừ phép.
/// </summary>
public static class LeaveDayCalculator
{
    public static int CountWorkingDays(DateTime start, DateTime end) =>
        CountWorkingDays(start, end, Array.Empty<DateTime>());

    public static int CountWorkingDays(DateTime start, DateTime end, IEnumerable<DateTime> holidays)
    {
        var holidaySet = holidays.Select(h => h.Date).ToHashSet();
        var days = 0;
        for (var day = start.Date; day <= end.Date; day = day.AddDays(1))
        {
            if (day.DayOfWeek != DayOfWeek.Saturday && day.DayOfWeek != DayOfWeek.Sunday && !holidaySet.Contains(day))
                days++;
        }
        return days;
    }
}
