using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NaderGorge.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSmsPaymentAutoMatcher : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_ParentDeviceTokens_StudentId\";");

            migrationBuilder.AlterColumn<string>(
                name: "ParentTrackingCode",
                table: "student_profiles",
                type: "character varying(6)",
                maxLength: 6,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "digital_wallets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PhoneNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Label = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    DailyLimit = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    MonthlyLimit = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CurrentBalance = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PairingToken = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DeviceStatus = table.Column<string>(type: "text", nullable: false),
                    LastSeenAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    SmsSenderFilters = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_digital_wallets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "incoming_sms_logs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WalletId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sender = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Body = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    ReceivedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ParsedAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    ParsedSenderPhone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    IsMatched = table.Column<bool>(type: "boolean", nullable: false),
                    MatchedRechargeRequestId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeduplicationHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_incoming_sms_logs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_incoming_sms_logs_digital_wallets_WalletId",
                        column: x => x.WalletId,
                        principalTable: "digital_wallets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "recharge_requests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    WalletId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    SenderPhoneNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ScreenshotUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ResolvedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ResolvedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    RejectionReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    MatchedSmsLogId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReservationExpiresAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_recharge_requests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_recharge_requests_digital_wallets_WalletId",
                        column: x => x.WalletId,
                        principalTable: "digital_wallets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_recharge_requests_incoming_sms_logs_MatchedSmsLogId",
                        column: x => x.MatchedSmsLogId,
                        principalTable: "incoming_sms_logs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_recharge_requests_users_ResolvedByUserId",
                        column: x => x.ResolvedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_recharge_requests_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ParentDeviceTokens_StudentId_DeviceToken",
                table: "ParentDeviceTokens",
                columns: new[] { "StudentId", "DeviceToken" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_digital_wallets_PairingToken",
                table: "digital_wallets",
                column: "PairingToken",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_digital_wallets_PhoneNumber",
                table: "digital_wallets",
                column: "PhoneNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_incoming_sms_logs_DeduplicationHash",
                table: "incoming_sms_logs",
                column: "DeduplicationHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_incoming_sms_logs_WalletId",
                table: "incoming_sms_logs",
                column: "WalletId");

            migrationBuilder.CreateIndex(
                name: "IX_recharge_requests_MatchedSmsLogId",
                table: "recharge_requests",
                column: "MatchedSmsLogId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_recharge_requests_ResolvedByUserId",
                table: "recharge_requests",
                column: "ResolvedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_recharge_requests_UserId",
                table: "recharge_requests",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_recharge_requests_WalletId",
                table: "recharge_requests",
                column: "WalletId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "recharge_requests");

            migrationBuilder.DropTable(
                name: "incoming_sms_logs");

            migrationBuilder.DropTable(
                name: "digital_wallets");

            migrationBuilder.DropIndex(
                name: "IX_ParentDeviceTokens_StudentId_DeviceToken",
                table: "ParentDeviceTokens");

            migrationBuilder.AlterColumn<string>(
                name: "ParentTrackingCode",
                table: "student_profiles",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(6)",
                oldMaxLength: 6,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ParentDeviceTokens_StudentId",
                table: "ParentDeviceTokens",
                column: "StudentId");
        }
    }
}
