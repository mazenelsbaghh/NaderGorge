using NaderGorge.Domain.Common;

namespace NaderGorge.Domain.Entities;

public class FindTheMistakeQuestion : QuestionBankItem
{
    public string BaseText { get; set; } = string.Empty;
    public int MistakeStartIndex { get; set; }
    public int MistakeEndIndex { get; set; }
}
