using System.ComponentModel.DataAnnotations;

namespace LeaveManage.Models;

/// <summary>Tệp đính kèm của đơn nghỉ (giấy khám bệnh, giấy tờ kèm theo). Nội dung lưu trong database.</summary>
public class LeaveAttachment
{
    [Key]
    public int Id { get; set; }

    public int LeaveRequestId { get; set; }
    public LeaveRequest? LeaveRequest { get; set; }

    [Required]
    [MaxLength(255)]
    public string FileName { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string ContentType { get; set; } = string.Empty;

    public long Size { get; set; }

    [Required]
    public byte[] Content { get; set; } = [];

    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
}
