using LeaveManage.Validation;

namespace LeaveManage.DTOs.Requests;

public class EvenNumberRequest
{
    [MustBeEven]
    public int Value { get; set; }
}
