using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrainingPlatform.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class Fase2Cancel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CancelApprovalComment",
                table: "Attempts",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CancelRequestComment",
                table: "Attempts",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CancelRequestedAt",
                table: "Attempts",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CanceledAt",
                table: "Attempts",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CanceledByName",
                table: "Attempts",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CanceledByUserId",
                table: "Attempts",
                type: "uniqueidentifier",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CancelApprovalComment",
                table: "Attempts");

            migrationBuilder.DropColumn(
                name: "CancelRequestComment",
                table: "Attempts");

            migrationBuilder.DropColumn(
                name: "CancelRequestedAt",
                table: "Attempts");

            migrationBuilder.DropColumn(
                name: "CanceledAt",
                table: "Attempts");

            migrationBuilder.DropColumn(
                name: "CanceledByName",
                table: "Attempts");

            migrationBuilder.DropColumn(
                name: "CanceledByUserId",
                table: "Attempts");
        }
    }
}
