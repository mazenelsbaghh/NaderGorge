using NaderGorge.Domain.Enums;

namespace NaderGorge.Application.Features.Admin.Gifts.Models;

internal static class GiftValueSemantics
{
    internal static bool IsSuccessful(GiftRecipientStatus status) =>
        status != GiftRecipientStatus.Failed &&
        status != GiftRecipientStatus.AlreadyEntitled;

    internal static (decimal? OriginalValue, decimal? AvailableValue) Resolve(
        GiftTargetType targetType,
        decimal? amountPerRecipient,
        int successfulCount,
        decimal allocatedOriginal,
        decimal allocatedAvailable)
    {
        if (!amountPerRecipient.HasValue)
            return (null, null);

        return targetType == GiftTargetType.TeacherBalance
            ? (allocatedOriginal, allocatedAvailable)
            : (amountPerRecipient.Value * successfulCount, null);
    }
}
