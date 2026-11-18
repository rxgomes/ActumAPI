using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Actum.Modules.Tarefas.Migrations
{
    /// <inheritdoc />
    public partial class _20261113_AnaliseItemTrecho : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "trecho",
                schema: "tarefas",
                table: "analise_item",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "trecho",
                schema: "tarefas",
                table: "analise_item");
        }
    }
}
