using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrainingPlatform.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class Fase4Recurrencia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RecurrenceMonths",
                table: "Training",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RenewLeadDays",
                table: "Training",
                type: "int",
                nullable: false,
                defaultValue: 30);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RecurrenceMonths",
                table: "Training");

            migrationBuilder.DropColumn(
                name: "RenewLeadDays",
                table: "Training");
        }
    }
}
