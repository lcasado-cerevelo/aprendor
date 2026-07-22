using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrainingPlatform.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class Fase3Sets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DisplayGroups");

            migrationBuilder.DropIndex(
                name: "IX_Trainings_DisplayGroupId",
                table: "Trainings");

            migrationBuilder.DropIndex(
                name: "IX_Assignments_DisplayGroupId",
                table: "Assignments");

            migrationBuilder.DropColumn(
                name: "DisplayGroupId",
                table: "Trainings");

            migrationBuilder.RenameColumn(
                name: "DisplayGroupId",
                table: "Assignments",
                newName: "SetId");

            migrationBuilder.AddColumn<Guid>(
                name: "StableKey",
                table: "TrainingItems",
                type: "uniqueidentifier",
                nullable: false,
                defaultValueSql: "NEWID()");

            migrationBuilder.AddColumn<Guid>(
                name: "SetId",
                table: "Attempts",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TrainingSetExclusions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StableKey = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrainingSetExclusions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TrainingSets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TrainingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrainingSets", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TrainingItems_StableKey",
                table: "TrainingItems",
                column: "StableKey");

            migrationBuilder.CreateIndex(
                name: "IX_Assignments_TrainingId",
                table: "Assignments",
                column: "TrainingId");

            migrationBuilder.CreateIndex(
                name: "IX_TrainingSetExclusions_SetId",
                table: "TrainingSetExclusions",
                column: "SetId");

            migrationBuilder.CreateIndex(
                name: "IX_TrainingSets_TrainingId",
                table: "TrainingSets",
                column: "TrainingId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TrainingSetExclusions");

            migrationBuilder.DropTable(
                name: "TrainingSets");

            migrationBuilder.DropIndex(
                name: "IX_TrainingItems_StableKey",
                table: "TrainingItems");

            migrationBuilder.DropIndex(
                name: "IX_Assignments_TrainingId",
                table: "Assignments");

            migrationBuilder.DropColumn(
                name: "StableKey",
                table: "TrainingItems");

            migrationBuilder.DropColumn(
                name: "SetId",
                table: "Attempts");

            migrationBuilder.RenameColumn(
                name: "SetId",
                table: "Assignments",
                newName: "DisplayGroupId");

            migrationBuilder.AddColumn<Guid>(
                name: "DisplayGroupId",
                table: "Trainings",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DisplayGroups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DefaultTrainingId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DisplayGroups", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Trainings_DisplayGroupId",
                table: "Trainings",
                column: "DisplayGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_Assignments_DisplayGroupId",
                table: "Assignments",
                column: "DisplayGroupId");
        }
    }
}
