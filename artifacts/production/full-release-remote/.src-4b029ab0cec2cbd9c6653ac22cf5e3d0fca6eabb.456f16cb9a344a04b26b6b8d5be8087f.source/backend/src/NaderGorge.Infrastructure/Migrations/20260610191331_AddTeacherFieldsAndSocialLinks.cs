using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NaderGorge.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTeacherFieldsAndSocialLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AssistantPhoneNumbers",
                table: "teacher_profiles",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "teacher_profiles",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FacebookUrl",
                table: "teacher_profiles",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TelegramUrl",
                table: "teacher_profiles",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "YouTubeUrl",
                table: "teacher_profiles",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AssistantPhoneNumbers",
                table: "teacher_profiles");

            migrationBuilder.DropColumn(
                name: "Email",
                table: "teacher_profiles");

            migrationBuilder.DropColumn(
                name: "FacebookUrl",
                table: "teacher_profiles");

            migrationBuilder.DropColumn(
                name: "TelegramUrl",
                table: "teacher_profiles");

            migrationBuilder.DropColumn(
                name: "YouTubeUrl",
                table: "teacher_profiles");
        }
    }
}
