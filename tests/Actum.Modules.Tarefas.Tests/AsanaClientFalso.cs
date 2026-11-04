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

        if (tarefaGid == FalharStories)
        {
            throw new Actum.Modules.Asana.Errors.AsanaIndisponivelException();
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

            // O Asana registra a conclusão no histórico da tarefa.
            var conclusao = new AsanaStory($"concluiu-{Guid.NewGuid():N}", AsanaStoryTipo.MarcadaConcluida, DateTime.Now, Usuario.Nome, "concluiu esta tarefa");
            Stories[tarefaGid] = Stories.TryGetValue(tarefaGid, out var stories) ? [.. stories, conclusao] : [conclusao];
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Tags de cada workspace
    /// </summary>
    /// <summary>
    /// Seguidores por tarefa; tarefa ausente responde como excluída no Asana (<see langword="null"/>)
    /// </summary>
    public Dictionary<string, List<AsanaUsuarioResumo>> Participantes { get; } = [];

    /// <summary>
    /// Comentários publicados: tarefa, texto e menções
    /// </summary>
    public List<(string Tarefa, string Texto, IReadOnlyList<string> Mencoes)> Comentarios { get; } = [];

    /// <summary>
    /// Anexos enviados: tarefa, nome, tipo e conteúdo
    /// </summary>
    public List<(string Tarefa, string Nome, string Tipo, byte[] Conteudo)> Anexos { get; } = [];

    public Task<IReadOnlyList<AsanaUsuarioResumo>?> ListarParticipantesAsync(string tarefaGid, CancellationToken cancellationToken)
    {
        IReadOnlyList<AsanaUsuarioResumo>? participantes = Participantes.TryGetValue(tarefaGid, out var lista) ? [.. lista] : null;
        return Task.FromResult(participantes);
    }

    public async Task<AsanaAnexo> AnexarArquivoAsync(string tarefaGid, string nome, string tipo, Stream conteudo, CancellationToken cancellationToken)
    {
        using var memoria = new MemoryStream();
        await conteudo.CopyToAsync(memoria, cancellationToken);

        lock (_trava)
        {
            RegistrarEscrita($"anexo:{nome}");
            Anexos.Add((tarefaGid, nome, tipo, memoria.ToArray()));
            return new AsanaAnexo($"anexo-{Anexos.Count}", nome);
        }
    }

    public Task<AsanaStory> ComentarAsync(string tarefaGid, string texto, IReadOnlyList<string> mencoes, CancellationToken cancellationToken)
    {
        lock (_trava)
        {
            RegistrarEscrita("comentar");
            Comentarios.Add((tarefaGid, texto, mencoes));

            var story = new AsanaStory($"story-{Guid.NewGuid():N}", AsanaStoryTipo.Comentario, new DateTime(2026, 11, 3, 18, 15, 0), Usuario.Nome, texto);
            if (Stories.TryGetValue(tarefaGid, out var stories))
            {
                stories.Add(story);
            }
            else
            {
                Stories[tarefaGid] = [story];
            }

            return Task.FromResult(story);
        }
    }

    /// <summary>
    /// Tarefa cujo histórico falha como indisponível
    /// </summary>
    public string? FalharStories { get; set; }

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
