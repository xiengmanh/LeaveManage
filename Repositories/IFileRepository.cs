using LeaveManage.Models;

namespace LeaveManage.Repositories;

public interface IFileRepository
{
    Task<StoredFile> AddAsync(StoredFile file);
    Task<StoredFile?> GetByIdAsync(int id);
}
