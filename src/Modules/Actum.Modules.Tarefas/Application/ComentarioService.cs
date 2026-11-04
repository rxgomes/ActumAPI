using Actum.Modules.Asana.Application;
using Actum.Modules.Asana.Contracts;
using Actum.Modules.Asana.Errors;
using Actum.Modules.Tarefas.Contracts;
using Actum.Modules.Tarefas.Domain;
using Actum.Modules.Tarefas.Persistence;
using Actum.Shared.Abstractions.Domain;
using Microsoft.EntityFrameworkCore;

namespace Actum.Modules.Tarefas.Application;

/// <summary>
/// <see cref="IComentarioService"/> over <see cref="TarefasDbContext"/> and the public <see cref="IAsanaClient"/>
/// contract of the Asana module
/// </summary>
internal sealed class ComentarioService(TarefasDbContext context, IAsanaClient asana) : IComentarioService
{
    public const int MaximoDeArquivos = 10;

    public const long TamanhoMaximoDoArquivo = 25L * 1024 * 1024;

    public const long TamanhoMaximoDoEnvio = 50L * 1024 * 1024;

    public const int TamanhoMaximoDoTexto = 10_000;

    private const string TarefaExcluidaNoAsana = "A tarefa não existe mais no Asana.";

    public async Task<IReadOnlyList<ParticipanteResponse>?> ListarParticipantesAsync(Guid tarefaIdentifier, CancellationToken cancellationToken)
    {
        var tarefa = await context.Set<Tarefa>()
            .AsNoTracking()
            .Include(tarefa => tarefa.Comentarios)
            .Include(tarefa => tarefa.Retrabalhos)
            .AsSplitQuery()
            .SingleOrDefaultAsync(tarefa => tarefa.Identifier == tarefaIdentifier, cancellationToken);
        if (tarefa is null)
        {
            return null;
        }

        var participantes = await ParticipantesAsync(tarefa, cancellationToken);
        var usuario = await asana.ObterUsuarioAtualAsync(cancellationToken);
        var sugerido = NomeSugerido(tarefa, usuario.Nome);

        return participantes
            .Where(participante => participante.Gid != usuario.Gid)
            .Select(participante => new ParticipanteResponse(
                participante.Gid,
                participante.Nome,
                sugerido is not null && Analise.Normalizar(participante.Nome) == sugerido))
            .ToList();
    }

    public async Task<ComentarioEnviadoResponse?> EnviarAsync(
        Guid tarefaIdentifier,
        string? texto,
        IReadOnlyList<string> mencoes,
        IReadOnlyList<ArquivoParaEnviar> arquivos,
        CancellationToken cancellationToken)
    {
        var tarefa = await context.Set<Tarefa>()
            .Include(tarefa => tarefa.Comentarios)
            .SingleOrDefaultAsync(tarefa => tarefa.Identifier == tarefaIdentifier, cancellationToken);
        if (tarefa is null)
        {
            return null;
        }

        var textoLimpo = texto?.Trim() ?? string.Empty;
        var mencoesUnicas = mencoes.Where(gid => !string.IsNullOrWhiteSpace(gid)).Distinct().ToList();
        Validar(tarefa, textoLimpo, arquivos);

        if (mencoesUnicas.Count > 0)
        {
            var participantes = await ParticipantesAsync(tarefa, cancellationToken);
            if (mencoesUnicas.Any(gid => participantes.All(participante => participante.Gid != gid)))
            {
                throw new DomainException("Só é possível mencionar participantes da tarefa no Asana.");
            }
        }

        var anexos = new List<string>();
        AsanaStory story;
        try
        {
            foreach (var arquivo in arquivos)
            {
                await using var conteudo = arquivo.Abrir();
                var anexo = await asana.AnexarArquivoAsync(tarefa.AsanaGid, arquivo.Nome, arquivo.Tipo, conteudo, cancellationToken);
                anexos.Add(anexo.Nome);
            }

            story = await asana.ComentarAsync(tarefa.AsanaGid, TextoDoComentario(textoLimpo, anexos), mencoesUnicas, cancellationToken);
        }
        catch (AsanaException erro) when (anexos.Count > 0)
        {
            throw new EnvioParcialException(anexos, erro);
        }

        tarefa.AdicionarComentarios([story], lidos: true);
        await context.SaveChangesAsync(cancellationToken);

        var comentario = tarefa.Comentarios.Single(comentario => comentario.AsanaStoryGid == story.Gid);
        return new ComentarioEnviadoResponse(
            new ComentarioResponse(comentario.Identifier, comentario.Autor, comentario.Texto, comentario.CriadoEm, comentario.Lido),
            anexos);
    }

    /// <summary>
    /// Comment text: the user text and, when there are attachments, the line with their names
    /// </summary>
    internal static string TextoDoComentario(string texto, IReadOnlyList<string> anexos)
    {
        if (anexos.Count == 0)
        {
            return texto;
        }

        var linha = $"Anexos: {string.Join(", ", anexos)}";
        return texto.Length == 0 ? linha : $"{texto}\n\n{linha}";
    }

    private static void Validar(Tarefa tarefa, string texto, IReadOnlyList<ArquivoParaEnviar> arquivos)
    {
        if (tarefa.Status == StatusTarefa.Arquivada)
        {
            throw new DomainException("Tarefas arquivadas não recebem comentários.");
        }

        if (texto.Length == 0 && arquivos.Count == 0)
        {
            throw new DomainException("Escreva um comentário ou anexe um arquivo.");
        }

        if (texto.Length > TamanhoMaximoDoTexto)
        {
            throw new DomainException($"O comentário deve ter no máximo {TamanhoMaximoDoTexto} caracteres.");
        }

        if (arquivos.Count > MaximoDeArquivos)
        {
            throw new DomainException($"Envie no máximo {MaximoDeArquivos} arquivos por vez.");
        }

        foreach (var arquivo in arquivos)
        {
            if (arquivo.Tamanho == 0)
            {
                throw new DomainException($"O arquivo {arquivo.Nome} está vazio.");
            }

            if (arquivo.Tamanho > TamanhoMaximoDoArquivo)
            {
                throw new DomainException($"O arquivo {arquivo.Nome} tem mais de 25 MB, o limite por arquivo.");
            }
        }

        if (arquivos.Sum(arquivo => arquivo.Tamanho) > TamanhoMaximoDoEnvio)
        {
            throw new DomainException("Os arquivos somam mais de 50 MB, o limite por envio.");
        }
    }

    private async Task<IReadOnlyList<AsanaUsuarioResumo>> ParticipantesAsync(Tarefa tarefa, CancellationToken cancellationToken)
    {
        return await asana.ListarParticipantesAsync(tarefa.AsanaGid, cancellationToken)
            ?? throw new DomainException(TarefaExcluidaNoAsana);
    }

    /// <summary>
    /// Normalized name of who reopened the task most recently or, without reopening, of who last commented (other
    /// than the user); <see langword="null"/> when there is no one
    /// </summary>
    private static string? NomeSugerido(Tarefa tarefa, string nomeDoUsuario)
    {
        var usuario = Analise.Normalizar(nomeDoUsuario);

        var reabertura = tarefa.Retrabalhos
            .OrderByDescending(retrabalho => retrabalho.Numero)
            .Select(retrabalho => retrabalho.ReabertaPor)
            .FirstOrDefault(nome => !string.IsNullOrWhiteSpace(nome) && Analise.Normalizar(nome) != usuario);
        if (reabertura is not null)
        {
            return Analise.Normalizar(reabertura);
        }

        var comentario = tarefa.Comentarios
            .OrderByDescending(comentario => comentario.CriadoEm)
            .Select(comentario => comentario.Autor)
            .FirstOrDefault(nome => !string.IsNullOrWhiteSpace(nome) && Analise.Normalizar(nome) != usuario);

        return comentario is null ? null : Analise.Normalizar(comentario);
    }
}
