using Actum.Modules.Asana.Application;
using Actum.Modules.Asana.Contracts;

namespace Actum.Modules.Tarefas.Tests;

/// <summary>
/// <see cref="IAsanaClient"/> em memória, configurado por cenário: o usuário e seus workspaces, as tarefas abertas
/// de cada workspace, a situação de cada tarefa e o histórico. Registra as consultas feitas.
/// </summary>
public sealed class AsanaClientFalso : IAsanaClient
{
    private readonly Lock _trava = new();

    public AsanaClientFalso(AsanaUsuario usuario)
    {
        Usuario = usuario;
    }

    /// <summary>
    /// Usuário autenticado e seus workspaces
    /// </summary>
    public AsanaUsuario Usuario { get; set; }

    /// <summary>
    /// Tarefas abertas atribuídas ao usuário, por workspace
    /// </summary>
    public Dictionary<string, List<AsanaTarefa>> Abertas { get; } = [];

    /// <summary>
    /// Situação de cada tarefa para <see cref="ObterTarefaAsync"/>; gid ausente significa tarefa excluída (404)
    /// </summary>
    public Dictionary<string, AsanaTarefa> Tarefas { get; } = [];

    /// <summary>
    /// Histórico de cada tarefa
    /// </summary>
    public Dictionary<string, List<AsanaStory>> Stories { get; } = [];

    /// <summary>
    /// Workspace cuja listagem de tarefas falha com a exceção informada
    /// </summary>
    public (string Workspace, Exception Erro)? Falha { get; set; }

    /// <summary>
    /// Quando definido, a listagem de tarefas aguarda esta tarefa antes de responder (para simular uma
    /// sincronização em andamento)
    /// </summary>
    public Task? BloquearListagem { get; set; }

    /// <summary>
    /// Avisado quando a listagem de tarefas começa
    /// </summary>
    public TaskCompletionSource ListagemIniciada { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public List<string> WorkspacesListados { get; } = [];

    public List<string> TarefasConsultadas { get; } = [];

    public List<string> StoriesConsultadas { get; } = [];

    public Task<AsanaUsuario> ObterUsuarioAtualAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult(Usuario);
    }

    public async Task<IReadOnlyList<AsanaTarefa>> ListarTarefasAbertasAsync(string workspaceGid, CancellationToken cancellationToken)
    {
        lock (_trava)
        {
            WorkspacesListados.Add(workspaceGid);
        }

        ListagemIniciada.TrySetResult();
        if (BloquearListagem is { } bloqueio)
        {
            await bloqueio;
        }

        if (Falha is { } falha && falha.Workspace == workspaceGid)
        {
            throw falha.Erro;
        }

        return Abertas.TryGetValue(workspaceGid, out var tarefas) ? [.. tarefas] : [];
    }

    public Task<AsanaTarefa?> ObterTarefaAsync(string tarefaGid, CancellationToken cancellationToken)
    {
        lock (_trava)
        {
            TarefasConsultadas.Add(tarefaGid);
        }

        return Task.FromResult(Tarefas.GetValueOrDefault(tarefaGid));
    }

    public Task<IReadOnlyList<AsanaStory>> ListarStoriesAsync(string tarefaGid, CancellationToken cancellationToken)
    {
        lock (_trava)
        {
            StoriesConsultadas.Add(tarefaGid);
        }

        IReadOnlyList<AsanaStory> stories = Stories.TryGetValue(tarefaGid, out var lista) ? [.. lista] : [];
        return Task.FromResult(stories);
    }

    public Task<AsanaTag> ObterOuCriarTagAsync(string workspaceGid, string nome, CancellationToken cancellationToken)
    {
        lock (_trava)
        {
            RegistrarEscrita($"tag:{nome}");

            var tags = TagsPorWorkspace.TryGetValue(workspaceGid, out var lista) ? lista : TagsPorWorkspace[workspaceGid] = [];
            var tag = tags.FirstOrDefault(t => string.Equals(t.Nome, nome, StringComparison.Ordinal));
            if (tag is null)
            {
                tag = new AsanaTag($"tag-{Guid.NewGuid():N}", nome);
                tags.Add(tag);
            }

            return Task.FromResult(tag);
        }
    }

    public Task AdicionarTagAsync(string tarefaGid, string tagGid, CancellationToken cancellationToken)
    {
        lock (_trava)
        {
            var tag = TagsPorWorkspace.Values.SelectMany(tags => tags).Single(t => t.Gid == tagGid);
            RegistrarEscrita($"addTag:{tag.Nome}");

            var tarefa = Tarefas[tarefaGid];
            Tarefas[tarefaGid] = tarefa with { Tags = [.. tarefa.Tags, tag] };
            return Task.CompletedTask;
        }
    }

    public Task ConcluirTarefaAsync(string tarefaGid, CancellationToken cancellationToken)
    {
        lock (_trava)
        {
            RegistrarEscrita("concluir");

            Tarefas[tarefaGid] = Tarefas[tarefaGid] with { Concluida = true };
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Tags de cada workspace
    /// </summary>
    public Dictionary<string, List<AsanaTag>> TagsPorWorkspace { get; } = [];

    /// <summary>
    /// Escritas pedidas ao Asana, na ordem: <c>tag:{nome}</c> (obter ou criar), <c>addTag:{nome}</c> e <c>concluir</c>
    /// </summary>
    public List<string> Escritas { get; } = [];

    /// <summary>
    /// Escrita que falha com <see cref="Actum.Modules.Asana.Errors.AsanaIndisponivelException"/> (ex.: <c>addTag:main</c>)
    /// </summary>
    public string? FalharNaEscrita { get; set; }

    private void RegistrarEscrita(string escrita)
    {
        Escritas.Add(escrita);
        if (escrita == FalharNaEscrita)
        {
            throw new Actum.Modules.Asana.Errors.AsanaIndisponivelException();
        }
    }
}
