using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NaderGorge.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MakeCodeGroupTeacherOptional : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_code_groups_teacher_profiles_TeacherId",
                table: "code_groups");

            migrationBuilder.AlterColumn<Guid>(
                name: "TeacherId",
                table: "code_groups",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddForeignKey(
                name: "FK_code_groups_teacher_profiles_TeacherId",
                table: "code_groups",
                column: "TeacherId",
                principalTable: "teacher_profiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_code_groups_teacher_profiles_TeacherId",
                table: "code_groups");

            migrationBuilder.AlterColumn<Guid>(
                name: "TeacherId",
                table: "code_groups",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_code_groups_teacher_profiles_TeacherId",
                table: "code_groups",
                column: "TeacherId",
                principalTable: "teacher_profiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
