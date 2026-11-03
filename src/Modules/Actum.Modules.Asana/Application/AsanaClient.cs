using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Actum.Modules.Asana.Contracts;
using Actum.Modules.Asana.Errors;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Actum.Modules.Asana.Application;

/// <summary>
/// <see cref="IAsanaClient"/> over the Asana REST API (<c>https://app.asana.com/api/1.0</c>), authenticated by
/// the Personal Access Token in <c>Asana:Token</c>. Follows <c>next_page</c> to read every page, retries
/// <c>429</c> responses after <c>Retry-After</c> and translates failures into <see cref="AsanaException"/>.
/// </summary>
/// <remarks>
/// The token is read on every operation (a token configured later is picked up without restart) and is never
/// logged: the module registers the typed <see cref="HttpClient"/> with every header value redacted.
/// </remarks>
internal sealed class AsanaClient(HttpClient httpClient, IConfiguration configuration, ILogger<AsanaClient> logger) : IAsanaClient
{
    public const string TokenKey = "Asana:Token";

    public const int MaximoDeNovasTentativas = 3;

    public static readonly Uri EnderecoBase = new("https://app.asana.com/api/1.0/");

    public static readonly TimeSpan TempoLimite = TimeSpan.FromSeconds(30);

    private const string CamposDaTarefa =
        "name,notes,html_notes,due_on,completed,modified_at,permalink_url,projects.name,tags.name,assignee";

    private const int TamanhoDaPagina = 100;

    private static readonly TimeSpan EsperaPadraoEm429 = TimeSpan.FromSeconds(1);

    private static readonly JsonSerializerOptions OpcoesJson = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    /// <summary>
    /// Base address and timeout of the Asana <see cref="HttpClient"/>
    /// </summary>
    public static void Configurar(HttpClient cliente)
    {
        cliente.BaseAddress = EnderecoBase;
        cliente.Timeout = TempoLimite;
    }

    public async Task<AsanaUsuario> ObterUsuarioAtualAsync(CancellationToken cancellationToken)
    {
        var envelope = await EnviarAsync<UsuarioJson>(HttpMethod.Get, "users/me?opt_fields=name,workspaces.name", null, false, cancellationToken);
        var usuario = Dados(envelope);

        var workspaces = (usuario.Workspaces ?? [])
            .Select(workspace => new AsanaWorkspace(workspace.Gid, workspace.Name ?? string.Empty))
            .ToList();

        return new AsanaUsuario(usuario.Gid, usuario.Name ?? string.Empty, workspaces);
    }

    public async Task<IReadOnlyList<AsanaTarefa>> ListarTarefasAbertasAsync(string workspaceGid, CancellationToken cancellationToken)
    {
        var caminho = $"tasks?assignee=me&workspace={Escapar(workspaceGid)}&completed_since=now&limit={TamanhoDaPagina}&opt_fields={CamposDaTarefa}";
        var tarefas = await ListarTodasAsPaginasAsync<TarefaJson>(caminho, cancellationToken);

        return tarefas.Select(ParaTarefa).ToList();
    }

    public async Task<AsanaTarefa?> ObterTarefaAsync(string tarefaGid, CancellationToken cancellationToken)
    {
        var envelope = await EnviarAsync<TarefaJson>(
            HttpMethod.Get, $"tasks/{Escapar(tarefaGid)}?opt_fields={CamposDaTarefa}", null, true, cancellationToken);

        return envelope is null ? null : ParaTarefa(Dados(envelope));
    }

    public async Task<IReadOnlyList<AsanaStory>> ListarStoriesAsync(string tarefaGid, CancellationToken cancellationToken)
    {
        var caminho = $"tasks/{Escapar(tarefaGid)}/stories?limit={TamanhoDaPagina}&opt_fields=resource_subtype,created_at,created_by.name,text";
        var stories = await ListarTodasAsPaginasAsync<StoryJson>(caminho, cancellationToken);

        return stories
            .Select(story => new AsanaStory(
                story.Gid,
                ParaTipo(story.ResourceSubtype),
                ParaHorarioLocal(story.CreatedAt),
                story.CreatedBy?.Name,
                story.Text ?? string.Empty))
            .ToList();
    }

    public async Task<AsanaTag> ObterOuCriarTagAsync(string workspaceGid, string nome, CancellationToken cancellationToken)
    {
        var tags = await ListarTodasAsPaginasAsync<RecursoJson>(
            $"workspaces/{Escapar(workspaceGid)}/tags?limit={TamanhoDaPagina}&opt_fields=name", cancellationToken);

        // O nome da tag é o nome da branch, e branches do Git diferenciam maiúsculas.
        var existente = tags.FirstOrDefault(tag => string.Equals(tag.Name, nome, StringComparison.Ordinal));
        if (existente is not null)
        {
            return new AsanaTag(existente.Gid, nome);
        }

        var envelope = await EnviarAsync<RecursoJson>(
            HttpMethod.Post, $"workspaces/{Escapar(workspaceGid)}/tags", new { name = nome }, false, cancellationToken);
        var criada = Dados(envelope);

        return new AsanaTag(criada.Gid, criada.Name ?? nome);
    }

    public async Task AdicionarTagAsync(string tarefaGid, string tagGid, CancellationToken cancellationToken)
    {
        await EnviarAsync<JsonElement>(
            HttpMethod.Post, $"tasks/{Escapar(tarefaGid)}/addTag", new { tag = tagGid }, false, cancellationToken);
    }

    public async Task ConcluirTarefaAsync(string tarefaGid, CancellationToken cancellationToken)
    {
        await EnviarAsync<JsonElement>(
            HttpMethod.Put, $"tasks/{Escapar(tarefaGid)}", new { completed = true }, false, cancellationToken);
    }

    private async Task<List<T>> ListarTodasAsPaginasAsync<T>(string caminho, CancellationToken cancellationToken)
    {
        var itens = new List<T>();
        string? offset = null;

        do
        {
            var pagina = offset is null ? caminho : $"{caminho}&offset={Escapar(offset)}";
            var envelope = await EnviarAsync<List<T>>(HttpMethod.Get, pagina, null, false, cancellationToken);

            itens.AddRange(Dados(envelope));
            offset = envelope?.NextPage?.Offset;
        }
        while (!string.IsNullOrEmpty(offset));

        return itens;
    }

    /// <summary>
    /// Send the request, retrying <c>429</c>, and read the <c>data</c> envelope. Returns <see langword="null"/>
    /// on <c>404</c> only when <paramref name="nuloSeNaoEncontrado"/> is set.
    /// </summary>
    private async Task<Envelope<T>?> EnviarAsync<T>(
        HttpMethod metodo,
        string caminho,
        object? dados,
        bool nuloSeNaoEncontrado,
        CancellationToken cancellationToken)
    {
        var token = configuration[TokenKey];
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new AsanaNaoConfiguradoException();
        }

        for (var novaTentativa = 0; ; novaTentativa++)
        {
            using var requisicao = new HttpRequestMessage(metodo, caminho);
            requisicao.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            requisicao.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            if (dados is not null)
            {
                requisicao.Content = JsonContent.Create(new { data = dados }, options: OpcoesJson);
            }

            try
            {
                using var resposta = await httpClient.SendAsync(requisicao, cancellationToken);

                if (resposta.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    if (novaTentativa >= MaximoDeNovasTentativas)
                    {
                        logger.LogWarning("Limite de requisições do Asana esgotado após {Tentativas} novas tentativas: {Metodo} {Caminho}", novaTentativa, metodo, CaminhoSemConsulta(caminho));
                        throw new AsanaIndisponivelException();
                    }

                    var espera = TempoDeEspera(resposta.Headers.RetryAfter);
                    logger.LogInformation("Asana respondeu 429; nova tentativa em {Espera}: {Metodo} {Caminho}", espera, metodo, CaminhoSemConsulta(caminho));
                    await Task.Delay(espera, cancellationToken);
                    continue;
                }

                if (resposta.StatusCode == HttpStatusCode.NotFound && nuloSeNaoEncontrado)
                {
                    return null;
                }

                if (resposta.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                {
                    logger.LogWarning("Asana recusou o token ({Status}): {Metodo} {Caminho}", (int)resposta.StatusCode, metodo, CaminhoSemConsulta(caminho));
                    throw new AsanaAcessoNegadoException();
                }

                if (!resposta.IsSuccessStatusCode)
                {
                    logger.LogWarning("Asana respondeu {Status}: {Metodo} {Caminho}", (int)resposta.StatusCode, metodo, CaminhoSemConsulta(caminho));
                    throw new AsanaIndisponivelException();
                }

                return await resposta.Content.ReadFromJsonAsync<Envelope<T>>(OpcoesJson, cancellationToken);
            }
            catch (HttpRequestException ex)
            {
                throw new AsanaIndisponivelException(ex);
            }
            catch (JsonException ex)
            {
                throw new AsanaIndisponivelException(ex);
            }
            catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
            {
                // Tempo limite do HttpClient.
                throw new AsanaIndisponivelException(ex);
            }
        }
    }

    private static T Dados<T>(Envelope<T>? envelope)
    {
        if (envelope is null || envelope.Data is not { } dados)
        {
            throw new AsanaIndisponivelException();
        }

        return dados;
    }

    private static TimeSpan TempoDeEspera(RetryConditionHeaderValue? retryAfter)
    {
        var espera = retryAfter?.Delta
            ?? (retryAfter?.Date is { } data ? data - DateTimeOffset.UtcNow : EsperaPadraoEm429);

        return espera < TimeSpan.Zero ? TimeSpan.Zero : espera;
    }

    private static AsanaTarefa ParaTarefa(TarefaJson tarefa)
    {
        return new AsanaTarefa(
            tarefa.Gid,
            tarefa.Name ?? string.Empty,
            tarefa.Notes ?? string.Empty,
            tarefa.HtmlNotes ?? string.Empty,
            tarefa.DueOn,
            tarefa.Completed,
            ParaHorarioLocal(tarefa.ModifiedAt),
            tarefa.PermalinkUrl ?? string.Empty,
            (tarefa.Projects ?? []).Select(projeto => projeto.Name ?? string.Empty).ToList(),
            (tarefa.Tags ?? []).Select(tag => new AsanaTag(tag.Gid, tag.Name ?? string.Empty)).ToList(),
            tarefa.Assignee?.Gid);
    }

    private static AsanaStoryTipo ParaTipo(string? resourceSubtype)
    {
        return resourceSubtype switch
        {
            "comment_added" => AsanaStoryTipo.Comentario,
            "marked_incomplete" => AsanaStoryTipo.MarcadaIncompleta,
            "marked_complete" => AsanaStoryTipo.MarcadaConcluida,
            _ => AsanaStoryTipo.Outro,
        };
    }

    /// <summary>
    /// The Asana answers in UTC; the database stores local time without time zone
    /// </summary>
    private static DateTime ParaHorarioLocal(DateTimeOffset valor)
    {
        return DateTime.SpecifyKind(valor.ToLocalTime().DateTime, DateTimeKind.Unspecified);
    }

    private static string Escapar(string valor)
    {
        return Uri.EscapeDataString(valor);
    }

    private static string CaminhoSemConsulta(string caminho)
    {
        var inicioDaConsulta = caminho.IndexOf('?', StringComparison.Ordinal);
        return inicioDaConsulta < 0 ? caminho : caminho[..inicioDaConsulta];
    }

    private sealed record Envelope<T>(T? Data, Pagina? NextPage);

    private sealed record Pagina(string? Offset);

    private sealed record RecursoJson(string Gid, string? Name);

    private sealed record UsuarioJson(string Gid, string? Name, List<RecursoJson>? Workspaces);

    private sealed record TarefaJson(
        string Gid,
        string? Name,
        string? Notes,
        string? HtmlNotes,
        DateOnly? DueOn,
        bool Completed,
        DateTimeOffset ModifiedAt,
        string? PermalinkUrl,
        List<RecursoJson>? Projects,
        List<RecursoJson>? Tags,
        RecursoJson? Assignee);

    private sealed record StoryJson(
        string Gid,
        string? ResourceSubtype,
        DateTimeOffset CreatedAt,
        RecursoJson? CreatedBy,
        string? Text);
}
