'use client';

import { useState } from 'react';
import { CheckCircle2 } from 'lucide-react';
import toast from 'react-hot-toast';

import { communityService, type CommunityPostPollOptionDto } from '@/services/community-service';

type CommunityPostPollProps = {
  postId: string;
  options: CommunityPostPollOptionDto[];
  initialUserVoteOptionId: string | null;
  totalVotes?: number;
};

export function CommunityPostPoll({ postId, options, initialUserVoteOptionId, totalVotes = 0 }: CommunityPostPollProps) {
  const [localOptions, setLocalOptions] = useState(options);
  const [userVoteId, setUserVoteId] = useState<string | null>(initialUserVoteOptionId);
  const [voting, setVoting] = useState(false);

  // Calculate total votes
  const currentTotalVotes = localOptions.reduce((acc, opt) => acc + opt.voteCount, 0);
  const displayTotal = totalVotes > currentTotalVotes ? totalVotes : currentTotalVotes;

  const handleVote = async (optionId: string) => {
    if (voting) return;
    setVoting(true);

    try {
      const response = await communityService.toggleCommunityPostVote(postId, optionId);
      const data = response.data?.data;
      if (data) {
        setUserVoteId(data.optionIdSelected);
        
        // Update local options counts based on API response
        setLocalOptions((prev) =>
          prev.map((opt) => ({
            ...opt,
            voteCount: data.optionVoteCounts[opt.id] ?? opt.voteCount,
          }))
        );
      }
    } catch {
      toast.error('حدث خطأ أثناء تسجيل التصويت.');
    } finally {
      setVoting(false);
    }
  };

  return (
    <div className="mt-4 space-y-2">
      {localOptions.map((option) => {
        const isSelected = userVoteId === option.id;
        const percentage = displayTotal > 0 ? Math.round((option.voteCount / displayTotal) * 100) : 0;

        return (
          <button
            key={option.id}
            onClick={() => handleVote(option.id)}
            disabled={voting}
            className={`relative flex w-full flex-col justify-center overflow-hidden rounded-lg border p-3 text-right transition-all
              ${
                isSelected
                  ? 'border-[#0866ff] bg-[#0866ff]/5'
                  : 'border-[var(--admin-border)] bg-[var(--admin-card-soft)] hover:bg-[var(--admin-hover)]'
              }`}
          >
            {/* Progress Bar Background */}
            <div
              className={`absolute bottom-0 left-0 top-0 opacity-20 transition-all duration-500 ease-out ${
                isSelected ? 'bg-[#0866ff]' : 'bg-gray-300 dark:bg-gray-600'
              }`}
              style={{ width: `${percentage}%` }}
            />

            <div className="relative z-10 flex w-full items-center justify-between gap-3">
              <div className="flex items-center gap-2">
                {isSelected ? (
                  <CheckCircle2 className="h-4 w-4 text-[#0866ff]" />
                ) : (
                  <div className="h-4 w-4 rounded-full border border-gray-400 dark:border-gray-500" />
                )}
                <span className={`text-[15px] font-bold ${isSelected ? 'text-[#0866ff]' : 'text-[var(--admin-text)]'}`}>
                  {option.text}
                </span>
              </div>
              <span className="text-sm font-bold text-gray-500 dark:text-gray-400">
                {percentage}%
              </span>
            </div>
            
            {userVoteId !== null && (
              <div className="relative z-10 mt-1 flex px-6 text-[12px] text-gray-500 dark:text-gray-400">
                {option.voteCount} صوت
              </div>
            )}
          </button>
        );
      })}

      <div className="pt-2 text-[13px] text-gray-500 dark:text-[var(--admin-muted)] text-right">
        إجمالي الأصوات: {displayTotal}
      </div>
    </div>
  );
}
