using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Actum.Modules.Tarefas.Migrations
{
    /// <inheritdoc />
    public partial class _20261118_AnaliseFontes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "fonte_comentarios",
                schema: "tarefas",
                table: "analise",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "fonte_descricao",
                schema: "tarefas",
                table: "analise",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "fonte_modo",
                schema: "tarefas",
                table: "analise",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "fonte_prompt",
                schema: "tarefas",
                table: "analise",
                type: "character varying(20000)",
                maxLength: 20000,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "analise_fonte_anexo",
                schema: "tarefas",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ordem = table.Column<short>(type: "smallint", nullable: false),
                    anexo_gid = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    nome = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    analise_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_analise_fonte_anexo", x => x.id);
                    table.ForeignKey(
                        name: "fk_analise_fonte_anexo_analise_analise_id",
                        column: x => x.analise_id,
                        principalSchema: "tarefas",
                        principalTable: "analise",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_analise_fonte_anexo_analise_id_ordem",
                schema: "tarefas",
                table: "analise_fonte_anexo",
                columns: new[] { "analise_id", "ordem" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "analise_fonte_anexo",
                schema: "tarefas");

            migrationBuilder.DropColumn(
                name: "fonte_comentarios",
                schema: "tarefas",
                table: "analise");

            migrationBuilder.DropColumn(
                name: "fonte_descricao",
                schema: "tarefas",
                table: "analise");

            migrationBuilder.DropColumn(
                name: "fonte_modo",
                schema: "tarefas",
                table: "analise");

            migrationBuilder.DropColumn(
                name: "fonte_prompt",
                schema: "tarefas",
                table: "analise");
        }
    }
}
