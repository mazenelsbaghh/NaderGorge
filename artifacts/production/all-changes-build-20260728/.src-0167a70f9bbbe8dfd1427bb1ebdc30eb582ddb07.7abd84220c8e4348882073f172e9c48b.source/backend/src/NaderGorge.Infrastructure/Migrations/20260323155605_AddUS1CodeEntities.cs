using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NaderGorge.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUS1CodeEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Device_users_UserId",
                table: "Device");

            migrationBuilder.DropForeignKey(
                name: "FK_RefreshToken_users_UserId",
                table: "RefreshToken");

            migrationBuilder.DropForeignKey(
                name: "FK_StudentProfile_users_UserId",
                table: "StudentProfile");

            migrationBuilder.DropPrimaryKey(
                name: "PK_StudentProfile",
                table: "StudentProfile");

            migrationBuilder.DropPrimaryKey(
                name: "PK_RefreshToken",
                table: "RefreshToken");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Device",
                table: "Device");

            migrationBuilder.DropIndex(
                name: "IX_Device_UserId",
                table: "Device");

            migrationBuilder.RenameTable(
                name: "StudentProfile",
                newName: "student_profiles");

            migrationBuilder.RenameTable(
                name: "RefreshToken",
                newName: "refresh_tokens");

            migrationBuilder.RenameTable(
                name: "Device",
                newName: "devices");

            migrationBuilder.RenameIndex(
                name: "IX_StudentProfile_UserId",
                table: "student_profiles",
                newName: "IX_student_profiles_UserId");

            migrationBuilder.RenameIndex(
                name: "IX_RefreshToken_UserId",
                table: "refresh_tokens",
                newName: "IX_refresh_tokens_UserId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_student_profiles",
                table: "student_profiles",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_refresh_tokens",
                table: "refresh_tokens",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_devices",
                table: "devices",
                column: "Id");

            migrationBuilder.CreateTable(
                name: "code_groups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    TotalCodes = table.Column<int>(type: "integer", nullable: false),
                    PackageId = table.Column<Guid>(type: "uuid", nullable: true),
                    LessonId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_code_groups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_code_groups_users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "access_codes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CodeHash = table.Column<string>(type: "text", nullable: false),
                    CodePlaintext = table.Column<string>(type: "text", nullable: false),
                    CodeGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsConsumed = table.Column<bool>(type: "boolean", nullable: false),
                    ConsumedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ConsumedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_access_codes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_access_codes_code_groups_CodeGroupId",
                        column: x => x.CodeGroupId,
                        principalTable: "code_groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_access_codes_users_ConsumedByUserId",
                        column: x => x.ConsumedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "student_access_grants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    PackageId = table.Column<Guid>(type: "uuid", nullable: true),
                    LessonId = table.Column<Guid>(type: "uuid", nullable: true),
                    AccessCodeId = table.Column<Guid>(type: "uuid", nullable: false),
                    GrantedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_student_access_grants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_student_access_grants_access_codes_AccessCodeId",
                        column: x => x.AccessCodeId,
                        principalTable: "access_codes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_student_access_grants_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_refresh_tokens_Token",
                table: "refresh_tokens",
                column: "Token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_devices_UserId_DeviceFingerprint",
                table: "devices",
                columns: new[] { "UserId", "DeviceFingerprint" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_access_codes_CodeGroupId",
                table: "access_codes",
                column: "CodeGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_access_codes_CodeHash",
                table: "access_codes",
                column: "CodeHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_access_codes_ConsumedByUserId",
                table: "access_codes",
                column: "ConsumedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_code_groups_CreatedByUserId",
                table: "code_groups",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_student_access_grants_AccessCodeId",
                table: "student_access_grants",
                column: "AccessCodeId");

            migrationBuilder.CreateIndex(
                name: "IX_student_access_grants_UserId_PackageId",
                table: "student_access_grants",
                columns: new[] { "UserId", "PackageId" });

            migrationBuilder.AddForeignKey(
                name: "FK_devices_users_UserId",
                table: "devices",
                column: "UserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_refresh_tokens_users_UserId",
                table: "refresh_tokens",
                column: "UserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_student_profiles_users_UserId",
                table: "student_profiles",
                column: "UserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_devices_users_UserId",
                table: "devices");

            migrationBuilder.DropForeignKey(
                name: "FK_refresh_tokens_users_UserId",
                table: "refresh_tokens");

            migrationBuilder.DropForeignKey(
                name: "FK_student_profiles_users_UserId",
                table: "student_profiles");

            migrationBuilder.DropTable(
                name: "student_access_grants");

            migrationBuilder.DropTable(
                name: "access_codes");

            migrationBuilder.DropTable(
                name: "code_groups");

            migrationBuilder.DropPrimaryKey(
                name: "PK_student_profiles",
                table: "student_profiles");

            migrationBuilder.DropPrimaryKey(
                name: "PK_refresh_tokens",
                table: "refresh_tokens");

            migrationBuilder.DropIndex(
                name: "IX_refresh_tokens_Token",
                table: "refresh_tokens");

            migrationBuilder.DropPrimaryKey(
                name: "PK_devices",
                table: "devices");

            migrationBuilder.DropIndex(
                name: "IX_devices_UserId_DeviceFingerprint",
                table: "devices");

            migrationBuilder.RenameTable(
                name: "student_profiles",
                newName: "StudentProfile");

            migrationBuilder.RenameTable(
                name: "refresh_tokens",
                newName: "RefreshToken");

            migrationBuilder.RenameTable(
                name: "devices",
                newName: "Device");

            migrationBuilder.RenameIndex(
                name: "IX_student_profiles_UserId",
                table: "StudentProfile",
                newName: "IX_StudentProfile_UserId");

            migrationBuilder.RenameIndex(
                name: "IX_refresh_tokens_UserId",
                table: "RefreshToken",
                newName: "IX_RefreshToken_UserId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_StudentProfile",
                table: "StudentProfile",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_RefreshToken",
                table: "RefreshToken",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Device",
                table: "Device",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_Device_UserId",
                table: "Device",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Device_users_UserId",
                table: "Device",
                column: "UserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_RefreshToken_users_UserId",
                table: "RefreshToken",
                column: "UserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_StudentProfile_users_UserId",
                table: "StudentProfile",
                column: "UserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
