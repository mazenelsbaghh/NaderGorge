using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using NaderGorge.Infrastructure.Migrations;

namespace NaderGorge.Application.Tests.LiveSupport;

public sealed class LiveSupportScheduleMigrationTests
{
    [Fact]
    public void Production20260904_OvernightSupportWindowIsAllowedByDatabaseConstraint()
    {
        var builder = new MigrationBuilder("Npgsql.EntityFrameworkCore.PostgreSQL");
        new TestableMigration().BuildUp(builder);

        var replacement = Assert.Single(builder.Operations.OfType<AddCheckConstraintOperation>());
        Assert.Equal("CK_live_support_schedule_time", replacement.Name);
        Assert.Equal("live_support_schedule_windows", replacement.Table);
        Assert.Equal("\"StartLocalTime\" <> \"EndLocalTime\"", replacement.Sql);
    }

    private sealed class TestableMigration : AllowOvernightLiveSupportSchedule
    {
        public void BuildUp(MigrationBuilder builder) => Up(builder);
    }
}
