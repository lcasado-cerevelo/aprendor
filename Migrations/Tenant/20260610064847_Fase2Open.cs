using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrainingPlatform.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class Fase2Open : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "GradedAt",
                table: "ItemResponses",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GradedByName",
                table: "ItemResponses",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "GradedByUserId",
                table: "ItemResponses",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GraderComment",
                table: "ItemResponses",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "NeedsGrading",
                table: "ItemResponses",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GradedAt",
                table: "ItemResponses");

            migrationBuilder.DropColumn(
                name: "GradedByName",
                table: "ItemResponses");

            migrationBuilder.DropColumn(
                name: "GradedByUserId",
                table: "ItemResponses");

            migrationBuilder.DropColumn(
                name: "GraderComment",
                table: "ItemResponses");

            migrationBuilder.DropColumn(
                name: "NeedsGrading",
                table: "ItemResponses");
        }
    }
}
