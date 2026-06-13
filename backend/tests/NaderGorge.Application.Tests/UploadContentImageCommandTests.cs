using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.Admin.Commands;
using NaderGorge.Application.Interfaces;
using NaderGorge.Domain.Entities;
using NaderGorge.Infrastructure.Data;

namespace NaderGorge.Application.Tests;

public class UploadContentImageCommandTests
{
    [Theory]
    [InlineData(ContentImageType.Package)]
    [InlineData(ContentImageType.Term)]
    [InlineData(ContentImageType.Section)]
    public async Task ExistingContentImageUpload_StoresReturnedWebpUrl(ContentImageType contentType)
    {
        await using var db = TestAppDbContextFactory.Create();
        var entityId = Guid.NewGuid();
        AddContentEntity(db, contentType, entityId);
        await db.SaveChangesAsync();

        var expectedImageUrl = $"/uploads/content/{contentType.ToString().ToLowerInvariant()}/{Guid.NewGuid():N}.webp";
        var handler = new UploadContentImageCommandHandler(db, new StubContentImageStorage(expectedImageUrl));

        var response = await handler.Handle(
            new UploadContentImageCommand(entityId, contentType, [1, 2, 3]),
            CancellationToken.None);

        Assert.True(response.Success);
        Assert.Equal(expectedImageUrl, response.Data);
        Assert.Equal(expectedImageUrl, await ReadImageUrlAsync(db, contentType, entityId));
    }

    private static void AddContentEntity(AppDbContext db, ContentImageType contentType, Guid entityId)
    {
        switch (contentType)
        {
            case ContentImageType.Package:
                db.Packages.Add(new Package
                {
                    Id = entityId,
                    Name = "Package",
                    Description = "Description",
                    SubjectId = Guid.NewGuid(),
                    TeacherId = Guid.NewGuid(),
                    TargetGrade = "All"
                });
                break;
            case ContentImageType.Term:
                db.Terms.Add(new Term { Id = entityId, Title = "Term", PackageId = Guid.NewGuid() });
                break;
            case ContentImageType.Section:
                db.ContentSections.Add(new ContentSection { Id = entityId, Title = "Section", TermId = Guid.NewGuid() });
                break;
        }
    }

    private static async Task<string?> ReadImageUrlAsync(
        AppDbContext db,
        ContentImageType contentType,
        Guid entityId) => contentType switch
    {
        ContentImageType.Package => await db.Packages
            .Where(entity => entity.Id == entityId)
            .Select(entity => entity.ImageUrl)
            .SingleAsync(),
        ContentImageType.Term => await db.Terms
            .Where(entity => entity.Id == entityId)
            .Select(entity => entity.ImageUrl)
            .SingleAsync(),
        ContentImageType.Section => await db.ContentSections
            .Where(entity => entity.Id == entityId)
            .Select(entity => entity.ImageUrl)
            .SingleAsync(),
        _ => null
    };

    private sealed class StubContentImageStorage(string imageUrl) : IContentImageStorage
    {
        public Task<string> SaveAsWebpAsync(
            Stream imageStream,
            string contentFolder,
            CancellationToken cancellationToken) => Task.FromResult(imageUrl);
    }
}
