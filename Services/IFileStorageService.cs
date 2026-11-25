using LeaveManage.DTOs.Responses;
using Microsoft.AspNetCore.Http;

namespace LeaveManage.Services;

public interface IFileStorageService
{
    Task<(bool Success, string Message, FileUploadResponse? Data)> UploadImageAsync(
        IFormFile file,
        CancellationToken cancellationToken = default);
}
