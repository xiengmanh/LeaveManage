using LeaveManage.Data;
using LeaveManage.Models;
using Microsoft.EntityFrameworkCore;

namespace LeaveManage.Repositories;

public class LeaveAttachmentRepository : ILeaveAttachmentRepository
{
    private readonly AppDbContext _context;

    public LeaveAttachmentRepository(AppDbContext context)
    {
        _context = context;
    }

    public Task<int> CountForRequestAsync(int leaveRequestId) =>
        _context.LeaveAttachments.CountAsync(a => a.LeaveRequestId == leaveRequestId);

    public async Task AddAsync(LeaveAttachment attachment) =>
        await _context.LeaveAttachments.AddAsync(attachment);

    // Không đọc cột Content khi chỉ liệt kê
    public Task<List<LeaveAttachment>> ListForRequestAsync(int leaveRequestId) =>
        _context.LeaveAttachments.AsNoTracking()
            .Where(a => a.LeaveRequestId == leaveRequestId)
            .Select(a => new LeaveAttachment
            {
                Id = a.Id,
                LeaveRequestId = a.LeaveRequestId,
                FileName = a.FileName,
                ContentType = a.ContentType,
                Size = a.Size,
                UploadedAt = a.UploadedAt
            })
            .OrderBy(a => a.Id)
            .ToListAsync();

    public Task<LeaveAttachment?> GetAsync(int leaveRequestId, int attachmentId) =>
        _context.LeaveAttachments.AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == attachmentId && a.LeaveRequestId == leaveRequestId);

    public Task<LeaveAttachment?> GetTrackedAsync(int leaveRequestId, int attachmentId) =>
        _context.LeaveAttachments
            .FirstOrDefaultAsync(a => a.Id == attachmentId && a.LeaveRequestId == leaveRequestId);

    public void Remove(LeaveAttachment attachment) => _context.LeaveAttachments.Remove(attachment);

    public Task SaveChangesAsync() => _context.SaveChangesAsync();
}
