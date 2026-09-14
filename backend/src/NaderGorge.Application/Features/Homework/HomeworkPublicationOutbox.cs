using System.Text.Json;
using NaderGorge.Domain.Entities;
using HomeworkEntity = NaderGorge.Domain.Entities.Homework.Homework;

namespace NaderGorge.Application.Features.Homework;

internal static class HomeworkPublicationOutbox
{
    public static OutboxEvent Create(HomeworkEntity homework, Guid packageId) => new()
    {
        Type = "HomeworkPublished",
        TargetGroup = $"Package_{packageId}",
        PayloadJson = JsonSerializer.Serialize(new
        {
            lessonId = homework.LessonId,
            homeworkId = homework.Id,
            title = homework.Title,
            packageId
        })
    };
}
