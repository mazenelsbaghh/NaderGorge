using MediatR;
using NaderGorge.Application.Common;
using StackExchange.Redis;
using System.Text.Json;

namespace NaderGorge.Application.Features.Admin.Commands;

public record BulkGenerateCodesCommand(Guid PackageId, Guid? LessonId, int Count, int CodeLength, Guid AdminId) : IRequest<ApiResponse>;

public class BulkGenerateCodesCommandHandler : IRequestHandler<BulkGenerateCodesCommand, ApiResponse>
{
    private readonly IConnectionMultiplexer _redis;

    public BulkGenerateCodesCommandHandler(IConnectionMultiplexer redis)
    {
        _redis = redis;
    }

    public async Task<ApiResponse> Handle(BulkGenerateCodesCommand request, CancellationToken ct)
    {
        if (request.Count <= 0 || request.Count > 100_000)
            return ApiResponse.Fail("Count must be between 1 and 100,000");

        var db = _redis.GetDatabase();

        // For cross-platform BullMQ pushes without native libraries, 
        // the simplest integration point if utilizing BullMQ v3/v4 is publishing 
        // a basic JSON to a specific bridge standard, or if the worker simply listens to 
        // a Redis LIST (LPUSH / BRPOP implementation):
        
        var payload = new
        {
            packageId = request.PackageId,
            lessonId = request.LessonId,
            count = request.Count,
            codeLength = request.CodeLength,
            adminId = request.AdminId,
            timestamp = DateTime.UtcNow
        };

        var json = JsonSerializer.Serialize(payload);
        
        // This acts as a standard list queue push. The Node worker can read via standard Redis list, 
        // or if using BullMQ, it's better to implement the worker as pulling from this list 
        // or constructing a valid BullMQ meta hash. 
        // We'll LPUSH to 'code-generation-queue' to ensure the Node worker easily picks it up.
        await db.ListLeftPushAsync("code-generation-queue", json);

        return ApiResponse.Ok("Job successfully pushed to the queue for asynchronous generation.");
    }
}
