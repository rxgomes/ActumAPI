using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Actum.Modules.Tarefas.Migrations
{
    /// <inheritdoc />
    public partial class _20261105_AnaliseItemOrigem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "origem",
                schema: "tarefas",
                table: "analise_item",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "origem",
                schema: "tarefas",
                table: "analise_item");
        }
    }
}
