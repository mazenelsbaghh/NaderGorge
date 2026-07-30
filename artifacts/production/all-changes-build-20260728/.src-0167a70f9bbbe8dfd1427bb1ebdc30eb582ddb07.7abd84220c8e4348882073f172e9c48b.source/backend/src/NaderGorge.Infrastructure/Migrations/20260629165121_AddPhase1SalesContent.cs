using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NaderGorge.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPhase1SalesContent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PublicExamProductId",
                table: "student_access_grants",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "discount_stacking_policies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    NormalizedName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Mode = table.Column<int>(type: "integer", nullable: false),
                    MaxDiscountPercentage = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    MaxDiscountAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    PriorityJson = table.Column<string>(type: "jsonb", nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_discount_stacking_policies", x => x.Id);
                    table.CheckConstraint("CK_discount_policy_amount", "\"MaxDiscountAmount\" IS NULL OR \"MaxDiscountAmount\" > 0");
                    table.CheckConstraint("CK_discount_policy_percentage", "\"MaxDiscountPercentage\" IS NULL OR (\"MaxDiscountPercentage\" >= 0 AND \"MaxDiscountPercentage\" <= 100)");
                    table.ForeignKey(
                        name: "FK_discount_stacking_policies_users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "printable_code_templates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    WidthMm = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    HeightMm = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    BackgroundColor = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    BackgroundImageUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    LayoutJson = table.Column<string>(type: "jsonb", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_printable_code_templates", x => x.Id);
                    table.CheckConstraint("CK_printable_templates_size", "\"WidthMm\" > 0 AND \"HeightMm\" > 0");
                    table.ForeignKey(
                        name: "FK_printable_code_templates_users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "public_exam_products",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ExamId = table.Column<Guid>(type: "uuid", nullable: false),
                    Slug = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    IsPublished = table.Column<bool>(type: "boolean", nullable: false),
                    IsPaid = table.Column<bool>(type: "boolean", nullable: false),
                    Price = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TeacherId = table.Column<Guid>(type: "uuid", nullable: true),
                    SubjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    GradeLevel = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    IsPlatformWide = table.Column<bool>(type: "boolean", nullable: false),
                    AvailableFrom = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    AvailableUntil = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    DisabledAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    DisabledByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    DisableReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_public_exam_products", x => x.Id);
                    table.CheckConstraint("CK_public_exam_price", "(\"IsPaid\" = FALSE AND \"Price\" = 0) OR (\"IsPaid\" = TRUE AND \"Price\" > 0)");
                    table.ForeignKey(
                        name: "FK_public_exam_products_exams_ExamId",
                        column: x => x.ExamId,
                        principalTable: "exams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_public_exam_products_subjects_SubjectId",
                        column: x => x.SubjectId,
                        principalTable: "subjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_public_exam_products_teacher_profiles_TeacherId",
                        column: x => x.TeacherId,
                        principalTable: "teacher_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_public_exam_products_users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_public_exam_products_users_DisabledByUserId",
                        column: x => x.DisabledByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "sales_financial_effects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PurchaseOperationId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetType = table.Column<int>(type: "integer", nullable: false),
                    TargetId = table.Column<Guid>(type: "uuid", nullable: false),
                    GrossAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    CouponDiscountAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    PrintableCodeDiscountAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    PromotionalAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    PaidAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TeacherId = table.Column<Guid>(type: "uuid", nullable: true),
                    TeacherShareImpact = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    PlatformShareImpact = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    DetailsJson = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sales_financial_effects", x => x.Id);
                    table.CheckConstraint("CK_sales_financial_effect_amounts", "\"GrossAmount\" >= 0 AND \"CouponDiscountAmount\" >= 0 AND \"PrintableCodeDiscountAmount\" >= 0 AND \"PromotionalAmount\" >= 0 AND \"PaidAmount\" >= 0 AND \"TeacherShareImpact\" >= 0 AND \"PlatformShareImpact\" >= 0");
                    table.CheckConstraint("CK_sales_financial_effect_conservation", "\"GrossAmount\" = \"CouponDiscountAmount\" + \"PrintableCodeDiscountAmount\" + \"PromotionalAmount\" + \"PaidAmount\"");
                    table.ForeignKey(
                        name: "FK_sales_financial_effects_teacher_profiles_TeacherId",
                        column: x => x.TeacherId,
                        principalTable: "teacher_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sales_financial_effects_users_StudentId",
                        column: x => x.StudentId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sales_rules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetType = table.Column<int>(type: "integer", nullable: false),
                    TargetId = table.Column<Guid>(type: "uuid", nullable: true),
                    TeacherId = table.Column<Guid>(type: "uuid", nullable: true),
                    SubjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    GradeLevel = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    VideoTypeId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sales_rules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_sales_rules_subjects_SubjectId",
                        column: x => x.SubjectId,
                        principalTable: "subjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sales_rules_teacher_profiles_TeacherId",
                        column: x => x.TeacherId,
                        principalTable: "teacher_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sales_rules_users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sales_rules_video_types_VideoTypeId",
                        column: x => x.VideoTypeId,
                        principalTable: "video_types",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sales_coupons",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    NormalizedCode = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    DiscountType = table.Column<int>(type: "integer", nullable: false),
                    DiscountValue = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TargetType = table.Column<int>(type: "integer", nullable: false),
                    TargetId = table.Column<Guid>(type: "uuid", nullable: true),
                    OwnerType = table.Column<int>(type: "integer", nullable: false),
                    TeacherId = table.Column<Guid>(type: "uuid", nullable: true),
                    StackingPolicyId = table.Column<Guid>(type: "uuid", nullable: true),
                    StartsAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    GlobalUsageLimit = table.Column<int>(type: "integer", nullable: true),
                    PerStudentUsageLimit = table.Column<int>(type: "integer", nullable: true),
                    UsedCount = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    DisableReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sales_coupons", x => x.Id);
                    table.CheckConstraint("CK_sales_coupons_discount_value", "\"DiscountValue\" > 0 AND (\"DiscountType\" <> 0 OR \"DiscountValue\" <= 100)");
                    table.CheckConstraint("CK_sales_coupons_limits", "(\"GlobalUsageLimit\" IS NULL OR \"GlobalUsageLimit\" > 0) AND (\"PerStudentUsageLimit\" IS NULL OR \"PerStudentUsageLimit\" > 0) AND \"UsedCount\" >= 0");
                    table.ForeignKey(
                        name: "FK_sales_coupons_discount_stacking_policies_StackingPolicyId",
                        column: x => x.StackingPolicyId,
                        principalTable: "discount_stacking_policies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sales_coupons_teacher_profiles_TeacherId",
                        column: x => x.TeacherId,
                        principalTable: "teacher_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sales_coupons_users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "printable_code_batches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Behavior = table.Column<int>(type: "integer", nullable: false),
                    DiscountType = table.Column<int>(type: "integer", nullable: true),
                    DiscountValue = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    CreditAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    TargetType = table.Column<int>(type: "integer", nullable: false),
                    TargetId = table.Column<Guid>(type: "uuid", nullable: true),
                    OwnerType = table.Column<int>(type: "integer", nullable: false),
                    TeacherId = table.Column<Guid>(type: "uuid", nullable: true),
                    TemplateId = table.Column<Guid>(type: "uuid", nullable: true),
                    StackingPolicyId = table.Column<Guid>(type: "uuid", nullable: true),
                    TotalCodes = table.Column<int>(type: "integer", nullable: false),
                    UsedCount = table.Column<int>(type: "integer", nullable: false),
                    StartsAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    DisableReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_printable_code_batches", x => x.Id);
                    table.CheckConstraint("CK_printable_batches_total", "\"TotalCodes\" > 0 AND \"TotalCodes\" <= 10000 AND \"UsedCount\" >= 0");
                    table.CheckConstraint("CK_printable_batches_values", "(\"Behavior\" = 0 AND \"DiscountType\" IS NOT NULL AND \"DiscountValue\" > 0) OR (\"Behavior\" = 1) OR (\"Behavior\" = 2 AND \"CreditAmount\" > 0)");
                    table.ForeignKey(
                        name: "FK_printable_code_batches_discount_stacking_policies_StackingP~",
                        column: x => x.StackingPolicyId,
                        principalTable: "discount_stacking_policies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_printable_code_batches_printable_code_templates_TemplateId",
                        column: x => x.TemplateId,
                        principalTable: "printable_code_templates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_printable_code_batches_teacher_profiles_TeacherId",
                        column: x => x.TeacherId,
                        principalTable: "teacher_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_printable_code_batches_users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sales_coupon_usages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CouponId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    PurchaseOperationId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetType = table.Column<int>(type: "integer", nullable: false),
                    TargetId = table.Column<Guid>(type: "uuid", nullable: false),
                    GrossAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sales_coupon_usages", x => x.Id);
                    table.CheckConstraint("CK_sales_coupon_usage_amounts", "\"GrossAmount\" >= 0 AND \"DiscountAmount\" > 0");
                    table.ForeignKey(
                        name: "FK_sales_coupon_usages_sales_coupons_CouponId",
                        column: x => x.CouponId,
                        principalTable: "sales_coupons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sales_coupon_usages_users_StudentId",
                        column: x => x.StudentId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "printable_sales_codes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    CodeHash = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    CodePlaintext = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    SerialNumber = table.Column<long>(type: "bigint", nullable: false),
                    QrPayload = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    UsedCount = table.Column<int>(type: "integer", nullable: false),
                    UsageLimit = table.Column<int>(type: "integer", nullable: false),
                    ConsumedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ConsumedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_printable_sales_codes", x => x.Id);
                    table.CheckConstraint("CK_printable_sales_codes_usage", "\"UsageLimit\" > 0 AND \"UsedCount\" >= 0 AND \"UsedCount\" <= \"UsageLimit\"");
                    table.ForeignKey(
                        name: "FK_printable_sales_codes_printable_code_batches_BatchId",
                        column: x => x.BatchId,
                        principalTable: "printable_code_batches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_printable_sales_codes_users_ConsumedByUserId",
                        column: x => x.ConsumedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "printable_code_redemptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PrintableCodeId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    PurchaseOperationId = table.Column<Guid>(type: "uuid", nullable: true),
                    TargetType = table.Column<int>(type: "integer", nullable: false),
                    TargetId = table.Column<Guid>(type: "uuid", nullable: false),
                    AppliedAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_printable_code_redemptions", x => x.Id);
                    table.CheckConstraint("CK_printable_redemption_amount", "\"AppliedAmount\" >= 0");
                    table.ForeignKey(
                        name: "FK_printable_code_redemptions_printable_sales_codes_PrintableC~",
                        column: x => x.PrintableCodeId,
                        principalTable: "printable_sales_codes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_printable_code_redemptions_users_StudentId",
                        column: x => x.StudentId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_student_access_grants_PublicExamProductId",
                table: "student_access_grants",
                column: "PublicExamProductId");

            migrationBuilder.CreateIndex(
                name: "IX_discount_stacking_policies_CreatedByUserId",
                table: "discount_stacking_policies",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_discount_stacking_policies_IsDefault",
                table: "discount_stacking_policies",
                column: "IsDefault");

            migrationBuilder.CreateIndex(
                name: "IX_discount_stacking_policies_NormalizedName",
                table: "discount_stacking_policies",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_printable_code_batches_CreatedByUserId",
                table: "printable_code_batches",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_printable_code_batches_StackingPolicyId",
                table: "printable_code_batches",
                column: "StackingPolicyId");

            migrationBuilder.CreateIndex(
                name: "IX_printable_code_batches_TargetType_TargetId_Status",
                table: "printable_code_batches",
                columns: new[] { "TargetType", "TargetId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_printable_code_batches_TeacherId",
                table: "printable_code_batches",
                column: "TeacherId");

            migrationBuilder.CreateIndex(
                name: "IX_printable_code_batches_TemplateId",
                table: "printable_code_batches",
                column: "TemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_printable_code_redemptions_PrintableCodeId_RequestId",
                table: "printable_code_redemptions",
                columns: new[] { "PrintableCodeId", "RequestId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_printable_code_redemptions_StudentId",
                table: "printable_code_redemptions",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_printable_code_templates_CreatedByUserId",
                table: "printable_code_templates",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_printable_sales_codes_BatchId",
                table: "printable_sales_codes",
                column: "BatchId");

            migrationBuilder.CreateIndex(
                name: "IX_printable_sales_codes_CodeHash",
                table: "printable_sales_codes",
                column: "CodeHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_printable_sales_codes_ConsumedByUserId",
                table: "printable_sales_codes",
                column: "ConsumedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_printable_sales_codes_SerialNumber",
                table: "printable_sales_codes",
                column: "SerialNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_public_exam_products_CreatedByUserId",
                table: "public_exam_products",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_public_exam_products_DisabledByUserId",
                table: "public_exam_products",
                column: "DisabledByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_public_exam_products_ExamId",
                table: "public_exam_products",
                column: "ExamId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_public_exam_products_IsPublished_DisabledAt_AvailableFrom_A~",
                table: "public_exam_products",
                columns: new[] { "IsPublished", "DisabledAt", "AvailableFrom", "AvailableUntil" });

            migrationBuilder.CreateIndex(
                name: "IX_public_exam_products_Slug",
                table: "public_exam_products",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_public_exam_products_SubjectId",
                table: "public_exam_products",
                column: "SubjectId");

            migrationBuilder.CreateIndex(
                name: "IX_public_exam_products_TeacherId",
                table: "public_exam_products",
                column: "TeacherId");

            migrationBuilder.CreateIndex(
                name: "IX_sales_coupon_usages_CouponId_PurchaseOperationId",
                table: "sales_coupon_usages",
                columns: new[] { "CouponId", "PurchaseOperationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sales_coupon_usages_CouponId_StudentId_PurchaseOperationId",
                table: "sales_coupon_usages",
                columns: new[] { "CouponId", "StudentId", "PurchaseOperationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sales_coupon_usages_StudentId",
                table: "sales_coupon_usages",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_sales_coupons_CreatedByUserId",
                table: "sales_coupons",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_sales_coupons_NormalizedCode",
                table: "sales_coupons",
                column: "NormalizedCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sales_coupons_StackingPolicyId",
                table: "sales_coupons",
                column: "StackingPolicyId");

            migrationBuilder.CreateIndex(
                name: "IX_sales_coupons_TargetType_TargetId_Status",
                table: "sales_coupons",
                columns: new[] { "TargetType", "TargetId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_sales_coupons_TeacherId",
                table: "sales_coupons",
                column: "TeacherId");

            migrationBuilder.CreateIndex(
                name: "IX_sales_financial_effects_PurchaseOperationId",
                table: "sales_financial_effects",
                column: "PurchaseOperationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sales_financial_effects_StudentId_TargetType_TargetId",
                table: "sales_financial_effects",
                columns: new[] { "StudentId", "TargetType", "TargetId" });

            migrationBuilder.CreateIndex(
                name: "IX_sales_financial_effects_TeacherId",
                table: "sales_financial_effects",
                column: "TeacherId");

            migrationBuilder.CreateIndex(
                name: "IX_sales_rules_CreatedByUserId",
                table: "sales_rules",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_sales_rules_SubjectId",
                table: "sales_rules",
                column: "SubjectId");

            migrationBuilder.CreateIndex(
                name: "IX_sales_rules_TargetType_TargetId_TeacherId_VideoTypeId_IsAct~",
                table: "sales_rules",
                columns: new[] { "TargetType", "TargetId", "TeacherId", "VideoTypeId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_sales_rules_TeacherId",
                table: "sales_rules",
                column: "TeacherId");

            migrationBuilder.CreateIndex(
                name: "IX_sales_rules_VideoTypeId",
                table: "sales_rules",
                column: "VideoTypeId");

            migrationBuilder.AddForeignKey(
                name: "FK_student_access_grants_public_exam_products_PublicExamProduc~",
                table: "student_access_grants",
                column: "PublicExamProductId",
                principalTable: "public_exam_products",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_student_access_grants_public_exam_products_PublicExamProduc~",
                table: "student_access_grants");

            migrationBuilder.DropTable(
                name: "printable_code_redemptions");

            migrationBuilder.DropTable(
                name: "public_exam_products");

            migrationBuilder.DropTable(
                name: "sales_coupon_usages");

            migrationBuilder.DropTable(
                name: "sales_financial_effects");

            migrationBuilder.DropTable(
                name: "sales_rules");

            migrationBuilder.DropTable(
                name: "printable_sales_codes");

            migrationBuilder.DropTable(
                name: "sales_coupons");

            migrationBuilder.DropTable(
                name: "printable_code_batches");

            migrationBuilder.DropTable(
                name: "discount_stacking_policies");

            migrationBuilder.DropTable(
                name: "printable_code_templates");

            migrationBuilder.DropIndex(
                name: "IX_student_access_grants_PublicExamProductId",
                table: "student_access_grants");

            migrationBuilder.DropColumn(
                name: "PublicExamProductId",
                table: "student_access_grants");
        }
    }
}
