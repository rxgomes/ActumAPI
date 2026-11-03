using Actum.Modules.Asana.Contracts;
using Actum.Modules.Tarefas.Application;
using Actum.Modules.Tarefas.Domain;
using Actum.Modules.Tarefas.Persistence;
using Actum.Tests.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

[assembly: AssemblyFixture(typeof(PostgresFixture))]

namespace Actum.Modules.Tarefas.Tests;

/// <summary>
/// As sincronizações gravam na mesma base temporária; numa só collection elas rodam em série e o registro
/// "última sincronização" é previsível.
/// </summary>
[CollectionDefinition(Nome)]
public sealed class SincronizacaoCollection
{
    public const string Nome = "Sincronização";
}

/// <summary>
/// Base dos testes de sincronização: aplica a migration do módulo e monta um cenário isolado por teste (usuário,
/// workspace e gids únicos), com o Asana simulado por <see cref="AsanaClientFalso"/>.
/// </summary>
[Collection(SincronizacaoCollection.Nome)]
public abstract class SincronizacaoTestBase(PostgresFixture postgres) : IAsyncLifetime
{
    /// <summary>
    /// Data e hora de referência das modificações no Asana
    /// </summary>
    protected static readonly DateTime Base = new(2026, 11, 1, 10, 0, 0, DateTimeKind.Unspecified);

    private readonly string _sufixo = Guid.NewGuid().ToString("N")[..12];

    private readonly SincronizacaoEmAndamento _emAndamento = new();

    protected SincronizacaoTestBase(PostgresFixture postgres, params string[] nomesDosWorkspaces)
        : this(postgres)
    {
        var nomes = nomesDosWorkspaces.Length == 0 ? ["Aureum-ERP"] : nomesDosWorkspaces;
        Workspaces = nomes.Select((nome, indice) => new AsanaWorkspace($"ws{indice}-{_sufixo}", nome)).ToList();
        Asana = new AsanaClientFalso(new AsanaUsuario(UsuarioGid, "Ricardo", Workspaces));
    }

    protected PostgresFixture Postgres { get; } = postgres;

    protected string UsuarioGid => $"user-{_sufixo}";

    protected IReadOnlyList<AsanaWorkspace> Workspaces { get; } = [];

    /// <summary>
    /// Primeiro workspace do cenário
    /// </summary>
    protected string Workspace => Workspaces[0].Gid;

    protected AsanaClientFalso Asana { get; } = null!;

    public async ValueTask InitializeAsync()
    {
        await using var contexto = TarefasDbContext.Criar(Postgres.DataSource);
        await contexto.Database.MigrateAsync(TestContext.Current.CancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        _emAndamento.Dispose();
        return ValueTask.CompletedTask;
    }

    protected string Gid(string nome)
    {
        return $"{nome}-{_sufixo}";
    }

    /// <summary>
    /// Tarefa aberta atribuída ao usuário do cenário
    /// </summary>
    protected AsanaTarefa TarefaAsana(
        string nome,
        DateOnly? prazo = null,
        DateTime? modificadaEm = null,
        string? responsavelGid = null,
        bool concluida = false)
    {
        return new AsanaTarefa(
            Gid(nome),
            $"Tarefa {nome}",
            $"notas {nome}",
            $"<body>notas {nome}</body>",
            prazo,
            concluida,
            modificadaEm ?? Base,
            $"https://app.asana.com/0/0/{Gid(nome)}",
            ["Projeto A", "Projeto B"],
            [],
            responsavelGid ?? UsuarioGid);
    }

    /// <summary>
    /// Define as tarefas abertas do workspace (substitui a lista anterior) e a situação delas para consulta individual
    /// </summary>
    protected void Abertas(string workspace, params AsanaTarefa[] tarefas)
    {
        Asana.Abertas[workspace] = [.. tarefas];
        foreach (var tarefa in tarefas)
        {
            Asana.Tarefas[tarefa.Gid] = tarefa;
        }
    }

    protected void Abertas(params AsanaTarefa[] tarefas)
    {
        Abertas(Workspace, tarefas);
    }

    protected void Historico(string nome, params AsanaStory[] stories)
    {
        Asana.Stories[Gid(nome)] = [.. stories];
    }

    protected AsanaStory Comentario(string id, string autor, string texto, DateTime em)
    {
        return new AsanaStory(Gid(id), AsanaStoryTipo.Comentario, em, autor, texto);
    }

    protected AsanaStory Reabertura(string id, string autor, DateTime em)
    {
        return new AsanaStory(Gid(id), AsanaStoryTipo.MarcadaIncompleta, em, autor, $"{autor} marcou como incompleta");
    }

    protected async Task<SincronizacaoResultado> SincronizarAsync()
    {
        await using var contexto = TarefasDbContext.Criar(Postgres.DataSource);
        var service = CriarServico(contexto);

        return await service.SincronizarAsync(TestContext.Current.CancellationToken);
    }

    protected ISincronizacaoService CriarServico(TarefasDbContext contexto)
    {
        return new SincronizacaoService(contexto, Asana, _emAndamento, NullLogger<SincronizacaoService>.Instance);
    }

    protected async Task<Tarefa?> LerTarefaAsync(string nome)
    {
        await using var contexto = TarefasDbContext.Criar(Postgres.DataSource);

        return await contexto.Set<Tarefa>()
            .AsNoTracking()
            .Include(tarefa => tarefa.Comentarios)
            .Include(tarefa => tarefa.Retrabalhos)
            .Include(tarefa => tarefa.Branches)
            .AsSplitQuery()
            .SingleOrDefaultAsync(tarefa => tarefa.AsanaGid == Gid(nome), TestContext.Current.CancellationToken);
    }

    protected async Task<Empresa> LerEmpresaAsync(string workspace)
    {
        await using var contexto = TarefasDbContext.Criar(Postgres.DataSource);

        return await contexto.Set<Empresa>()
            .AsNoTracking()
            .SingleAsync(empresa => empresa.AsanaWorkspaceGid == workspace, TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Simula o que o ciclo de vida faz fora desta change: muda status, data de conclusão e prazo interno
    /// </summary>
    protected async Task AlterarTarefaAsync(
        string nome,
        StatusTarefa? status = null,
        DateTime? concluidaEm = null,
        DateOnly? prazoInterno = null)
    {
        await using var contexto = TarefasDbContext.Criar(Postgres.DataSource);
        var tarefa = await contexto.Set<Tarefa>().SingleAsync(t => t.AsanaGid == Gid(nome), TestContext.Current.CancellationToken);
        var entrada = contexto.Entry(tarefa);

        if (status is { } novoStatus)
        {
            entrada.Property(t => t.Status).CurrentValue = novoStatus;
        }

        if (concluidaEm is { } data)
        {
            entrada.Property(t => t.ConcluidaEm).CurrentValue = data;
        }

        if (prazoInterno is { } prazo)
        {
            entrada.Property(t => t.PrazoInterno).CurrentValue = prazo;
        }

        await contexto.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Executa a carga inicial do cenário sem tarefas, para que as próximas tarefas entrem como novas
    /// </summary>
    protected async Task ConcluirCargaInicialAsync()
    {
        foreach (var workspace in Workspaces)
        {
            Asana.Abertas.TryAdd(workspace.Gid, []);
        }

        await SincronizarAsync();
    }
}
