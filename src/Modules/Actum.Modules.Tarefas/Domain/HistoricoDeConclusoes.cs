using Actum.Modules.Asana.Contracts;

namespace Actum.Modules.Tarefas.Domain;

/// <summary>
/// Completion ("concluiu esta tarefa") found in the Asana history
/// </summary>
/// <param name="StoryGid">Story identifier in the Asana</param>
/// <param name="Em">Local date and time of the completion</param>
/// <param name="Por">Who completed it; <see langword="null"/> when unknown</param>
public sealed record ConclusaoDoHistorico(string StoryGid, DateTime Em, string? Por);

/// <summary>
/// Rework found in the Asana history: a completion followed later by "marcou como não concluída"
/// </summary>
/// <param name="ConcluidaEm">Local date and time of the completion of the pair</param>
/// <param name="ReaberturaGid">Story identifier of the reopening in the Asana</param>
/// <param name="ReabertaEm">Local date and time of the reopening</param>
/// <param name="ReabertaPor">Who reopened it; <see langword="null"/> when unknown</param>
/// <param name="ResolvidoEm">Next completion after the reopening; <see langword="null"/> while not completed again</param>
public sealed record ParDoHistorico(DateTime ConcluidaEm, string ReaberturaGid, DateTime ReabertaEm, string? ReabertaPor, DateTime? ResolvidoEm);

/// <summary>
/// Completions and reworks of a task, derived from its Asana history: each reopening preceded by a completion (since
/// the previous reopening) is a rework; a reopening without a completion before it is not.
/// </summary>
/// <param name="Conclusoes">Completions, in chronological order</param>
/// <param name="Pares">Reworks, in chronological order of the reopening</param>
public sealed record HistoricoDeConclusoes(IReadOnlyList<ConclusaoDoHistorico> Conclusoes, IReadOnlyList<ParDoHistorico> Pares)
{
    /// <summary>
    /// Derive the completions and reworks from the task history
    /// </summary>
    /// <param name="stories">Task history in the Asana, in any order</param>
    /// <returns>Completions and reworks in chronological order</returns>
    public static HistoricoDeConclusoes Calcular(IEnumerable<AsanaStory> stories)
    {
        var conclusoes = new List<ConclusaoDoHistorico>();
        var pares = new List<ParDoHistorico>();
        ConclusaoDoHistorico? pendente = null;

        var ordenadas = stories
            .Where(story => story.Tipo is AsanaStoryTipo.MarcadaConcluida or AsanaStoryTipo.MarcadaIncompleta)
            .OrderBy(story => story.CriadaEm)
            .ThenBy(story => story.Gid, StringComparer.Ordinal);

        foreach (var story in ordenadas)
        {
            if (story.Tipo == AsanaStoryTipo.MarcadaConcluida)
            {
                var conclusao = new ConclusaoDoHistorico(story.Gid, story.CriadaEm, story.AutorNome);
                conclusoes.Add(conclusao);

                // A primeira conclusão depois de uma reabertura resolve aquele retrabalho.
                if (pares.Count > 0 && pares[^1].ResolvidoEm is null && pendente is null)
                {
                    pares[^1] = pares[^1] with { ResolvidoEm = story.CriadaEm };
                }

                pendente = conclusao;
            }
            else if (pendente is not null)
            {
                pares.Add(new ParDoHistorico(pendente.Em, story.Gid, story.CriadaEm, story.AutorNome, null));
                pendente = null;
            }
        }

        return new HistoricoDeConclusoes(conclusoes, pares);
    }
}
