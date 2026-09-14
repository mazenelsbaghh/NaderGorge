using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NaderGorge.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPlatformFinancialCenterFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "accounting_periods",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StartDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    EndDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ClosedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ClosedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CloseReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_accounting_periods", x => x.Id);
                    table.CheckConstraint("CK_accounting_period_dates", "\"StartDate\" <= \"EndDate\"");
                });

            migrationBuilder.CreateTable(
                name: "financial_accounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    NormalSide = table.Column<int>(type: "integer", nullable: false),
                    Role = table.Column<int>(type: "integer", nullable: false),
                    ParentId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_financial_accounts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "financial_journal_entries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SequenceNumber = table.Column<long>(type: "bigint", nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    PostedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    SourceType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    SourceId = table.Column<Guid>(type: "uuid", nullable: true),
                    PostingKind = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CorrelationId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ReversalOfId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_financial_journal_entries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "treasury_accounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    FinancialAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    DigitalWalletId = table.Column<Guid>(type: "uuid", nullable: true),
                    MaskedIdentifier = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_treasury_accounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_treasury_accounts_digital_wallets_DigitalWalletId",
                        column: x => x.DigitalWalletId,
                        principalTable: "digital_wallets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_treasury_accounts_financial_accounts_FinancialAccountId",
                        column: x => x.FinancialAccountId,
                        principalTable: "financial_accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "financial_journal_lines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    JournalEntryId = table.Column<Guid>(type: "uuid", nullable: false),
                    FinancialAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    Debit = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Credit = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: true),
                    TeacherId = table.Column<Guid>(type: "uuid", nullable: true),
                    TreasuryAccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    DimensionKey = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Memo = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_financial_journal_lines", x => x.Id);
                    table.CheckConstraint("CK_financial_journal_lines_amount", "\"Debit\" >= 0 AND \"Credit\" >= 0 AND ((\"Debit\" > 0 AND \"Credit\" = 0) OR (\"Credit\" > 0 AND \"Debit\" = 0))");
                    table.ForeignKey(
                        name: "FK_financial_journal_lines_financial_accounts_FinancialAccount~",
                        column: x => x.FinancialAccountId,
                        principalTable: "financial_accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_financial_journal_lines_financial_journal_entries_JournalEn~",
                        column: x => x.JournalEntryId,
                        principalTable: "financial_journal_entries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_accounting_periods_StartDate_EndDate",
                table: "accounting_periods",
                columns: new[] { "StartDate", "EndDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_financial_accounts_Code",
                table: "financial_accounts",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_financial_accounts_Type_IsActive",
                table: "financial_accounts",
                columns: new[] { "Type", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_financial_journal_entries_IdempotencyKey",
                table: "financial_journal_entries",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_financial_journal_entries_OccurredAt_Status",
                table: "financial_journal_entries",
                columns: new[] { "OccurredAt", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_financial_journal_entries_SequenceNumber",
                table: "financial_journal_entries",
                column: "SequenceNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_financial_journal_entries_SourceType_SourceId_PostingKind",
                table: "financial_journal_entries",
                columns: new[] { "SourceType", "SourceId", "PostingKind" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_financial_journal_lines_FinancialAccountId_JournalEntryId",
                table: "financial_journal_lines",
                columns: new[] { "FinancialAccountId", "JournalEntryId" });

            migrationBuilder.CreateIndex(
                name: "IX_financial_journal_lines_JournalEntryId",
                table: "financial_journal_lines",
                column: "JournalEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_financial_journal_lines_TeacherId_StudentId",
                table: "financial_journal_lines",
                columns: new[] { "TeacherId", "StudentId" });

            migrationBuilder.CreateIndex(
                name: "IX_financial_journal_lines_TreasuryAccountId",
                table: "financial_journal_lines",
                column: "TreasuryAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_treasury_accounts_DigitalWalletId",
                table: "treasury_accounts",
                column: "DigitalWalletId",
                unique: true,
                filter: "\"DigitalWalletId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_treasury_accounts_FinancialAccountId",
                table: "treasury_accounts",
                column: "FinancialAccountId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "accounting_periods");

            migrationBuilder.DropTable(
                name: "financial_journal_lines");

            migrationBuilder.DropTable(
                name: "treasury_accounts");

            migrationBuilder.DropTable(
                name: "financial_journal_entries");

            migrationBuilder.DropTable(
                name: "financial_accounts");
        }
    }
}
