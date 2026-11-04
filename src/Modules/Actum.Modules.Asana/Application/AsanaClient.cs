using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security;
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

    public static readonly TimeSpan TempoLimiteDoUpload = TimeSpan.FromMinutes(5);

    private const string CamposDaTarefa =
        "name,notes,html_notes,due_on,completed,modified_at,permalink_url,projects.name,tags.name,assignee";

    private const int TamanhoDaPagina = 100;

    private static readonly TimeSpan EsperaPadraoEm429 = TimeSpan.FromSeconds(1);

    private static readonly JsonSerializerOptions OpcoesJson = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    /// <summary>
    /// Base address of the Asana <see cref="HttpClient"/>. The time limit is set per call (<see cref="TempoLimite"/>,
    /// or <see cref="TempoLimiteDoUpload"/> for attachments), so the client itself has none.
    /// </summary>
    public static void Configurar(HttpClient cliente)
    {
        cliente.BaseAddress = EnderecoBase;
        cliente.Timeout = Timeout.InfiniteTimeSpan;
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

    public async Task<IReadOnlyList<AsanaUsuarioResumo>?> ListarParticipantesAsync(string tarefaGid, CancellationToken cancellationToken)
    {
        var envelope = await EnviarAsync<SeguidoresJson>(
            HttpMethod.Get, $"tasks/{Escapar(tarefaGid)}?opt_fields=followers.name", null, true, cancellationToken);
        if (envelope is null)
        {
            return null;
        }

        return (Dados(envelope).Followers ?? [])
            .Select(seguidor => new AsanaUsuarioResumo(seguidor.Gid, seguidor.Name ?? string.Empty))
            .ToList();
    }

    public async Task<AsanaAnexo> AnexarArquivoAsync(string tarefaGid, string nome, string tipo, Stream conteudo, CancellationToken cancellationToken)
    {
        // O corpo é refeito a cada nova tentativa em 429; o arquivo (até 25 MB) fica em memória uma vez.
        byte[] bytes;
        using (var memoria = new MemoryStream())
        {
            await conteudo.CopyToAsync(memoria, cancellationToken);
            bytes = memoria.ToArray();
        }

        HttpContent CriarConteudo()
        {
            var arquivo = new ByteArrayContent(bytes);
            arquivo.Headers.ContentType = MediaTypeHeaderValue.TryParse(tipo, out var tipoDoArquivo)
                ? tipoDoArquivo
                : new MediaTypeHeaderValue("application/octet-stream");

            return new MultipartFormDataContent
            {
                { new StringContent(tarefaGid), "parent" },
                { arquivo, "file", nome },
            };
        }

        var envelope = await EnviarConteudoAsync<RecursoJson>(
            HttpMethod.Post, "attachments?opt_fields=name", CriarConteudo, false, true, TempoLimiteDoUpload, cancellationToken);
        var anexo = Dados(envelope);

        return new AsanaAnexo(anexo.Gid, anexo.Name ?? nome);
    }

    public async Task<AsanaStory> ComentarAsync(string tarefaGid, string texto, IReadOnlyList<string> mencoes, CancellationToken cancellationToken)
    {
        var dados = new { html_text = HtmlDoComentario(texto, mencoes) };
        HttpContent CriarConteudo() => JsonContent.Create(new { data = dados });

        var envelope = await EnviarConteudoAsync<StoryJson>(
            HttpMethod.Post,
            $"tasks/{Escapar(tarefaGid)}/stories?opt_fields=resource_subtype,created_at,created_by.name,text",
            CriarConteudo,
            false,
            true,
            TempoLimite,
            cancellationToken);
        var story = Dados(envelope);

        return new AsanaStory(
            story.Gid,
            AsanaStoryTipo.Comentario,
            ParaHorarioLocal(story.CreatedAt),
            story.CreatedBy?.Name,
            story.Text ?? texto);
    }

    /// <summary>
    /// Rich text of a comment: the mentions first, then the text escaped (published exactly as typed, with its line
    /// breaks)
    /// </summary>
    internal static string HtmlDoComentario(string texto, IReadOnlyList<string> mencoes)
    {
        var partes = mencoes
            .Select(gid => $"<a data-asana-gid=\"{SecurityElement.Escape(gid)}\"/>")
            .ToList();

        if (!string.IsNullOrEmpty(texto))
        {
            partes.Add(SecurityElement.Escape(texto) ?? string.Empty);
        }

        return $"<body>{string.Join(' ', partes)}</body>";
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
    /// Send a JSON request (body in the <c>data</c> envelope), retrying <c>429</c>, and read the <c>data</c> envelope.
    /// Returns <see langword="null"/> on <c>404</c> only when <paramref name="nuloSeNaoEncontrado"/> is set.
    /// </summary>
    private Task<Envelope<T>?> EnviarAsync<T>(
        HttpMethod metodo,
        string caminho,
        object? dados,
        bool nuloSeNaoEncontrado,
        CancellationToken cancellationToken)
    {
        Func<HttpContent>? criarConteudo = dados is null
            ? null
            : () => JsonContent.Create(new { data = dados }, options: OpcoesJson);

        return EnviarConteudoAsync<T>(metodo, caminho, criarConteudo, nuloSeNaoEncontrado, false, TempoLimite, cancellationToken);
    }

    /// <summary>
    /// Send a request with the content built by <paramref name="criarConteudo"/> (rebuilt on each retry of
    /// <c>429</c>), with its own time limit. In a write (<paramref name="escrita"/>), <c>400</c>, <c>404</c>,
    /// <c>413</c> and <c>422</c> become <see cref="AsanaRequisicaoRecusadaException"/>.
    /// </summary>
    private async Task<Envelope<T>?> EnviarConteudoAsync<T>(
        HttpMethod metodo,
        string caminho,
        Func<HttpContent>? criarConteudo,
        bool nuloSeNaoEncontrado,
        bool escrita,
        TimeSpan tempoLimite,
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
            if (criarConteudo is not null)
            {
                requisicao.Content = criarConteudo();
            }

            using var limite = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            limite.CancelAfter(tempoLimite);

            try
            {
                using var resposta = await httpClient.SendAsync(requisicao, limite.Token);

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

                if (escrita && MensagemDeRecusa(resposta.StatusCode) is { } recusa)
                {
                    logger.LogWarning("Asana recusou a escrita ({Status}): {Metodo} {Caminho}", (int)resposta.StatusCode, metodo, CaminhoSemConsulta(caminho));
                    throw new AsanaRequisicaoRecusadaException(recusa);
                }

                if (!resposta.IsSuccessStatusCode)
                {
                    logger.LogWarning("Asana respondeu {Status}: {Metodo} {Caminho}", (int)resposta.StatusCode, metodo, CaminhoSemConsulta(caminho));
                    throw new AsanaIndisponivelException();
                }

                return await resposta.Content.ReadFromJsonAsync<Envelope<T>>(OpcoesJson, limite.Token);
            }
            catch (HttpRequestException ex)
            {
                throw new AsanaIndisponivelException(ex);
            }
            catch (JsonException ex)
            {
                throw new AsanaIndisponivelException(ex);
            }
            catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
            {
                // Tempo limite da chamada.
                throw new AsanaIndisponivelException(ex);
            }
        }
    }

    private static string? MensagemDeRecusa(HttpStatusCode status)
    {
        return status switch
        {
            HttpStatusCode.NotFound => "A tarefa não existe mais no Asana.",
            HttpStatusCode.RequestEntityTooLarge => "O Asana recusou o arquivo por ser grande demais.",
            HttpStatusCode.BadRequest or HttpStatusCode.UnprocessableEntity => "O Asana recusou o comentário ou o anexo. Revise o conteúdo e tente novamente.",
            _ => null,
        };
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

    private sealed record SeguidoresJson(List<RecursoJson>? Followers);
}
