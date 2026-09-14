using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NaderGorge.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddClusterLeases : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "cluster_leases",
                columns: table => new
                {
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    OwnerToken = table.Column<Guid>(type: "uuid", nullable: false),
                    FencingGeneration = table.Column<long>(type: "bigint", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    RenewedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    LastOutcome = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cluster_leases", x => x.Name);
                });

            migrationBuilder.CreateIndex(
                name: "IX_cluster_leases_ExpiresAt",
                table: "cluster_leases",
                column: "ExpiresAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "cluster_leases");
        }
    }
}
