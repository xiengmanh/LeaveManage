using LeaveManage.DTOs.Responses;
using LeaveManage.Exceptions;
using LeaveManage.Models;
using LeaveManage.Repositories;

namespace LeaveManage.Services;

public interface ILeaveAttachmentService
{
    Task<LeaveAttachmentResponse> UploadAsync(int requestId, IFormFile file, CallerContext caller);
    Task<List<LeaveAttachmentResponse>> ListAsync(int requestId, CallerContext caller);
    Task<LeaveAttachmentDownload> DownloadAsync(int requestId, int attachmentId, CallerContext caller);
    Task DeleteAsync(int requestId, int attachmentId, CallerContext caller);
}

/// <summary>
/// Tệp đính kèm đơn nghỉ. Chỉ chủ đơn được tải lên/xóa khi đơn còn Draft hoặc Pending.
/// Ai xem được đơn (chủ đơn, quản lý trực tiếp, Admin) thì được liệt kê và tải về.
/// Nội dung lưu trong database; loại tệp được kiểm tra theo chữ ký (magic bytes), không tin Content-Type client gửi.
/// </summary>
public class LeaveAttachmentService : ILeaveAttachmentService
{
    public const long MaxFileSize = 5 * 1024 * 1024;
    public const int MaxAttachmentsPerRequest = 5;

    private static readonly Dictionary<string, string> ContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = "application/pdf",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".png"] = "image/png",
    };

    private static readonly byte[] PdfSignature = [0x25, 0x50, 0x44, 0x46];                          // %PDF
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly byte[] JpegSignature = [0xFF, 0xD8, 0xFF];

    private readonly ILeaveRepository _leaveRepository;
    private readonly ILeaveAttachmentRepository _attachmentRepository;

    public LeaveAttachmentService(ILeaveRepository leaveRepository, ILeaveAttachmentRepository attachmentRepository)
    {
        _leaveRepository = leaveRepository;
        _attachmentRepository = attachmentRepository;
    }

    public async Task<LeaveAttachmentResponse> UploadAsync(int requestId, IFormFile file, CallerContext caller)
    {
        var employeeId = caller.RequireEmployeeId();
        var (content, contentType, fileName) = await ReadAndValidateAsync(file);

        return await _leaveRepository.ExecuteInTransactionAsync(async () =>
        {
            // Cùng giao thức với Submit/Approve/Cancel: khóa nhân viên trước rồi mới đọc đơn,
            // để trạng thái đọc được là trạng thái mới nhất.
            await _leaveRepository.LockEmployeeAsync(employeeId);

            var request = await _leaveRepository.GetRequestTrackedAsync(requestId);
            LeaveRequestService.EnsureOwner(request, caller);
            EnsureEditable(request!);

            if (await _attachmentRepository.CountForRequestAsync(requestId) >= MaxAttachmentsPerRequest)
                throw new BadRequestException(
                    $"Mỗi đơn chỉ được đính kèm tối đa {MaxAttachmentsPerRequest} tệp.", "TOO_MANY_ATTACHMENTS");

            var attachment = new LeaveAttachment
            {
                LeaveRequestId = requestId,
                FileName = fileName,
                ContentType = contentType,
                Size = content.Length,
                Content = content,
                UploadedAt = DateTime.UtcNow
            };
            await _attachmentRepository.AddAsync(attachment);
            await _attachmentRepository.SaveChangesAsync();
            return ToResponse(attachment);
        });
    }

    public async Task<List<LeaveAttachmentResponse>> ListAsync(int requestId, CallerContext caller)
    {
        await EnsureVisibleAsync(requestId, caller);
        var items = await _attachmentRepository.ListForRequestAsync(requestId);
        return items.Select(ToResponse).ToList();
    }

    public async Task<LeaveAttachmentDownload> DownloadAsync(int requestId, int attachmentId, CallerContext caller)
    {
        await EnsureVisibleAsync(requestId, caller);
        var attachment = await _attachmentRepository.GetAsync(requestId, attachmentId)
            ?? throw new NotFoundException("Không tìm thấy tệp đính kèm.", "ATTACHMENT_NOT_FOUND");

        return new LeaveAttachmentDownload
        {
            FileName = attachment.FileName,
            ContentType = attachment.ContentType,
            Content = attachment.Content
        };
    }

    public async Task DeleteAsync(int requestId, int attachmentId, CallerContext caller)
    {
        var employeeId = caller.RequireEmployeeId();

        await _leaveRepository.ExecuteInTransactionAsync(async () =>
        {
            await _leaveRepository.LockEmployeeAsync(employeeId);

            var request = await _leaveRepository.GetRequestTrackedAsync(requestId);
            LeaveRequestService.EnsureOwner(request, caller);
            EnsureEditable(request!);

            var attachment = await _attachmentRepository.GetTrackedAsync(requestId, attachmentId)
                ?? throw new NotFoundException("Không tìm thấy tệp đính kèm.", "ATTACHMENT_NOT_FOUND");

            _attachmentRepository.Remove(attachment);
            await _attachmentRepository.SaveChangesAsync();
            return true;
        });
    }

    private async Task EnsureVisibleAsync(int requestId, CallerContext caller)
    {
        var request = await _leaveRepository.GetRequestDetailAsync(requestId);
        LeaveRequestService.EnsureVisible(request, caller);
    }

    private static void EnsureEditable(LeaveRequest request)
    {
        if (request.Status is not (LeaveRequestStatus.Draft or LeaveRequestStatus.Pending))
            throw new ConflictException(
                "Chỉ thay đổi tệp đính kèm khi đơn ở trạng thái Nháp hoặc Chờ duyệt.", "INVALID_STATUS");
    }

    private static async Task<(byte[] Content, string ContentType, string FileName)> ReadAndValidateAsync(IFormFile file)
    {
        if (file is null || file.Length == 0)
            throw new BadRequestException("Bạn phải chọn một tệp.", "NO_FILE");

        if (file.Length > MaxFileSize)
            throw new BadRequestException("Tệp không được vượt quá 5MB.", "FILE_TOO_LARGE");

        var fileName = Path.GetFileName(file.FileName);
        var extension = Path.GetExtension(fileName);
        if (!ContentTypes.TryGetValue(extension, out var contentType))
            throw new BadRequestException("Chỉ chấp nhận tệp PDF, JPG hoặc PNG.", "INVALID_FILE_TYPE");

        using var stream = file.OpenReadStream();
        using var memory = new MemoryStream();
        await stream.CopyToAsync(memory);
        var content = memory.ToArray();

        if (!HasExpectedSignature(content, extension))
            throw new BadRequestException("Nội dung tệp không khớp với định dạng đuôi tệp.", "INVALID_FILE_CONTENT");

        return (content, contentType, fileName);
    }

    private static bool HasExpectedSignature(byte[] content, string extension)
    {
        var signature = extension.ToLowerInvariant() switch
        {
            ".pdf" => PdfSignature,
            ".png" => PngSignature,
            ".jpg" or ".jpeg" => JpegSignature,
            _ => null
        };
        return signature is not null
            && content.Length >= signature.Length
            && content.AsSpan(0, signature.Length).SequenceEqual(signature);
    }

    private static LeaveAttachmentResponse ToResponse(LeaveAttachment a) => new()
    {
        Id = a.Id,
        LeaveRequestId = a.LeaveRequestId,
        FileName = a.FileName,
        ContentType = a.ContentType,
        Size = a.Size,
        UploadedAt = a.UploadedAt
    };
}
