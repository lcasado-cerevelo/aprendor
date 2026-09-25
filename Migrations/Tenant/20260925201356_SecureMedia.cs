using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrainingPlatform.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class SecureMedia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "OwnerUserId",
                table: "MediaAsset",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Purpose",
                table: "MediaAsset",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "course");

            // Los archivos ya enlazados a una certificación externa son documentos del
            // expediente de esa persona.
            migrationBuilder.Sql(@"
UPDATE m SET m.Purpose = 'record', m.OwnerUserId = c.UserId
FROM MediaAsset m
JOIN ExternalCertification c ON c.MediaAssetId = m.Id;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OwnerUserId",
                table: "MediaAsset");

            migrationBuilder.DropColumn(
                name: "Purpose",
                table: "MediaAsset");
        }
    }
}
