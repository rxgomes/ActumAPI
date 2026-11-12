using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Actum.Modules.Tarefas.Migrations
{
    /// <inheritdoc />
    public partial class _20261112_WorktreesDasBranches : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<List<string>>(
                name: "arquivos_back",
                schema: "tarefas",
                table: "empresa",
                type: "text[]",
                nullable: false,
                defaultValueSql: "'{}'::text[]");

            migrationBuilder.AddColumn<List<string>>(
                name: "arquivos_front",
                schema: "tarefas",
                table: "empresa",
                type: "text[]",
                nullable: false,
                defaultValueSql: "'{}'::text[]");

            migrationBuilder.AddColumn<string>(
                name: "clone_back",
                schema: "tarefas",
                table: "empresa",
                type: "character varying(260)",
                maxLength: 260,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "clone_front",
                schema: "tarefas",
                table: "empresa",
                type: "character varying(260)",
                maxLength: 260,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "pasta_worktrees",
                schema: "tarefas",
                table: "empresa",
                type: "character varying(260)",
                maxLength: 260,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "worktree",
                schema: "tarefas",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tarefa_branch_id = table.Column<long>(type: "bigint", nullable: false),
                    pasta = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    removido_em = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    identifier = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                    create_in = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "localtimestamp"),
                    update_in = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "localtimestamp"),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_worktree", x => x.id);
                    table.ForeignKey(
                        name: "fk_worktree_tarefa_branch_tarefa_branch_id",
                        column: x => x.tarefa_branch_id,
                        principalSchema: "tarefas",
                        principalTable: "tarefa_branch",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "worktree_parte",
                schema: "tarefas",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    worktree_id = table.Column<long>(type: "bigint", nullable: false),
                    parte = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    caminho = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    situacao = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ultimo_erro = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    criado_em = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    identifier = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                    create_in = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "localtimestamp"),
                    update_in = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "localtimestamp"),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_worktree_parte", x => x.id);
                    table.ForeignKey(
                        name: "fk_worktree_parte_worktree_worktree_id",
                        column: x => x.worktree_id,
                        principalSchema: "tarefas",
                        principalTable: "worktree",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_worktree_identifier",
                schema: "tarefas",
                table: "worktree",
                column: "identifier",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_worktree_tarefa_branch_id",
                schema: "tarefas",
                table: "worktree",
                column: "tarefa_branch_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_worktree_parte_identifier",
                schema: "tarefas",
                table: "worktree_parte",
                column: "identifier",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_worktree_parte_worktree_id_parte",
                schema: "tarefas",
                table: "worktree_parte",
                columns: new[] { "worktree_id", "parte" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "worktree_parte",
                schema: "tarefas");

            migrationBuilder.DropTable(
                name: "worktree",
                schema: "tarefas");

            migrationBuilder.DropColumn(
                name: "arquivos_back",
                schema: "tarefas",
                table: "empresa");

            migrationBuilder.DropColumn(
                name: "arquivos_front",
                schema: "tarefas",
                table: "empresa");

            migrationBuilder.DropColumn(
                name: "clone_back",
                schema: "tarefas",
                table: "empresa");

            migrationBuilder.DropColumn(
                name: "clone_front",
                schema: "tarefas",
                table: "empresa");

            migrationBuilder.DropColumn(
                name: "pasta_worktrees",
                schema: "tarefas",
                table: "empresa");
        }
    }
}
