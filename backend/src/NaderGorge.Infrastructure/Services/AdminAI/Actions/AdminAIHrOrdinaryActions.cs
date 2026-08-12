namespace NaderGorge.Infrastructure.Services.AdminAI.Actions;

// HR commands are intentionally not adapted merely because they are typed.
// Existing employee-profile commands combine metadata with salary, and many
// lifecycle commands affect employment/security state. They must be split or
// classified as strong-risk before they can be exposed as ordinary actions.
internal static class AdminAIHrOrdinaryActions
{
    internal const string Blocker = "No reviewed metadata-only authoritative HR command exists.";
}
