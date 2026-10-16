using NaderGorge.Domain.Entities;

namespace NaderGorge.Application.Common;

public static class QuestionOptionOrdering
{
    public static IOrderedEnumerable<QuestionOption> InAuthoredOrder(IEnumerable<QuestionOption> options) => options
        .OrderBy(option => option.Order > 0 ? option.Order : int.MaxValue)
        .ThenBy(option => option.CreatedAt).ThenBy(option => option.Id);
}
