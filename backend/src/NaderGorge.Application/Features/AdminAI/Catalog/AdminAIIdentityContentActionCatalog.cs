using NaderGorge.Application.Features.AdminAI.Interfaces;

namespace NaderGorge.Application.Features.AdminAI.Catalog;

/// <summary>
/// Reviewed action definitions for the first identity/content slice. These are
/// candidates until the entire Admin mutation inventory can be activated.
/// </summary>
public static class AdminAIIdentityContentActionCatalog
{
    public static IReadOnlyList<AdminAICapabilityDefinition> CreateCandidates() =>
        Array.AsReadOnly<AdminAICapabilityDefinition>(
        [
            Action("admin.identity.student-note.create", "AddStudentNoteCommand", """
                {"type":"object","properties":{"studentId":{"type":"string","format":"uuid"},"content":{"type":"string","minLength":1,"maxLength":4000},"isPinned":{"type":"boolean"}},"required":["studentId","content","isPinned"],"additionalProperties":false}
                """, ["students", "student-notes"]),
            Action("admin.content.subject.create", "CreateSubjectCommand", """
                {"type":"object","properties":{"name":{"type":"string","minLength":1,"maxLength":100},"description":{"type":"string","maxLength":2000}},"required":["name","description"],"additionalProperties":false}
                """, ["subjects", "content"]),
            Action("admin.content.subject.update", "UpdateSubjectCommand", """
                {"type":"object","properties":{"subjectId":{"type":"string","format":"uuid"},"name":{"type":"string","minLength":1,"maxLength":100},"description":{"type":"string","maxLength":2000}},"required":["subjectId","name","description"],"additionalProperties":false}
                """, ["subjects", "content"]),
            Action("admin.content.video-type.create", "CreateVideoTypeCommand", """
                {"type":"object","properties":{"name":{"type":"string","minLength":2,"maxLength":80},"sortOrder":{"type":"integer","minimum":0,"maximum":10000},"isActive":{"type":"boolean"}},"required":["name","sortOrder","isActive"],"additionalProperties":false}
                """, ["video-types", "content"]),
            Action("admin.content.video-type.update", "UpdateVideoTypeCommand", """
                {"type":"object","properties":{"videoTypeId":{"type":"string","format":"uuid"},"name":{"type":"string","minLength":2,"maxLength":80},"sortOrder":{"type":"integer","minimum":0,"maximum":10000}},"required":["videoTypeId","name","sortOrder"],"additionalProperties":false}
                """, ["video-types", "content"])
        ]);

    private static AdminAICapabilityDefinition Action(string key, string operation,
        string inputSchema, string[] refreshScopes) =>
        new(key, "candidate-1", "action", "ordinary", "ordinary", inputSchema,
            "{\"type\":\"object\"}", 0, 8_192, 5_000, operation, refreshScopes);
}
