using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LeaveManage.Models;

public class StoredFile
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    [MaxLength(255)]
    public required string FileName { get; set; }

    [Required]
    [MaxLength(100)]
    public required string ContentType { get; set; }

    [Required]
    public required byte[] Content { get; set; }

    public long Length { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
}
