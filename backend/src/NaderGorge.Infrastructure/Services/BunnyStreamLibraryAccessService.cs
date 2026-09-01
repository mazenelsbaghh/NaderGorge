using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Interfaces;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Infrastructure.Services;

public sealed class BunnyStreamLibraryAccessService : IBunnyStreamLibraryAccessService
{
    private readonly IAppDbContext _db;
    private readonly IBunnyStreamLibrarySecretProtector _protector;

    public BunnyStreamLibraryAccessService(
        IAppDbContext db,
        IBunnyStreamLibrarySecretProtector protector)
    {
        _db = db;
        _protector = protector;
    }

    public async Task<BunnyStreamLibraryAccessResult> ResolveAsync(
        Guid libraryId,
        bool requireActive,
        CancellationToken cancellationToken)
    {
        var library = await _db.BunnyStreamLibraries
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == libraryId, cancellationToken);
        return Resolve(library, requireActive);
    }

    public async Task<BunnyStreamLibraryAccessResult> ResolveByExternalIdAsync(
        long externalLibraryId,
        bool requireActive,
        CancellationToken cancellationToken)
    {
        var library = await _db.BunnyStreamLibraries
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.ExternalLibraryId == externalLibraryId, cancellationToken);
        return Resolve(library, requireActive);
    }

    private BunnyStreamLibraryAccessResult Resolve(BunnyStreamLibrary? library, bool requireActive)
    {
        if (library is null)
        {
            return BunnyStreamLibraryAccessResult.Fail(
                "BUNNY_LIBRARY_NOT_REGISTERED",
                "مكتبة Bunny المحددة غير مسجلة في إعدادات المنصة.");
        }

        if (requireActive && !library.IsActive)
        {
            return BunnyStreamLibraryAccessResult.Fail(
                "BUNNY_LIBRARY_INACTIVE",
                "مكتبة Bunny المحددة معطلة ولا يمكن استخدامها لفيديو جديد.");
        }

        if (library.ApiKeyCiphertext is not { Length: > 0 })
        {
            return BunnyStreamLibraryAccessResult.Fail(
                "BUNNY_API_KEY_REQUIRED",
                "أضف مفتاح API صالحًا لهذه المكتبة من الإعدادات أولًا.");
        }

        try
        {
            var apiKey = _protector.Unprotect(library.Id, library.ApiKeyCiphertext);
            return BunnyStreamLibraryAccessResult.Ok(new BunnyStreamLibraryAccess(
                library.Id,
                library.Name,
                library.ExternalLibraryId,
                apiKey,
                library.IsActive));
        }
        catch (Exception exception) when (exception is System.Security.Cryptography.CryptographicException or InvalidOperationException)
        {
            return BunnyStreamLibraryAccessResult.Fail(
                "BUNNY_API_KEY_UNAVAILABLE",
                "تعذر قراءة مفتاح مكتبة Bunny. أعد إدخال المفتاح من الإعدادات.");
        }
    }
}
