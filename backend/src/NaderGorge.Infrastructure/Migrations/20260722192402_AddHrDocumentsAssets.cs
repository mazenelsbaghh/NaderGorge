using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NaderGorge.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddHrDocumentsAssets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "hr_assets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    SerialNumber = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Value = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_hr_assets", x => x.Id);
                    table.CheckConstraint("CK_hr_asset_value", "\"Value\" >= 0");
                });

            migrationBuilder.CreateTable(
                name: "hr_employee_documents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Category = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    IssuedOn = table.Column<DateOnly>(type: "date", nullable: true),
                    ExpiresOn = table.Column<DateOnly>(type: "date", nullable: true),
                    RetainUntil = table.Column<DateOnly>(type: "date", nullable: true),
                    LegalHold = table.Column<bool>(type: "boolean", nullable: false),
                    IsArchived = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_hr_employee_documents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_hr_employee_documents_employee_profiles_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "employee_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "hr_asset_custodies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AssetId = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    AssignedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignedCondition = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    State = table.Column<int>(type: "integer", nullable: false),
                    ReturnedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ReturnCondition = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ClosedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ExceptionApprovedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ExceptionReason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_hr_asset_custodies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_hr_asset_custodies_employee_profiles_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "employee_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_hr_asset_custodies_hr_assets_AssetId",
                        column: x => x.AssetId,
                        principalTable: "hr_assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "hr_employee_document_versions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeDocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    AssetReference = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    ContentHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    MimeType = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    UploadedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_hr_employee_document_versions", x => x.Id);
                    table.CheckConstraint("CK_hr_document_version_size", "\"SizeBytes\" >= 0 AND \"Version\" > 0");
                    table.ForeignKey(
                        name: "FK_hr_employee_document_versions_hr_employee_documents_Employe~",
                        column: x => x.EmployeeDocumentId,
                        principalTable: "hr_employee_documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_hr_asset_custodies_AssetId",
                table: "hr_asset_custodies",
                column: "AssetId",
                unique: true,
                filter: "\"State\" = 0");

            migrationBuilder.CreateIndex(
                name: "IX_hr_asset_custodies_EmployeeId_State",
                table: "hr_asset_custodies",
                columns: new[] { "EmployeeId", "State" });

            migrationBuilder.CreateIndex(
                name: "IX_hr_assets_Code",
                table: "hr_assets",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_hr_assets_SerialNumber",
                table: "hr_assets",
                column: "SerialNumber",
                unique: true,
                filter: "\"SerialNumber\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_hr_employee_document_versions_EmployeeDocumentId_Version",
                table: "hr_employee_document_versions",
                columns: new[] { "EmployeeDocumentId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_hr_employee_documents_EmployeeId_Category_Name",
                table: "hr_employee_documents",
                columns: new[] { "EmployeeId", "Category", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_hr_employee_documents_ExpiresOn_IsArchived",
                table: "hr_employee_documents",
                columns: new[] { "ExpiresOn", "IsArchived" });

            migrationBuilder.CreateIndex(
                name: "IX_hr_employee_documents_RetainUntil_LegalHold_IsArchived",
                table: "hr_employee_documents",
                columns: new[] { "RetainUntil", "LegalHold", "IsArchived" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "hr_asset_custodies");

            migrationBuilder.DropTable(
                name: "hr_employee_document_versions");

            migrationBuilder.DropTable(
                name: "hr_assets");

            migrationBuilder.DropTable(
                name: "hr_employee_documents");
        }
    }
}
