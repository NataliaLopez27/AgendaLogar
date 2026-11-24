using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace AgendaLogar.Core.Datos.Migraciones
{
    /// <inheritdoc />
    public partial class Inicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Cliente",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nombre = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Nif = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Tipo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Notas = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cliente", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Modelo",
                columns: table => new
                {
                    Codigo = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Periodicidad = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    AplicaFisica = table.Column<bool>(type: "bit", nullable: false),
                    AplicaSociedad = table.Column<bool>(type: "bit", nullable: false),
                    Orden = table.Column<int>(type: "int", nullable: false),
                    ResumeA = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Modelo", x => x.Codigo);
                });

            migrationBuilder.CreateTable(
                name: "Periodo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Anio = table.Column<int>(type: "int", nullable: false),
                    Trimestre = table.Column<byte>(type: "tinyint", nullable: true),
                    Estado = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Periodo", x => x.Id);
                    table.CheckConstraint("CK_Periodo_Trimestre", "[Trimestre] IS NULL OR [Trimestre] BETWEEN 1 AND 4");
                });

            migrationBuilder.CreateTable(
                name: "Usuario",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nombre = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    NombreUsuario = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    HashPassword = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Rol = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Usuario", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AltaCliente",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ClienteId = table.Column<int>(type: "int", nullable: false),
                    FechaAlta = table.Column<DateOnly>(type: "date", nullable: false),
                    FechaBaja = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AltaCliente", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AltaCliente_Cliente_ClienteId",
                        column: x => x.ClienteId,
                        principalTable: "Cliente",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ClienteModelo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ClienteId = table.Column<int>(type: "int", nullable: false),
                    ModeloCodigo = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    FechaAlta = table.Column<DateOnly>(type: "date", nullable: false),
                    FechaBaja = table.Column<DateOnly>(type: "date", nullable: true),
                    Notas = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClienteModelo", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClienteModelo_Cliente_ClienteId",
                        column: x => x.ClienteId,
                        principalTable: "Cliente",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ClienteModelo_Modelo_ModeloCodigo",
                        column: x => x.ModeloCodigo,
                        principalTable: "Modelo",
                        principalColumn: "Codigo",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Asignacion",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PeriodoId = table.Column<int>(type: "int", nullable: false),
                    ClienteId = table.Column<int>(type: "int", nullable: false),
                    UsuarioId = table.Column<int>(type: "int", nullable: true),
                    Estado = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Importe = table.Column<decimal>(type: "decimal(14,2)", precision: 14, scale: 2, nullable: true),
                    Observaciones = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    FechaHecho = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FechaListo = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModificadoPorId = table.Column<int>(type: "int", nullable: true),
                    ModificadoEn = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Asignacion", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Asignacion_Cliente_ClienteId",
                        column: x => x.ClienteId,
                        principalTable: "Cliente",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Asignacion_Periodo_PeriodoId",
                        column: x => x.PeriodoId,
                        principalTable: "Periodo",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Asignacion_Usuario_ModificadoPorId",
                        column: x => x.ModificadoPorId,
                        principalTable: "Usuario",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Asignacion_Usuario_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuario",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "HistorialAsignacion",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AsignacionId = table.Column<int>(type: "int", nullable: false),
                    Campo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ValorAnterior = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ValorNuevo = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    UsuarioId = table.Column<int>(type: "int", nullable: false),
                    Fecha = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HistorialAsignacion", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HistorialAsignacion_Asignacion_AsignacionId",
                        column: x => x.AsignacionId,
                        principalTable: "Asignacion",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_HistorialAsignacion_Usuario_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuario",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "Modelo",
                columns: new[] { "Codigo", "AplicaFisica", "AplicaSociedad", "Descripcion", "Orden", "Periodicidad", "ResumeA" },
                values: new object[,]
                {
                    { "100", true, false, "Declaración de la Renta", 150, "Anual", null },
                    { "111", true, true, "Retenciones trabajo / profesionales", 30, "Trimestral", null },
                    { "115", true, true, "Retenciones alquileres", 40, "Trimestral", null },
                    { "130", true, false, "Pago fraccionado IRPF (estimación directa)", 10, "Trimestral", null },
                    { "131", true, false, "Pago fraccionado IRPF (módulos)", 20, "Trimestral", null },
                    { "180", true, true, "Resumen anual alquileres", 130, "Anual", "115" },
                    { "190", true, true, "Resumen anual retenciones", 120, "Anual", "111" },
                    { "200", false, true, "Impuesto de Sociedades", 80, "Trimestral", null },
                    { "202", false, true, "Pago fraccionado Impuesto de Sociedades", 70, "Trimestral", null },
                    { "303", true, true, "IVA", 50, "Trimestral", null },
                    { "347", true, true, "Operaciones con terceros", 140, "Anual", null },
                    { "349", true, true, "Operaciones intracomunitarias", 60, "Trimestral", null },
                    { "390", true, true, "Resumen anual IVA", 110, "Anual", "303" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_AltaCliente_ClienteId_FechaAlta",
                table: "AltaCliente",
                columns: new[] { "ClienteId", "FechaAlta" });

            migrationBuilder.CreateIndex(
                name: "IX_Asignacion_ClienteId",
                table: "Asignacion",
                column: "ClienteId");

            migrationBuilder.CreateIndex(
                name: "IX_Asignacion_ModificadoPorId",
                table: "Asignacion",
                column: "ModificadoPorId");

            migrationBuilder.CreateIndex(
                name: "IX_Asignacion_PeriodoId_ClienteId",
                table: "Asignacion",
                columns: new[] { "PeriodoId", "ClienteId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Asignacion_PeriodoId_UsuarioId_Estado",
                table: "Asignacion",
                columns: new[] { "PeriodoId", "UsuarioId", "Estado" });

            migrationBuilder.CreateIndex(
                name: "IX_Asignacion_UsuarioId",
                table: "Asignacion",
                column: "UsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_Cliente_Nif",
                table: "Cliente",
                column: "Nif");

            migrationBuilder.CreateIndex(
                name: "IX_Cliente_Nombre",
                table: "Cliente",
                column: "Nombre");

            migrationBuilder.CreateIndex(
                name: "IX_ClienteModelo_ClienteId_ModeloCodigo",
                table: "ClienteModelo",
                columns: new[] { "ClienteId", "ModeloCodigo" });

            migrationBuilder.CreateIndex(
                name: "IX_ClienteModelo_ModeloCodigo",
                table: "ClienteModelo",
                column: "ModeloCodigo");

            migrationBuilder.CreateIndex(
                name: "IX_HistorialAsignacion_AsignacionId_Fecha",
                table: "HistorialAsignacion",
                columns: new[] { "AsignacionId", "Fecha" });

            migrationBuilder.CreateIndex(
                name: "IX_HistorialAsignacion_UsuarioId",
                table: "HistorialAsignacion",
                column: "UsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_Periodo_Anio_Trimestre",
                table: "Periodo",
                columns: new[] { "Anio", "Trimestre" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Usuario_NombreUsuario",
                table: "Usuario",
                column: "NombreUsuario",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AltaCliente");

            migrationBuilder.DropTable(
                name: "ClienteModelo");

            migrationBuilder.DropTable(
                name: "HistorialAsignacion");

            migrationBuilder.DropTable(
                name: "Modelo");

            migrationBuilder.DropTable(
                name: "Asignacion");

            migrationBuilder.DropTable(
                name: "Cliente");

            migrationBuilder.DropTable(
                name: "Periodo");

            migrationBuilder.DropTable(
                name: "Usuario");
        }
    }
}
