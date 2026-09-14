using NaderGorge.Application.Features.Admin.Queries;
using NaderGorge.Domain.Entities;

namespace NaderGorge.Application.Tests;

public class CodeGroupLookupTests
{
    [Theory]
    [InlineData("EXACT-CODE")]
    [InlineData("12345")]
    public async Task CodeOrSerial_ReturnsItsGroupWithoutExposingOtherGroups(string search)
    {
        await using var db = TestAppDbContextFactory.Create();
        var group = new CodeGroup { Name = "Target" };
        group.AccessCodes.Add(new AccessCode { CodePlaintext = "EXACT-CODE", CodeHash = "test-hash", SerialNumber = 12345 });
        db.CodeGroups.AddRange(group, new CodeGroup { Name = "Other" });
        await db.SaveChangesAsync();
        var response = await new ListCodeGroupsQueryHandler(db).Handle(new(Search: search), default);
        Assert.True(response.Success);
        Assert.Equal(group.Id, Assert.Single(response.Data!).Id);
    }
}
