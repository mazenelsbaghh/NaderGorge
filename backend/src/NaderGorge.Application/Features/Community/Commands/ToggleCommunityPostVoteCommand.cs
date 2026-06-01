using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Community.Commands;

public record ToggleCommunityPostVoteResponse(
    Guid PostId,
    Guid? OptionIdSelected,
    Dictionary<Guid, int> OptionVoteCounts
);

public record ToggleCommunityPostVoteCommand(Guid PostId, Guid OptionId, Guid UserId)
    : IRequest<ApiResponse<ToggleCommunityPostVoteResponse>>;

public class ToggleCommunityPostVoteCommandHandler : IRequestHandler<ToggleCommunityPostVoteCommand, ApiResponse<ToggleCommunityPostVoteResponse>>
{
    private readonly IAppDbContext _db;

    public ToggleCommunityPostVoteCommandHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<ApiResponse<ToggleCommunityPostVoteResponse>> Handle(ToggleCommunityPostVoteCommand request, CancellationToken ct)
    {
        var post = await _db.CommunityPosts
            .Include(p => p.PollOptions)
            .FirstOrDefaultAsync(p => p.Id == request.PostId, ct);

        if (post == null || post.Status != CommunityPostStatus.Approved)
            return ApiResponse<ToggleCommunityPostVoteResponse>.Fail("Poll not found", new List<string> { "NOT_FOUND" });

        if (!post.IsPoll)
            return ApiResponse<ToggleCommunityPostVoteResponse>.Fail("This post is not a poll", new List<string> { "NOT_A_POLL" });

        var optionExists = post.PollOptions.Any(o => o.Id == request.OptionId);
        if (!optionExists)
            return ApiResponse<ToggleCommunityPostVoteResponse>.Fail("Option not found in this poll", new List<string> { "INVALID_OPTION" });

        // Check if user already voted
        var existingVote = await _db.CommunityPostPollVotes
            .FirstOrDefaultAsync(v => v.PostId == request.PostId && v.UserId == request.UserId, ct);

        Guid? selectedOptionId = null;

        if (existingVote == null)
        {
            // First time voting
            _db.CommunityPostPollVotes.Add(new CommunityPostPollVote
            {
                PostId = request.PostId,
                PollOptionId = request.OptionId,
                UserId = request.UserId
            });
            selectedOptionId = request.OptionId;
        }
        else if (existingVote.PollOptionId == request.OptionId)
        {
            // Clicking the same option again removes the vote
            _db.CommunityPostPollVotes.Remove(existingVote);
            selectedOptionId = null;
        }
        else
        {
            // Switching vote
            existingVote.PollOptionId = request.OptionId;
            selectedOptionId = request.OptionId;
        }

        await _db.SaveChangesAsync(ct);

        // Calculate all vote counts to return
        var allVotes = await _db.CommunityPostPollVotes
            .Where(v => v.PostId == request.PostId)
            .GroupBy(v => v.PollOptionId)
            .Select(g => new { OptionId = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var dict = post.PollOptions.ToDictionary(o => o.Id, o => 0);
        foreach (var v in allVotes)
        {
            dict[v.OptionId] = v.Count;
        }

        return ApiResponse<ToggleCommunityPostVoteResponse>.Ok(
            new ToggleCommunityPostVoteResponse(request.PostId, selectedOptionId, dict)
        );
    }
}
