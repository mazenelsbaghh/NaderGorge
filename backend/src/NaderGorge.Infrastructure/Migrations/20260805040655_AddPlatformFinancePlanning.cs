using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NaderGorge.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPlatformFinancePlanning : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "finance_budget_plans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    PeriodKind = table.Column<int>(type: "integer", nullable: false),
                    StartDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    EndDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_finance_budget_plans", x => x.Id);
                    table.CheckConstraint("CK_finance_budget_plan_dates", "\"StartDate\" <= \"EndDate\"");
                });

            migrationBuilder.CreateTable(
                name: "treasury_reconciliations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TreasuryAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    AsOfDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    SystemBalance = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    CountedOrStatementBalance = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    EvidenceNote = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    AdjustmentJournalEntryId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_treasury_reconciliations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_treasury_reconciliations_financial_journal_entries_Adjustme~",
                        column: x => x.AdjustmentJournalEntryId,
                        principalTable: "financial_journal_entries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_treasury_reconciliations_treasury_accounts_TreasuryAccountId",
                        column: x => x.TreasuryAccountId,
                        principalTable: "treasury_accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "treasury_transfers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceTreasuryAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    DestinationTreasuryAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Reference = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    JournalEntryId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_treasury_transfers", x => x.Id);
                    table.CheckConstraint("CK_treasury_transfers_amount", "\"Amount\" > 0");
                    table.ForeignKey(
                        name: "FK_treasury_transfers_financial_journal_entries_JournalEntryId",
                        column: x => x.JournalEntryId,
                        principalTable: "financial_journal_entries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_treasury_transfers_treasury_accounts_DestinationTreasuryAcc~",
                        column: x => x.DestinationTreasuryAccountId,
                        principalTable: "treasury_accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_treasury_transfers_treasury_accounts_SourceTreasuryAccountId",
                        column: x => x.SourceTreasuryAccountId,
                        principalTable: "treasury_accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "finance_budget_lines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FinanceBudgetPlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    FinancialAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    CostCenterId = table.Column<Guid>(type: "uuid", nullable: true),
                    TeacherId = table.Column<Guid>(type: "uuid", nullable: true),
                    PlannedAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_finance_budget_lines", x => x.Id);
                    table.CheckConstraint("CK_finance_budget_lines_amount", "\"PlannedAmount\" >= 0");
                    table.ForeignKey(
                        name: "FK_finance_budget_lines_finance_budget_plans_FinanceBudgetPlan~",
                        column: x => x.FinanceBudgetPlanId,
                        principalTable: "finance_budget_plans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_finance_budget_lines_financial_accounts_FinancialAccountId",
                        column: x => x.FinancialAccountId,
                        principalTable: "financial_accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_finance_budget_lines_FinanceBudgetPlanId_FinancialAccountId",
                table: "finance_budget_lines",
                columns: new[] { "FinanceBudgetPlanId", "FinancialAccountId" });

            migrationBuilder.CreateIndex(
                name: "IX_finance_budget_lines_FinancialAccountId",
                table: "finance_budget_lines",
                column: "FinancialAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_finance_budget_plans_StartDate_EndDate_Status",
                table: "finance_budget_plans",
                columns: new[] { "StartDate", "EndDate", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_treasury_reconciliations_AdjustmentJournalEntryId",
                table: "treasury_reconciliations",
                column: "AdjustmentJournalEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_treasury_reconciliations_TreasuryAccountId_AsOfDate",
                table: "treasury_reconciliations",
                columns: new[] { "TreasuryAccountId", "AsOfDate" });

            migrationBuilder.CreateIndex(
                name: "IX_treasury_transfers_DestinationTreasuryAccountId",
                table: "treasury_transfers",
                column: "DestinationTreasuryAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_treasury_transfers_JournalEntryId",
                table: "treasury_transfers",
                column: "JournalEntryId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_treasury_transfers_SourceTreasuryAccountId",
                table: "treasury_transfers",
                column: "SourceTreasuryAccountId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "finance_budget_lines");

            migrationBuilder.DropTable(
                name: "treasury_reconciliations");

            migrationBuilder.DropTable(
                name: "treasury_transfers");

            migrationBuilder.DropTable(
                name: "finance_budget_plans");
        }
    }
}
