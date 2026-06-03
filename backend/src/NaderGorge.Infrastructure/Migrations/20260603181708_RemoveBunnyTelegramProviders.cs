using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NaderGorge.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveBunnyTelegramProviders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE lesson_videos SET \"Provider\" = 'YouTube', \"ProviderVideoId\" = '2LfJcOt7Zhs' WHERE LOWER(\"Provider\") = 'bunny' OR LOWER(\"Provider\") = 'telegram';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
