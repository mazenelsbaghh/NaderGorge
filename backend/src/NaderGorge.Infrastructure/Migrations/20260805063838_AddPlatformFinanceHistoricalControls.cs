using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NaderGorge.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPlatformFinanceHistoricalControls : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "financial_migration_batches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    From = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    To = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CandidateCount = table.Column<int>(type: "integer", nullable: false),
                    PostedCount = table.Column<int>(type: "integer", nullable: false),
                    AlreadyPostedCount = table.Column<int>(type: "integer", nullable: false),
                    FailedCount = table.Column<int>(type: "integer", nullable: false),
                    SourceChecksum = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_financial_migration_batches", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "financial_projection_checkpoints",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    LastOccurredAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    LastSourceId = table.Column<Guid>(type: "uuid", nullable: true),
                    SourceCount = table.Column<long>(type: "bigint", nullable: false),
                    SourceAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    PostedAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Variance = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    LastReconciledAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_financial_projection_checkpoints", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "financial_migration_exceptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FinancialMigrationBatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    SourceId = table.Column<Guid>(type: "uuid", nullable: true),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    IsResolved = table.Column<bool>(type: "boolean", nullable: false),
                    ResolvedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ResolutionNote = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ResolvedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_financial_migration_exceptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_financial_migration_exceptions_financial_migration_batches_~",
                        column: x => x.FinancialMigrationBatchId,
                        principalTable: "financial_migration_batches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "financial_migration_items",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FinancialMigrationBatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    SourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceChecksum = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    JournalEntryId = table.Column<Guid>(type: "uuid", nullable: true),
                    ErrorMessage = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_financial_migration_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_financial_migration_items_financial_journal_entries_Journal~",
                        column: x => x.JournalEntryId,
                        principalTable: "financial_journal_entries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_financial_migration_items_financial_migration_batches_Finan~",
                        column: x => x.FinancialMigrationBatchId,
                        principalTable: "financial_migration_batches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_financial_migration_batches_From_To",
                table: "financial_migration_batches",
                columns: new[] { "From", "To" });

            migrationBuilder.CreateIndex(
                name: "IX_financial_migration_exceptions_FinancialMigrationBatchId_Is~",
                table: "financial_migration_exceptions",
                columns: new[] { "FinancialMigrationBatchId", "IsResolved" });

            migrationBuilder.CreateIndex(
                name: "IX_financial_migration_items_FinancialMigrationBatchId",
                table: "financial_migration_items",
                column: "FinancialMigrationBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_financial_migration_items_JournalEntryId",
                table: "financial_migration_items",
                column: "JournalEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_financial_migration_items_SourceType_SourceId",
                table: "financial_migration_items",
                columns: new[] { "SourceType", "SourceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_financial_projection_checkpoints_SourceType",
                table: "financial_projection_checkpoints",
                column: "SourceType",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "financial_migration_exceptions");

            migrationBuilder.DropTable(
                name: "financial_migration_items");

            migrationBuilder.DropTable(
                name: "financial_projection_checkpoints");

            migrationBuilder.DropTable(
                name: "financial_migration_batches");
        }
    }
}
