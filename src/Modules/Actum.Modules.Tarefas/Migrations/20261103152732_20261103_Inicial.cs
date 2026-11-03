using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Actum.Modules.Tarefas.Migrations
{
    /// <inheritdoc />
    public partial class _20261103_Inicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "tarefas");

            migrationBuilder.CreateTable(
                name: "empresa",
                schema: "tarefas",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    asana_workspace_gid = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    cor = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    sincronizar = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    carga_inicial_em = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    identifier = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                    create_in = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "localtimestamp"),
                    update_in = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "localtimestamp"),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_empresa", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "sincronizacao",
                schema: "tarefas",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    iniciada_em = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    finalizada_em = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    sucesso = table.Column<bool>(type: "boolean", nullable: false),
                    tarefas_novas = table.Column<int>(type: "integer", nullable: false),
                    tarefas_atualizadas = table.Column<int>(type: "integer", nullable: false),
                    tarefas_arquivadas = table.Column<int>(type: "integer", nullable: false),
                    reaberturas = table.Column<int>(type: "integer", nullable: false),
                    erro = table.Column<string>(type: "text", nullable: true),
                    identifier = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                    create_in = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "localtimestamp"),
                    update_in = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "localtimestamp"),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sincronizacao", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tarefa",
                schema: "tarefas",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    empresa_id = table.Column<long>(type: "bigint", nullable: false),
                    asana_gid = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    titulo = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    descricao = table.Column<string>(type: "text", nullable: false),
                    descricao_html = table.Column<string>(type: "text", nullable: false),
                    projetos = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    link_asana = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    prazo_asana = table.Column<DateOnly>(type: "date", nullable: true),
                    prazo_interno = table.Column<DateOnly>(type: "date", nullable: true),
                    prazo_interno_pendente = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    status_anterior = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    prioridade = table.Column<short>(type: "smallint", nullable: true),
                    asana_modificada_em = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    concluida_em = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    arquivada_em = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    motivo_arquivamento = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    sincronizada_em = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    identifier = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                    create_in = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "localtimestamp"),
                    update_in = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "localtimestamp"),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tarefa", x => x.id);
                    table.ForeignKey(
                        name: "fk_tarefa_empresa_empresa_id",
                        column: x => x.empresa_id,
                        principalSchema: "tarefas",
                        principalTable: "empresa",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "comentario",
                schema: "tarefas",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tarefa_id = table.Column<long>(type: "bigint", nullable: false),
                    asana_story_gid = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    autor = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    texto = table.Column<string>(type: "text", nullable: false),
                    criado_em = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    lido = table.Column<bool>(type: "boolean", nullable: false),
                    identifier = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                    create_in = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "localtimestamp"),
                    update_in = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "localtimestamp"),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_comentario", x => x.id);
                    table.ForeignKey(
                        name: "fk_comentario_tarefa_tarefa_id",
                        column: x => x.tarefa_id,
                        principalSchema: "tarefas",
                        principalTable: "tarefa",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "retrabalho",
                schema: "tarefas",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tarefa_id = table.Column<long>(type: "bigint", nullable: false),
                    numero = table.Column<short>(type: "smallint", nullable: false),
                    reaberta_em = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    reaberta_por = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    asana_story_gid = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    prazo_interno_anterior = table.Column<DateOnly>(type: "date", nullable: true),
                    prazo_interno_novo = table.Column<DateOnly>(type: "date", nullable: true),
                    resolvido_em = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    identifier = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                    create_in = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "localtimestamp"),
                    update_in = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "localtimestamp"),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_retrabalho", x => x.id);
                    table.ForeignKey(
                        name: "fk_retrabalho_tarefa_tarefa_id",
                        column: x => x.tarefa_id,
                        principalSchema: "tarefas",
                        principalTable: "tarefa",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_comentario_asana_story_gid",
                schema: "tarefas",
                table: "comentario",
                column: "asana_story_gid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_comentario_identifier",
                schema: "tarefas",
                table: "comentario",
                column: "identifier",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_comentario_tarefa_id",
                schema: "tarefas",
                table: "comentario",
                column: "tarefa_id");

            migrationBuilder.CreateIndex(
                name: "ix_empresa_asana_workspace_gid",
                schema: "tarefas",
                table: "empresa",
                column: "asana_workspace_gid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_empresa_identifier",
                schema: "tarefas",
                table: "empresa",
                column: "identifier",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_retrabalho_asana_story_gid",
                schema: "tarefas",
                table: "retrabalho",
                column: "asana_story_gid",
                unique: true,
                filter: "asana_story_gid is not null");

            migrationBuilder.CreateIndex(
                name: "ix_retrabalho_identifier",
                schema: "tarefas",
                table: "retrabalho",
                column: "identifier",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_retrabalho_tarefa_id_numero",
                schema: "tarefas",
                table: "retrabalho",
                columns: new[] { "tarefa_id", "numero" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sincronizacao_identifier",
                schema: "tarefas",
                table: "sincronizacao",
                column: "identifier",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sincronizacao_iniciada_em",
                schema: "tarefas",
                table: "sincronizacao",
                column: "iniciada_em");

            migrationBuilder.CreateIndex(
                name: "ix_tarefa_asana_gid",
                schema: "tarefas",
                table: "tarefa",
                column: "asana_gid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_tarefa_empresa_id_status",
                schema: "tarefas",
                table: "tarefa",
                columns: new[] { "empresa_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_tarefa_identifier",
                schema: "tarefas",
                table: "tarefa",
                column: "identifier",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_tarefa_prazo_interno",
                schema: "tarefas",
                table: "tarefa",
                column: "prazo_interno");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "comentario",
                schema: "tarefas");

            migrationBuilder.DropTable(
                name: "retrabalho",
                schema: "tarefas");

            migrationBuilder.DropTable(
                name: "sincronizacao",
                schema: "tarefas");

            migrationBuilder.DropTable(
                name: "tarefa",
                schema: "tarefas");

            migrationBuilder.DropTable(
                name: "empresa",
                schema: "tarefas");
        }
    }
}
