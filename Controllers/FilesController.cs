using LeaveManage.Constants;
using LeaveManage.DTOs;
using LeaveManage.DTOs.Responses;
using LeaveManage.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LeaveManage.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = AuthorizationPolicies.CanManageEmployees)]
public class FilesController : ControllerBase
{
    private readonly IFileStorageService _fileStorageService;
    private readonly IFileService _fileService;

    public FilesController(
        IFileStorageService fileStorageService,
        IFileService fileService)
    {
        _fileStorageService = fileStorageService;
        _fileService = fileService;
    }

    /// <summary>Upload một ảnh JPG hoặc PNG, tối đa theo cấu hình FileUpload.</summary>
    [HttpPost("image")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<FileUploadResponse>> UploadImage(
        IFormFile file,
        CancellationToken cancellationToken)
    {
        var result = await _fileStorageService.UploadImageAsync(file, cancellationToken);
        return result.Success
            ? Ok(result.Data)
            : BadRequest(ErrorResponse.Fail(result.Message, "INVALID_FILE"));
    }

    /// <summary>Upload nhiều ảnh trong một request.</summary>
    [HttpPost("images")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadImages(
        List<IFormFile> files,
        CancellationToken cancellationToken)
    {
        if (files.Count == 0)
            return BadRequest(ErrorResponse.Fail("Bạn phải chọn ít nhất một file.", "NO_FILES"));

        var uploaded = new List<FileUploadResponse>();
        foreach (var file in files)
        {
            var result = await _fileStorageService.UploadImageAsync(file, cancellationToken);
            if (!result.Success)
                return BadRequest(ErrorResponse.Fail($"{file.FileName}: {result.Message}", "INVALID_FILE"));
            uploaded.Add(result.Data!);
        }

        return Ok(uploaded);
    }

    /// <summary>Lưu toàn bộ nội dung file vào bảng StoredFiles trong database.</summary>
    [HttpPost("database")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadToDatabase(
        [FromForm] FileUploadDbDto.UploadFileDto request)
    {
        try
        {
            return Ok(await _fileService.UploadFileAsync(request.File));
        }
        catch (ArgumentException exception)
        {
            return BadRequest(ErrorResponse.Fail(exception.Message, "INVALID_FILE"));
        }
    }

    /// <summary>Tải file đã lưu trong database theo ID.</summary>
    [HttpGet("database/{id:int}")]
    public async Task<IActionResult> DownloadFromDatabase(int id)
    {
        var file = await _fileService.GetFileForDownloadAsync(id);
        return file is null
            ? NotFound(ErrorResponse.Fail("Không tìm thấy file.", "FILE_NOT_FOUND"))
            : File(file.Content, file.ContentType, file.FileName);
    }
}
