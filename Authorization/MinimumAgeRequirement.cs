using LeaveManage.Constants;
using Microsoft.AspNetCore.Authorization;

namespace LeaveManage.Authorization;

public sealed class MinimumAgeRequirement : IAuthorizationRequirement
{
    public MinimumAgeRequirement(int minimumAge)
    {
        MinimumAge = minimumAge;
    }

    public int MinimumAge { get; }
}

public sealed class MinimumAgeHandler : AuthorizationHandler<MinimumAgeRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        MinimumAgeRequirement requirement)
    {
        var ageValue = context.User.FindFirst(CustomClaimTypes.Age)?.Value;
        if (int.TryParse(ageValue, out var age) && age >= requirement.MinimumAge)
            context.Succeed(requirement);

        return Task.CompletedTask;
    }
}
