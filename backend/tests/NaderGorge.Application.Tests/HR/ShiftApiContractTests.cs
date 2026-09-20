using System.Text.Json;
using System.Text.Json.Serialization;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using NaderGorge.API.Controllers;
using NaderGorge.Domain.Entities;

namespace NaderGorge.Application.Tests.HR;

public sealed class ShiftApiContractTests
{
    [Fact]
    public async Task WeeklyTemplate_UsesNumericWeekdaysEvenWithStringEnumSerialization()
    {
        await using var db = TestAppDbContextFactory.Create();
        var template = new ShiftTemplate { Code = "WEEK", Name = "Weekly", Segments =
            [new ShiftSegment { Sequence = 1, DayOfWeek = DayOfWeek.Saturday, StartsAt = TimeSpan.FromHours(10), EndsAt = TimeSpan.FromHours(16) }] };
        db.ShiftTemplates.Add(template);
        await db.SaveChangesAsync();
        using var services = new ServiceCollection().BuildServiceProvider();
        var controller = new HrShiftsController(db, new Mediator(services));
        var response = Assert.IsType<OkObjectResult>(await controller.GetTemplates(CancellationToken.None));
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        var payload = JsonSerializer.SerializeToElement(response.Value, options);
        Assert.Equal(6, payload[0].GetProperty("segments")[0].GetProperty("dayOfWeek").GetInt32());
        Assert.Equal("10:00:00", payload[0].GetProperty("segments")[0].GetProperty("startsAt").GetString());
    }
}
