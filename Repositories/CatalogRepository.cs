using LeaveManage.Data;
using LeaveManage.Models;
using Microsoft.EntityFrameworkCore;

namespace LeaveManage.Repositories;

public interface ICatalogRepository<T> where T : class, ICatalogEntity
{
    Task<List<T>> GetAllAsync(bool includeInactive);
    Task<T?> GetByIdAsync(int id);
    Task<T?> GetTrackedByIdAsync(int id);
    Task<bool> CodeExistsAsync(string code, int? exceptId);
    Task AddAsync(T entity);
    Task SaveChangesAsync();
}

/// <summary>Repository generic cho các bảng danh mục (Department, Position, LeaveType).</summary>
public class CatalogRepository<T> : ICatalogRepository<T> where T : class, ICatalogEntity
{
    private readonly AppDbContext _context;

    public CatalogRepository(AppDbContext context)
    {
        _context = context;
    }

    public Task<List<T>> GetAllAsync(bool includeInactive) =>
        _context.Set<T>().AsNoTracking()
            .Where(item => includeInactive || item.IsActive)
            .OrderBy(item => item.Code)
            .ToListAsync();

    public Task<T?> GetByIdAsync(int id) =>
        _context.Set<T>().AsNoTracking().FirstOrDefaultAsync(item => item.Id == id);

    public Task<T?> GetTrackedByIdAsync(int id) =>
        _context.Set<T>().FirstOrDefaultAsync(item => item.Id == id);

    public Task<bool> CodeExistsAsync(string code, int? exceptId) =>
        _context.Set<T>().AnyAsync(item => item.Code == code && (exceptId == null || item.Id != exceptId));

    public Task AddAsync(T entity) => _context.Set<T>().AddAsync(entity).AsTask();

    public Task SaveChangesAsync() => _context.SaveChangesAsync();
}
