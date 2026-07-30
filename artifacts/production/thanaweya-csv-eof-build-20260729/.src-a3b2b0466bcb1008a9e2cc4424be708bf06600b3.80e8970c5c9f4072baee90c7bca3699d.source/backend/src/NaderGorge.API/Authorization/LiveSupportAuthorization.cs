using Microsoft.AspNetCore.Authorization;

namespace NaderGorge.API.Authorization;

public static class LiveSupportAuthorization
{
    public const string ParticipantOrStaff = "LiveSupportParticipantOrStaff";
    public const string Staff = "LiveSupportStaff";

    public static void AddLiveSupportPolicies(this AuthorizationOptions options)
    {
        options.AddPolicy(ParticipantOrStaff, policy => policy.RequireAssertion(context =>
            context.User.Identity?.IsAuthenticated == true || context.Resource is HttpContext http && http.Request.Cookies.ContainsKey("massar_support_guest")));
        options.AddPolicy(Staff, policy => policy.RequireRole("Admin", "Assistant", "AssistantReviewer", "Staff"));
    }
}
