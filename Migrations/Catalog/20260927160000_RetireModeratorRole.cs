using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using TrainingPlatform.Catalog;

#nullable disable

namespace TrainingPlatform.Migrations.Catalog
{
    // Se retira el rol Moderator (sep 2026): nació para la entrega en vivo con facilitador,
    // que nunca se construyó, y en la práctica solo calificaba y aprobaba cancelaciones.
    // Quienes lo tenían pasan a Author, que conserva esas dos funciones (y es lo que el
    // servidor ya les permitía). Solo cambia datos: el modelo no cambia, así que no hace
    // falta tocar el ModelSnapshot. El Down no puede saber quién era moderador: no revierte.
    /// <inheritdoc />
    [DbContext(typeof(CatalogDbContext))]
    [Migration("20260927160000_RetireModeratorRole")]
    public partial class RetireModeratorRole : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE [User] SET [Role] = N'Author' WHERE [Role] = N'Moderator';");
            migrationBuilder.Sql("UPDATE [UserCompany] SET [Role] = N'Author' WHERE [Role] = N'Moderator';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
