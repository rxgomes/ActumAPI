using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Actum.Modules.Tarefas.Migrations
{
    /// <inheritdoc />
    public partial class _20261103_Analise : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "analise",
                schema: "tarefas",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tarefa_id = table.Column<long>(type: "bigint", nullable: false),
                    versao = table.Column<short>(type: "smallint", nullable: false),
                    objetivo = table.Column<string>(type: "text", nullable: false),
                    modelo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    tokens_entrada = table.Column<int>(type: "integer", nullable: false),
                    tokens_saida = table.Column<int>(type: "integer", nullable: false),
                    analisada_em = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    identifier = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                    create_in = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "localtimestamp"),
                    update_in = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "localtimestamp"),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_analise", x => x.id);
                    table.ForeignKey(
                        name: "fk_analise_tarefa_tarefa_id",
                        column: x => x.tarefa_id,
                        principalSchema: "tarefas",
                        principalTable: "tarefa",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "analise_item",
                schema: "tarefas",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    analise_id = table.Column<long>(type: "bigint", nullable: false),
                    tipo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ordem = table.Column<short>(type: "smallint", nullable: false),
                    texto = table.Column<string>(type: "text", nullable: false),
                    concluido_em = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    identifier = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                    create_in = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "localtimestamp"),
                    update_in = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "localtimestamp"),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_analise_item", x => x.id);
                    table.ForeignKey(
                        name: "fk_analise_item_analise_analise_id",
                        column: x => x.analise_id,
                        principalSchema: "tarefas",
                        principalTable: "analise",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_analise_identifier",
                schema: "tarefas",
                table: "analise",
                column: "identifier",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_analise_tarefa_id_versao",
                schema: "tarefas",
                table: "analise",
                columns: new[] { "tarefa_id", "versao" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_analise_item_analise_id_tipo_ordem",
                schema: "tarefas",
                table: "analise_item",
                columns: new[] { "analise_id", "tipo", "ordem" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_analise_item_identifier",
                schema: "tarefas",
                table: "analise_item",
                column: "identifier",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "analise_item",
                schema: "tarefas");

            migrationBuilder.DropTable(
                name: "analise",
                schema: "tarefas");
        }
    }
}
