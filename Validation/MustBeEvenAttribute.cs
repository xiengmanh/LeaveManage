using System.ComponentModel.DataAnnotations;

namespace LeaveManage.Validation;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class MustBeEvenAttribute : ValidationAttribute
{
    public MustBeEvenAttribute()
    {
        ErrorMessage = "Giá trị phải là số chẵn.";
    }

    public override bool IsValid(object? value)
    {
        return value is null || value is int number && number % 2 == 0;
    }
}
