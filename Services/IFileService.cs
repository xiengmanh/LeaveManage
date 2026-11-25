using static LeaveManage.DTOs.FileUploadDbDto;

namespace LeaveManage.Services;

public interface IFileService
{
    Task<FileResponseDto> UploadFileAsync(IFormFile file);
    Task<FileDownloadDto?> GetFileForDownloadAsync(int id);
}
