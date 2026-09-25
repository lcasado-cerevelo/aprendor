using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrainingPlatform.Migrations.Catalog
{
    /// <inheritdoc />
    public partial class SecurityHardening : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AccessFailedCount",
                table: "User",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<long>(
                name: "LastTotpStep",
                table: "User",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LockoutEnd",
                table: "User",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PendingTotpSecret",
                table: "User",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SecurityStamp",
                table: "User",
                type: "uniqueidentifier",
                nullable: false,
                defaultValueSql: "NEWID()");

            migrationBuilder.AddColumn<DateTime>(
                name: "TempPasswordExpiresAt",
                table: "User",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TwoFactorFailedCount",
                table: "User",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "TwoFactorLockedUntil",
                table: "User",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SessionExpiresAt",
                table: "TwoFactorChallenge",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TargetTenantId",
                table: "TwoFactorChallenge",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Purpose",
                table: "PasswordResetToken",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "reset");

            migrationBuilder.AddColumn<string>(
                name: "Ip",
                table: "AuditLog",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SecurityEvent",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Kind = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Ip = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    At = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SecurityEvent", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SecurityEvent_Kind_Ip_At",
                table: "SecurityEvent",
                columns: new[] { "Kind", "Ip", "At" });

            migrationBuilder.CreateIndex(
                name: "IX_SecurityEvent_Kind_UserId_At",
                table: "SecurityEvent",
                columns: new[] { "Kind", "UserId", "At" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SecurityEvent");

            migrationBuilder.DropColumn(
                name: "AccessFailedCount",
                table: "User");

            migrationBuilder.DropColumn(
                name: "LastTotpStep",
                table: "User");

            migrationBuilder.DropColumn(
                name: "LockoutEnd",
                table: "User");

            migrationBuilder.DropColumn(
                name: "PendingTotpSecret",
                table: "User");

            migrationBuilder.DropColumn(
                name: "SecurityStamp",
                table: "User");

            migrationBuilder.DropColumn(
                name: "TempPasswordExpiresAt",
                table: "User");

            migrationBuilder.DropColumn(
                name: "TwoFactorFailedCount",
                table: "User");

            migrationBuilder.DropColumn(
                name: "TwoFactorLockedUntil",
                table: "User");

            migrationBuilder.DropColumn(
                name: "SessionExpiresAt",
                table: "TwoFactorChallenge");

            migrationBuilder.DropColumn(
                name: "TargetTenantId",
                table: "TwoFactorChallenge");

            migrationBuilder.DropColumn(
                name: "Purpose",
                table: "PasswordResetToken");

            migrationBuilder.DropColumn(
                name: "Ip",
                table: "AuditLog");
        }
    }
}
