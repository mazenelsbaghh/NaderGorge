using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using NaderGorge.Infrastructure.Data;

#nullable disable

namespace NaderGorge.Infrastructure.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(AppDbContext))]
    [Migration("20260408190000_AddPackageCodePageProfiles")]
    public partial class AddPackageCodePageProfiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "package_code_page_profiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PackageId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    HeroEyebrow = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    HeroTitle = table.Column<string>(type: "character varying(140)", maxLength: 140, nullable: true),
                    HeroDescription = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: true),
                    OfferTitle = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    OfferDescription = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: true),
                    ActivationTitle = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    ActivationDescription = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SupportTitle = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    SupportDescription = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    ThemeAccentKey = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    PublishedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_package_code_page_profiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_package_code_page_profiles_packages_PackageId",
                        column: x => x.PackageId,
                        principalTable: "packages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_package_code_page_profiles_users_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_package_code_page_profiles_PackageId",
                table: "package_code_page_profiles",
                column: "PackageId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_package_code_page_profiles_UpdatedByUserId",
                table: "package_code_page_profiles",
                column: "UpdatedByUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "package_code_page_profiles");
        }
    }
}
