using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Actum.Modules.Tarefas.Migrations
{
    /// <inheritdoc />
    public partial class _20261103_RetrabalhoPorPares : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_retrabalho_tarefa_id_numero",
                schema: "tarefas",
                table: "retrabalho");

            migrationBuilder.AddColumn<string>(
                name: "tipo",
                schema: "tarefas",
                table: "sincronizacao",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "sincronizacao");

            migrationBuilder.AddColumn<DateTime>(
                name: "concluida_em",
                schema: "tarefas",
                table: "retrabalho",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "anotacao",
                schema: "tarefas",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tarefa_id = table.Column<long>(type: "bigint", nullable: false),
                    texto = table.Column<string>(type: "text", nullable: false),
                    alterada_em = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    identifier = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                    create_in = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "localtimestamp"),
                    update_in = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "localtimestamp"),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_anotacao", x => x.id);
                    table.ForeignKey(
                        name: "fk_anotacao_tarefa_tarefa_id",
                        column: x => x.tarefa_id,
                        principalSchema: "tarefas",
                        principalTable: "tarefa",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "conclusao",
                schema: "tarefas",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tarefa_id = table.Column<long>(type: "bigint", nullable: false),
                    asana_story_gid = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    concluida_em = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    concluida_por = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    identifier = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                    create_in = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "localtimestamp"),
                    update_in = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "localtimestamp"),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_conclusao", x => x.id);
                    table.ForeignKey(
                        name: "fk_conclusao_tarefa_tarefa_id",
                        column: x => x.tarefa_id,
                        principalSchema: "tarefas",
                        principalTable: "tarefa",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_retrabalho_reaberta_em",
                schema: "tarefas",
                table: "retrabalho",
                column: "reaberta_em");

            migrationBuilder.CreateIndex(
                name: "ix_retrabalho_tarefa_id_numero",
                schema: "tarefas",
                table: "retrabalho",
                columns: new[] { "tarefa_id", "numero" });

            migrationBuilder.CreateIndex(
                name: "ix_anotacao_identifier",
                schema: "tarefas",
                table: "anotacao",
                column: "identifier",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_anotacao_tarefa_id",
                schema: "tarefas",
                table: "anotacao",
                column: "tarefa_id");

            migrationBuilder.CreateIndex(
                name: "ix_conclusao_asana_story_gid",
                schema: "tarefas",
                table: "conclusao",
                column: "asana_story_gid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_conclusao_identifier",
                schema: "tarefas",
                table: "conclusao",
                column: "identifier",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_conclusao_tarefa_id_concluida_em",
                schema: "tarefas",
                table: "conclusao",
                columns: new[] { "tarefa_id", "concluida_em" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "anotacao",
                schema: "tarefas");

            migrationBuilder.DropTable(
                name: "conclusao",
                schema: "tarefas");

            migrationBuilder.DropIndex(
                name: "ix_retrabalho_reaberta_em",
                schema: "tarefas",
                table: "retrabalho");

            migrationBuilder.DropIndex(
                name: "ix_retrabalho_tarefa_id_numero",
                schema: "tarefas",
                table: "retrabalho");

            migrationBuilder.DropColumn(
                name: "tipo",
                schema: "tarefas",
                table: "sincronizacao");

            migrationBuilder.DropColumn(
                name: "concluida_em",
                schema: "tarefas",
                table: "retrabalho");

            migrationBuilder.CreateIndex(
                name: "ix_retrabalho_tarefa_id_numero",
                schema: "tarefas",
                table: "retrabalho",
                columns: new[] { "tarefa_id", "numero" },
                unique: true);
        }
    }
}
