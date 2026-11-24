using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgendaLogar.Core.Datos.Migraciones
{
    /// <inheritdoc />
    public partial class Hito1Autenticacion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "DebeCambiarPassword",
                table: "Usuario",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DebeCambiarPassword",
                table: "Usuario");
        }
    }
}
