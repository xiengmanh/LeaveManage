using LeaveManage.Models;

namespace LeaveManage.Repositories;

/// <summary>Truy cập bảng Holidays.</summary>
public interface IHolidayRepository
{
    Task<List<Holiday>> ListAsync(int? year);
    Task<Holiday?> GetByIdAsync(int id);
    Task<bool> ExistsAsync(DateTime date);
    Task AddAsync(Holiday holiday);
    void Remove(Holiday holiday);
    Task SaveChangesAsync();
}
