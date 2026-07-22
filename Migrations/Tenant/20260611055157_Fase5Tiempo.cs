using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrainingPlatform.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class Fase5Tiempo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ActiveSeconds",
                table: "Attempt",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastHeartbeatAt",
                table: "Attempt",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ActiveSeconds",
                table: "Attempt");

            migrationBuilder.DropColumn(
                name: "LastHeartbeatAt",
                table: "Attempt");
        }
    }
}
