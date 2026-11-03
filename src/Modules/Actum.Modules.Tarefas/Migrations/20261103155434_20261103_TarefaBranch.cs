using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Actum.Modules.Tarefas.Migrations
{
    /// <inheritdoc />
    public partial class _20261103_TarefaBranch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "tarefa_branch",
                schema: "tarefas",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tarefa_id = table.Column<long>(type: "bigint", nullable: false),
                    tipo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    nome = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    retrabalho_id = table.Column<long>(type: "bigint", nullable: true),
                    asana_tag_gid = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    enviada_asana_em = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    identifier = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                    create_in = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "localtimestamp"),
                    update_in = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "localtimestamp"),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tarefa_branch", x => x.id);
                    table.ForeignKey(
                        name: "fk_tarefa_branch_retrabalho_retrabalho_id",
                        column: x => x.retrabalho_id,
                        principalSchema: "tarefas",
                        principalTable: "retrabalho",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_tarefa_branch_tarefa_tarefa_id",
                        column: x => x.tarefa_id,
                        principalSchema: "tarefas",
                        principalTable: "tarefa",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_tarefa_branch_identifier",
                schema: "tarefas",
                table: "tarefa_branch",
                column: "identifier",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_tarefa_branch_retrabalho_id",
                schema: "tarefas",
                table: "tarefa_branch",
                column: "retrabalho_id");

            migrationBuilder.CreateIndex(
                name: "ix_tarefa_branch_tarefa_id_nome",
                schema: "tarefas",
                table: "tarefa_branch",
                columns: new[] { "tarefa_id", "nome" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tarefa_branch",
                schema: "tarefas");
        }
    }
}
