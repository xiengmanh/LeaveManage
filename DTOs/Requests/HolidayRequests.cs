using System.ComponentModel.DataAnnotations;

namespace LeaveManage.DTOs.Requests;

public class HolidayUpsertDto
{
    [Required(ErrorMessage = "Ngày lễ là bắt buộc.")]
    public DateTime? Date { get; set; }

    [Required(ErrorMessage = "Tên ngày lễ là bắt buộc.")]
    [MaxLength(200, ErrorMessage = "Tên ngày lễ tối đa 200 ký tự.")]
    public string Name { get; set; } = string.Empty;
}
