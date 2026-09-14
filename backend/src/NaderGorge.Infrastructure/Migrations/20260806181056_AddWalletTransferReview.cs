using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NaderGorge.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWalletTransferReview : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "wallet_transfer_reviews",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    IncomingSmsLogId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceWalletId = table.Column<Guid>(type: "uuid", nullable: false),
                    DestinationPhoneNumber = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    ServiceFee = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TransferReference = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    OccurredAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    PlatformExpenseId = table.Column<Guid>(type: "uuid", nullable: true),
                    TreasuryTransferId = table.Column<Guid>(type: "uuid", nullable: true),
                    ClassifiedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ClassifiedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_wallet_transfer_reviews", x => x.Id);
                    table.CheckConstraint("CK_wallet_transfer_reviews_amount", "\"Amount\" > 0 AND \"ServiceFee\" >= 0");
                    table.ForeignKey(
                        name: "FK_wallet_transfer_reviews_digital_wallets_SourceWalletId",
                        column: x => x.SourceWalletId,
                        principalTable: "digital_wallets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_wallet_transfer_reviews_incoming_sms_logs_IncomingSmsLogId",
                        column: x => x.IncomingSmsLogId,
                        principalTable: "incoming_sms_logs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_wallet_transfer_reviews_platform_expenses_PlatformExpenseId",
                        column: x => x.PlatformExpenseId,
                        principalTable: "platform_expenses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_wallet_transfer_reviews_treasury_transfers_TreasuryTransfer~",
                        column: x => x.TreasuryTransferId,
                        principalTable: "treasury_transfers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_wallet_transfer_reviews_IncomingSmsLogId",
                table: "wallet_transfer_reviews",
                column: "IncomingSmsLogId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_wallet_transfer_reviews_PlatformExpenseId",
                table: "wallet_transfer_reviews",
                column: "PlatformExpenseId");

            migrationBuilder.CreateIndex(
                name: "IX_wallet_transfer_reviews_SourceWalletId",
                table: "wallet_transfer_reviews",
                column: "SourceWalletId");

            migrationBuilder.CreateIndex(
                name: "IX_wallet_transfer_reviews_Status_OccurredAt",
                table: "wallet_transfer_reviews",
                columns: new[] { "Status", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_wallet_transfer_reviews_TreasuryTransferId",
                table: "wallet_transfer_reviews",
                column: "TreasuryTransferId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "wallet_transfer_reviews");
        }
    }
}
