using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NaderGorge.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTeacherFinanceCenter : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_teacher_financial_events_amounts",
                table: "teacher_financial_events");

            migrationBuilder.AddColumn<decimal>(
                name: "PlatformDiscountAmount",
                table: "teacher_financial_events",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "TeacherDiscountAmount",
                table: "teacher_financial_events",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "AgreementAllocationMode",
                table: "teacher_financial_allocations",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "AgreementId",
                table: "teacher_financial_allocations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "AgreementScopeId",
                table: "teacher_financial_allocations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AgreementScopeType",
                table: "teacher_financial_allocations",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DiscountBearer",
                table: "teacher_financial_allocations",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PriceBasis",
                table: "teacher_financial_allocations",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ReversedAmount",
                table: "teacher_financial_allocations",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "SettlementLineId",
                table: "teacher_financial_allocations",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "code_group_delivery_confirmations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CodeGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    Recipient = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    AttachmentUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ConfirmedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConfirmedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_code_group_delivery_confirmations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_code_group_delivery_confirmations_code_groups_CodeGroupId",
                        column: x => x.CodeGroupId,
                        principalTable: "code_groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "financial_invoices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    DocumentNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TeacherId = table.Column<Guid>(type: "uuid", nullable: true),
                    TeacherSettlementId = table.Column<Guid>(type: "uuid", nullable: true),
                    AttachmentUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    PaymentReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_financial_invoices", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "teacher_financial_agreements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TeacherId = table.Column<Guid>(type: "uuid", nullable: false),
                    ScopeType = table.Column<int>(type: "integer", nullable: false),
                    ScopeId = table.Column<Guid>(type: "uuid", nullable: true),
                    Trigger = table.Column<int>(type: "integer", nullable: false),
                    AllocationMode = table.Column<int>(type: "integer", nullable: false),
                    AllocationValue = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    PriceBasis = table.Column<int>(type: "integer", nullable: false),
                    EffectiveFrom = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    EffectiveTo = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_teacher_financial_agreements", x => x.Id);
                    table.CheckConstraint("CK_teacher_financial_agreements_dates", "\"EffectiveTo\" IS NULL OR \"EffectiveTo\" >= \"EffectiveFrom\"");
                    table.CheckConstraint("CK_teacher_financial_agreements_value", "\"AllocationValue\" >= 0 AND (\"AllocationMode\" <> 0 OR \"AllocationValue\" <= 100)");
                    table.ForeignKey(
                        name: "FK_teacher_financial_agreements_teacher_profiles_TeacherId",
                        column: x => x.TeacherId,
                        principalTable: "teacher_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "teacher_settlements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TeacherId = table.Column<Guid>(type: "uuid", nullable: false),
                    PeriodFrom = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    PeriodTo = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false, defaultValue: "EGP"),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    GrossDueAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    DebtDeductionAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    NetPayableAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReviewedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ApprovedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    PaidByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    PaidAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_teacher_settlements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_teacher_settlements_teacher_profiles_TeacherId",
                        column: x => x.TeacherId,
                        principalTable: "teacher_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "code_group_financial_terms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CodeGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    Trigger = table.Column<int>(type: "integer", nullable: false),
                    AgreementId = table.Column<Guid>(type: "uuid", nullable: true),
                    Recipient = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_code_group_financial_terms", x => x.Id);
                    table.ForeignKey(
                        name: "FK_code_group_financial_terms_code_groups_CodeGroupId",
                        column: x => x.CodeGroupId,
                        principalTable: "code_groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_code_group_financial_terms_teacher_financial_agreements_Agr~",
                        column: x => x.AgreementId,
                        principalTable: "teacher_financial_agreements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "teacher_settlement_lines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TeacherSettlementId = table.Column<Guid>(type: "uuid", nullable: false),
                    AllocationId = table.Column<Guid>(type: "uuid", nullable: true),
                    AdjustmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    DescriptionSnapshot = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_teacher_settlement_lines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_teacher_settlement_lines_teacher_financial_allocations_Allo~",
                        column: x => x.AllocationId,
                        principalTable: "teacher_financial_allocations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_teacher_settlement_lines_teacher_payout_adjustments_Adjustm~",
                        column: x => x.AdjustmentId,
                        principalTable: "teacher_payout_adjustments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_teacher_settlement_lines_teacher_settlements_TeacherSettlem~",
                        column: x => x.TeacherSettlementId,
                        principalTable: "teacher_settlements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "teacher_settlement_payments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TeacherSettlementId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    PaymentMethod = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    TransferReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    AttachmentUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    PaidByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    PaidAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_teacher_settlement_payments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_teacher_settlement_payments_teacher_settlements_TeacherSett~",
                        column: x => x.TeacherSettlementId,
                        principalTable: "teacher_settlements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_teacher_financial_events_amounts",
                table: "teacher_financial_events",
                sql: "\"DiscountAmount\" >= 0 AND \"PlatformDiscountAmount\" >= 0 AND \"TeacherDiscountAmount\" >= 0");

            migrationBuilder.CreateIndex(
                name: "IX_code_group_delivery_confirmations_CodeGroupId",
                table: "code_group_delivery_confirmations",
                column: "CodeGroupId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_code_group_delivery_confirmations_IdempotencyKey",
                table: "code_group_delivery_confirmations",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_code_group_financial_terms_AgreementId",
                table: "code_group_financial_terms",
                column: "AgreementId");

            migrationBuilder.CreateIndex(
                name: "IX_code_group_financial_terms_CodeGroupId",
                table: "code_group_financial_terms",
                column: "CodeGroupId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_financial_invoices_DocumentNumber",
                table: "financial_invoices",
                column: "DocumentNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_financial_invoices_TeacherId_Status",
                table: "financial_invoices",
                columns: new[] { "TeacherId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_teacher_financial_agreements_TeacherId_ScopeType_ScopeId_Tr~",
                table: "teacher_financial_agreements",
                columns: new[] { "TeacherId", "ScopeType", "ScopeId", "Trigger", "EffectiveFrom" });

            migrationBuilder.CreateIndex(
                name: "IX_teacher_settlement_lines_AdjustmentId",
                table: "teacher_settlement_lines",
                column: "AdjustmentId",
                unique: true,
                filter: "\"AdjustmentId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_teacher_settlement_lines_AllocationId",
                table: "teacher_settlement_lines",
                column: "AllocationId",
                unique: true,
                filter: "\"AllocationId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_teacher_settlement_lines_TeacherSettlementId",
                table: "teacher_settlement_lines",
                column: "TeacherSettlementId");

            migrationBuilder.CreateIndex(
                name: "IX_teacher_settlement_payments_TeacherSettlementId",
                table: "teacher_settlement_payments",
                column: "TeacherSettlementId");

            migrationBuilder.CreateIndex(
                name: "IX_teacher_settlements_TeacherId_Status_PeriodFrom_PeriodTo",
                table: "teacher_settlements",
                columns: new[] { "TeacherId", "Status", "PeriodFrom", "PeriodTo" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "code_group_delivery_confirmations");

            migrationBuilder.DropTable(
                name: "code_group_financial_terms");

            migrationBuilder.DropTable(
                name: "financial_invoices");

            migrationBuilder.DropTable(
                name: "teacher_settlement_lines");

            migrationBuilder.DropTable(
                name: "teacher_settlement_payments");

            migrationBuilder.DropTable(
                name: "teacher_financial_agreements");

            migrationBuilder.DropTable(
                name: "teacher_settlements");

            migrationBuilder.DropCheckConstraint(
                name: "CK_teacher_financial_events_amounts",
                table: "teacher_financial_events");

            migrationBuilder.DropColumn(
                name: "PlatformDiscountAmount",
                table: "teacher_financial_events");

            migrationBuilder.DropColumn(
                name: "TeacherDiscountAmount",
                table: "teacher_financial_events");

            migrationBuilder.DropColumn(
                name: "AgreementAllocationMode",
                table: "teacher_financial_allocations");

            migrationBuilder.DropColumn(
                name: "AgreementId",
                table: "teacher_financial_allocations");

            migrationBuilder.DropColumn(
                name: "AgreementScopeId",
                table: "teacher_financial_allocations");

            migrationBuilder.DropColumn(
                name: "AgreementScopeType",
                table: "teacher_financial_allocations");

            migrationBuilder.DropColumn(
                name: "DiscountBearer",
                table: "teacher_financial_allocations");

            migrationBuilder.DropColumn(
                name: "PriceBasis",
                table: "teacher_financial_allocations");

            migrationBuilder.DropColumn(
                name: "ReversedAmount",
                table: "teacher_financial_allocations");

            migrationBuilder.DropColumn(
                name: "SettlementLineId",
                table: "teacher_financial_allocations");

            migrationBuilder.AddCheckConstraint(
                name: "CK_teacher_financial_events_amounts",
                table: "teacher_financial_events",
                sql: "\"GrossAmount\" >= 0 AND \"DiscountAmount\" >= 0 AND \"PaidAmount\" >= 0 AND \"PromotionalAmount\" >= 0 AND \"PlatformShareAmount\" >= 0");
        }
    }
}
