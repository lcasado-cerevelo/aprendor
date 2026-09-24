using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrainingPlatform.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class AddGroupPlansAndOnboarding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "JoinedAt",
                table: "UserGroupMember",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "GETUTCDATE()");

            migrationBuilder.AddColumn<int>(
                name: "OnboardingDays",
                table: "UserGroup",
                type: "int",
                nullable: false,
                defaultValue: 7);

            migrationBuilder.AddColumn<string>(
                name: "Audience",
                table: "Training",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "everyone");

            migrationBuilder.AddColumn<int>(
                name: "OnboardingDays",
                table: "Training",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "GroupCourse",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserGroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TrainingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DueDays = table.Column<int>(type: "int", nullable: true),
                    AddedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GroupCourse", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GroupCourse_TrainingId",
                table: "GroupCourse",
                column: "TrainingId");

            migrationBuilder.CreateIndex(
                name: "IX_GroupCourse_UserGroupId_TrainingId",
                table: "GroupCourse",
                columns: new[] { "UserGroupId", "TrainingId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GroupCourse");

            migrationBuilder.DropColumn(
                name: "JoinedAt",
                table: "UserGroupMember");

            migrationBuilder.DropColumn(
                name: "OnboardingDays",
                table: "UserGroup");

            migrationBuilder.DropColumn(
                name: "Audience",
                table: "Training");

            migrationBuilder.DropColumn(
                name: "OnboardingDays",
                table: "Training");
        }
    }
}
