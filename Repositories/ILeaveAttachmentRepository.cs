using LeaveManage.Models;

namespace LeaveManage.Repositories;

/// <summary>Truy cập dữ liệu tệp đính kèm của đơn nghỉ.</summary>
public interface ILeaveAttachmentRepository
{
    Task<int> CountForRequestAsync(int leaveRequestId);
    Task AddAsync(LeaveAttachment attachment);
    Task<List<LeaveAttachment>> ListForRequestAsync(int leaveRequestId);
    Task<LeaveAttachment?> GetAsync(int leaveRequestId, int attachmentId);
    Task<LeaveAttachment?> GetTrackedAsync(int leaveRequestId, int attachmentId);
    void Remove(LeaveAttachment attachment);
    Task SaveChangesAsync();
}
