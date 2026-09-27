using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrainingPlatform.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class RetakeRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "Certificate",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "valid");

            migrationBuilder.AddColumn<Guid>(
                name: "StatusByUserId",
                table: "Certificate",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "StatusChangedAt",
                table: "Certificate",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StatusReason",
                table: "Certificate",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VoidReason",
                table: "Attempt",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "VoidedAt",
                table: "Attempt",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "RetakeRequest",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TrainingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Mode = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    DueAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedByName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FulfilledAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FulfilledAttemptId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CancelledAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CancelledByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RetakeRequest", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RetakeRequest_UserId_TrainingId",
                table: "RetakeRequest",
                columns: new[] { "UserId", "TrainingId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RetakeRequest");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Certificate");

            migrationBuilder.DropColumn(
                name: "StatusByUserId",
                table: "Certificate");

            migrationBuilder.DropColumn(
                name: "StatusChangedAt",
                table: "Certificate");

            migrationBuilder.DropColumn(
                name: "StatusReason",
                table: "Certificate");

            migrationBuilder.DropColumn(
                name: "VoidReason",
                table: "Attempt");

            migrationBuilder.DropColumn(
                name: "VoidedAt",
                table: "Attempt");
        }
    }
}
