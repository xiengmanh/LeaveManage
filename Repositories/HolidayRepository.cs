using LeaveManage.Data;
using LeaveManage.Models;
using Microsoft.EntityFrameworkCore;

namespace LeaveManage.Repositories;

public class HolidayRepository : IHolidayRepository
{
    private readonly AppDbContext _context;

    public HolidayRepository(AppDbContext context) => _context = context;

    public Task<List<Holiday>> ListAsync(int? year)
    {
        var query = _context.Holidays.AsNoTracking().AsQueryable();
        if (year.HasValue) query = query.Where(h => h.Date.Year == year.Value);
        return query.OrderBy(h => h.Date).Take(500).ToListAsync();
    }

    public Task<Holiday?> GetByIdAsync(int id) =>
        _context.Holidays.FirstOrDefaultAsync(h => h.Id == id);

    public Task<bool> ExistsAsync(DateTime date) =>
        _context.Holidays.AnyAsync(h => h.Date == date.Date);

    public Task AddAsync(Holiday holiday) => _context.Holidays.AddAsync(holiday).AsTask();

    public void Remove(Holiday holiday) => _context.Holidays.Remove(holiday);

    public Task SaveChangesAsync() => _context.SaveChangesAsync();
}
