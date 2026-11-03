using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Actum.Modules.Tarefas.Migrations
{
    /// <inheritdoc />
    public partial class _20261103_AnaliseGrupos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "grupo",
                schema: "tarefas",
                table: "analise_item",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "vago",
                schema: "tarefas",
                table: "analise_item",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "grupo",
                schema: "tarefas",
                table: "analise_item");

            migrationBuilder.DropColumn(
                name: "vago",
                schema: "tarefas",
                table: "analise_item");
        }
    }
}
