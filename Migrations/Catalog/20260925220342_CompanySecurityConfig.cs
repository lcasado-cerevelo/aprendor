using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrainingPlatform.Migrations.Catalog
{
    /// <inheritdoc />
    public partial class CompanySecurityConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SecurityConfigJson",
                table: "Tenant",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "{}");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SecurityConfigJson",
                table: "Tenant");
        }
    }
}
