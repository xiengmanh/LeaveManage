using LeaveManage.DTOs.Responses;
using LeaveManage.Options;
using Microsoft.Extensions.Options;

namespace LeaveManage.Services;

public class FileStorageService : IFileStorageService
{
    private readonly IWebHostEnvironment _environment;
    private readonly FileUploadOptions _options;

    public FileStorageService(
        IWebHostEnvironment environment,
        IOptions<FileUploadOptions> options)
    {
        _environment = environment;
        _options = options.Value;
    }

    public async Task<(bool, string, FileUploadResponse?)> UploadImageAsync(
        IFormFile file,
        CancellationToken cancellationToken = default)
    {
        if (file is null || file.Length == 0)
            return (false, "Bạn phải chọn file có dữ liệu.", null);

        if (file.Length > _options.MaxImageSize)
            return (false, $"File không được vượt quá {_options.MaxImageSize / 1024 / 1024}MB.", null);

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!_options.AllowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase) ||
            !_options.AllowedContentTypes.Contains(file.ContentType, StringComparer.OrdinalIgnoreCase))
        {
            return (false, "Chỉ hỗ trợ file ảnh JPG, JPEG hoặc PNG.", null);
        }

        if (!await HasValidImageSignatureAsync(file, extension, cancellationToken))
            return (false, "Nội dung file không đúng định dạng ảnh đã khai báo.", null);

        var webRoot = _environment.WebRootPath ??
            Path.Combine(_environment.ContentRootPath, "wwwroot");
        var uploadPath = Path.Combine(webRoot, _options.UploadFolder);
        Directory.CreateDirectory(uploadPath);

        var storedFileName = $"{Guid.NewGuid():N}{extension}";
        var fullPath = Path.Combine(uploadPath, storedFileName);
        await using var output = new FileStream(
            fullPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 81920,
            useAsync: true);
        await file.CopyToAsync(output, cancellationToken);

        return (true, "Upload ảnh thành công.", new FileUploadResponse
        {
            OriginalFileName = Path.GetFileName(file.FileName),
            StoredFileName = storedFileName,
            Url = $"/{_options.UploadFolder}/{storedFileName}",
            Size = file.Length
        });
    }

    private static async Task<bool> HasValidImageSignatureAsync(
        IFormFile file,
        string extension,
        CancellationToken cancellationToken)
    {
        var signature = new byte[8];
        await using var stream = file.OpenReadStream();
        var bytesRead = await stream.ReadAsync(signature, cancellationToken);

        if (extension == ".png")
        {
            byte[] png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
            return bytesRead >= png.Length && signature.SequenceEqual(png);
        }

        return bytesRead >= 3 &&
               signature[0] == 0xFF &&
               signature[1] == 0xD8 &&
               signature[2] == 0xFF;
    }
}
