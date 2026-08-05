using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NaderGorge.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPlatformFinanceOperations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "finance_cost_centers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_finance_cost_centers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "finance_expense_categories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    AccountCode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_finance_expense_categories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "finance_vendors",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Phone = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_finance_vendors", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "platform_refunds",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OriginalSourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    OriginalSourceType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    TeacherId = table.Column<Guid>(type: "uuid", nullable: true),
                    PlatformAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TeacherAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Method = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    TreasuryAccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    PaymentReference = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    JournalEntryId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_platform_refunds", x => x.Id);
                    table.CheckConstraint("CK_platform_refunds_amounts", "\"PlatformAmount\" >= 0 AND \"TeacherAmount\" >= 0 AND (\"PlatformAmount\" + \"TeacherAmount\") > 0");
                    table.ForeignKey(
                        name: "FK_platform_refunds_financial_journal_entries_JournalEntryId",
                        column: x => x.JournalEntryId,
                        principalTable: "financial_journal_entries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_platform_refunds_treasury_accounts_TreasuryAccountId",
                        column: x => x.TreasuryAccountId,
                        principalTable: "treasury_accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "platform_expenses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentNumber = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    CostCenterId = table.Column<Guid>(type: "uuid", nullable: true),
                    VendorId = table.Column<Guid>(type: "uuid", nullable: true),
                    TreasuryAccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    AttachmentUrl = table.Column<string>(type: "text", nullable: true),
                    JournalEntryId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_platform_expenses", x => x.Id);
                    table.CheckConstraint("CK_platform_expenses_amount", "\"Amount\" > 0");
                    table.ForeignKey(
                        name: "FK_platform_expenses_finance_cost_centers_CostCenterId",
                        column: x => x.CostCenterId,
                        principalTable: "finance_cost_centers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_platform_expenses_finance_expense_categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "finance_expense_categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_platform_expenses_finance_vendors_VendorId",
                        column: x => x.VendorId,
                        principalTable: "finance_vendors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_platform_expenses_financial_journal_entries_JournalEntryId",
                        column: x => x.JournalEntryId,
                        principalTable: "financial_journal_entries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_platform_expenses_treasury_accounts_TreasuryAccountId",
                        column: x => x.TreasuryAccountId,
                        principalTable: "treasury_accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "platform_expense_payments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PlatformExpenseId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    PaidAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    TreasuryAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    PaymentReference = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    JournalEntryId = table.Column<Guid>(type: "uuid", nullable: false),
                    PaidByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_platform_expense_payments", x => x.Id);
                    table.CheckConstraint("CK_platform_expense_payments_amount", "\"Amount\" > 0");
                    table.ForeignKey(
                        name: "FK_platform_expense_payments_financial_journal_entries_Journal~",
                        column: x => x.JournalEntryId,
                        principalTable: "financial_journal_entries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_platform_expense_payments_platform_expenses_PlatformExpense~",
                        column: x => x.PlatformExpenseId,
                        principalTable: "platform_expenses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_platform_expense_payments_treasury_accounts_TreasuryAccount~",
                        column: x => x.TreasuryAccountId,
                        principalTable: "treasury_accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_finance_cost_centers_Name",
                table: "finance_cost_centers",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_finance_expense_categories_Name",
                table: "finance_expense_categories",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_finance_vendors_Name",
                table: "finance_vendors",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_platform_expense_payments_JournalEntryId",
                table: "platform_expense_payments",
                column: "JournalEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_platform_expense_payments_PlatformExpenseId",
                table: "platform_expense_payments",
                column: "PlatformExpenseId");

            migrationBuilder.CreateIndex(
                name: "IX_platform_expense_payments_TreasuryAccountId",
                table: "platform_expense_payments",
                column: "TreasuryAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_platform_expenses_CategoryId",
                table: "platform_expenses",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_platform_expenses_CostCenterId",
                table: "platform_expenses",
                column: "CostCenterId");

            migrationBuilder.CreateIndex(
                name: "IX_platform_expenses_DocumentNumber",
                table: "platform_expenses",
                column: "DocumentNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_platform_expenses_JournalEntryId",
                table: "platform_expenses",
                column: "JournalEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_platform_expenses_OccurredAt_Status",
                table: "platform_expenses",
                columns: new[] { "OccurredAt", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_platform_expenses_TreasuryAccountId",
                table: "platform_expenses",
                column: "TreasuryAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_platform_expenses_VendorId",
                table: "platform_expenses",
                column: "VendorId");

            migrationBuilder.CreateIndex(
                name: "IX_platform_refunds_JournalEntryId",
                table: "platform_refunds",
                column: "JournalEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_platform_refunds_OriginalSourceType_OriginalSourceId",
                table: "platform_refunds",
                columns: new[] { "OriginalSourceType", "OriginalSourceId" });

            migrationBuilder.CreateIndex(
                name: "IX_platform_refunds_Status",
                table: "platform_refunds",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_platform_refunds_TreasuryAccountId",
                table: "platform_refunds",
                column: "TreasuryAccountId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "platform_expense_payments");

            migrationBuilder.DropTable(
                name: "platform_refunds");

            migrationBuilder.DropTable(
                name: "platform_expenses");

            migrationBuilder.DropTable(
                name: "finance_cost_centers");

            migrationBuilder.DropTable(
                name: "finance_expense_categories");

            migrationBuilder.DropTable(
                name: "finance_vendors");
        }
    }
}
