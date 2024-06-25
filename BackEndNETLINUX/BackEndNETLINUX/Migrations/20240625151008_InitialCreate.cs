using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BackEndNETLINUX.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Trabajadores",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Trabajadores", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "RegistrosTiempo",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TrabajadorID = table.Column<int>(type: "int", nullable: false),
                    Fecha = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TiempoEntrada = table.Column<TimeSpan>(type: "time", nullable: false),
                    TiempoSalida = table.Column<TimeSpan>(type: "time", nullable: false),
                    TiempoTrabajado = table.Column<TimeSpan>(type: "time", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RegistrosTiempo", x => x.ID);
                    table.ForeignKey(
                        name: "FK_RegistrosTiempo_Trabajadores_TrabajadorID",
                        column: x => x.TrabajadorID,
                        principalTable: "Trabajadores",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RegistrosTiempo_TrabajadorID",
                table: "RegistrosTiempo",
                column: "TrabajadorID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RegistrosTiempo");

            migrationBuilder.DropTable(
                name: "Trabajadores");
        }
    }
}
