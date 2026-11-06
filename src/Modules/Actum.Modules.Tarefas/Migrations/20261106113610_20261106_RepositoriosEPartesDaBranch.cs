using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Actum.Modules.Tarefas.Migrations
{
    /// <inheritdoc />
    public partial class _20261106_RepositoriosEPartesDaBranch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "parte_back",
                schema: "tarefas",
                table: "tarefa_branch",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "parte_front",
                schema: "tarefas",
                table: "tarefa_branch",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "repositorio_back",
                schema: "tarefas",
                table: "empresa",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "repositorio_front",
                schema: "tarefas",
                table: "empresa",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "parte_back",
                schema: "tarefas",
                table: "tarefa_branch");

            migrationBuilder.DropColumn(
                name: "parte_front",
                schema: "tarefas",
                table: "tarefa_branch");

            migrationBuilder.DropColumn(
                name: "repositorio_back",
                schema: "tarefas",
                table: "empresa");

            migrationBuilder.DropColumn(
                name: "repositorio_front",
                schema: "tarefas",
                table: "empresa");
        }
    }
}
