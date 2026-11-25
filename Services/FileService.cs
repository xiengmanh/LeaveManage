using LeaveManage.Models;
using LeaveManage.Repositories;
using static LeaveManage.DTOs.FileUploadDbDto;

namespace LeaveManage.Services;

public class FileService : IFileService
{
    private const long MaxDatabaseFileSize = 10 * 1024 * 1024;
    private readonly IFileRepository _fileRepository;

    public FileService(IFileRepository fileRepository)
    {
        _fileRepository = fileRepository;
    }

    public async Task<FileResponseDto> UploadFileAsync(IFormFile file)
    {
        if (file is null || file.Length == 0)
            throw new ArgumentException("File không hợp lệ.");

        if (file.Length > MaxDatabaseFileSize)
            throw new ArgumentException("File lưu vào database không được vượt quá 10MB.");

        using var memoryStream = new MemoryStream();
        await file.CopyToAsync(memoryStream);

        var storedFile = new StoredFile
        {
            FileName = Path.GetFileName(file.FileName),
            ContentType = string.IsNullOrWhiteSpace(file.ContentType)
                ? "application/octet-stream"
                : file.ContentType,
            Content = memoryStream.ToArray(),
            Length = file.Length
        };

        var createdFile = await _fileRepository.AddAsync(storedFile);
        return new FileResponseDto
        {
            Id = createdFile.Id,
            FileName = createdFile.FileName,
            ContentType = createdFile.ContentType,
            Size = createdFile.Length,
            UploadedAt = createdFile.UploadedAt
        };
    }

    public async Task<FileDownloadDto?> GetFileForDownloadAsync(int id)
    {
        var file = await _fileRepository.GetByIdAsync(id);
        if (file is null)
            return null;

        return new FileDownloadDto
        {
            Content = file.Content,
            ContentType = file.ContentType,
            FileName = file.FileName
        };
    }
}
