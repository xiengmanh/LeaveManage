namespace LeaveManage.Options;

public class FileUploadOptions
{
    public const string SectionName = "FileUpload";

    public long MaxImageSize { get; set; } = 3 * 1024 * 1024;
    public string UploadFolder { get; set; } = "images";
    public string[] AllowedExtensions { get; set; } = [".jpg", ".jpeg", ".png"];
    public string[] AllowedContentTypes { get; set; } = ["image/jpeg", "image/png"];
}
