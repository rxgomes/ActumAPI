using System.Globalization;
using System.Text;
using Actum.Shared.Abstractions.Domain;

namespace Actum.Modules.Tarefas.Domain;

/// <summary>
/// One version of the AI analysis of a task demand: objective, checklist and questions. The most recent version
/// is the current one; the previous ones are read-only history.
/// </summary>
public sealed class Analise : Entity
{
    public const int ModeloMaxLength = 100;

    private readonly List<AnaliseItem> _itens = [];

    private Analise()
    {
    }

    /// <summary>
    /// Analyzed task
    /// </summary>
    public long TarefaId { get; private set; }

    /// <summary>
    /// Sequence of the analysis in the task, starting at 1
    /// </summary>
    public short Versao { get; private set; }

    /// <summary>
    /// Expected result of the task
    /// </summary>
    public string Objetivo { get; private set; } = string.Empty;

    /// <summary>
    /// Model that answered
    /// </summary>
    public string Modelo { get; private set; } = string.Empty;

    /// <summary>
    /// Input tokens consumed
    /// </summary>
    public int TokensEntrada { get; private set; }

    /// <summary>
    /// Output tokens consumed
    /// </summary>
    public int TokensSaida { get; private set; }

    /// <summary>
    /// Local date and time of the analysis
    /// </summary>
    public DateTime AnalisadaEm { get; private set; }

    /// <summary>
    /// Checklist items and questions, in order
    /// </summary>
    public IReadOnlyList<AnaliseItem> Itens => _itens;

    /// <summary>
    /// Create a new version of the analysis. Items with the same type and text (ignoring case, accents and
    /// surrounding spaces) of an item marked in <paramref name="anterior"/> come already marked, with the
    /// original date.
    /// </summary>
    /// <param name="tarefaId">Analyzed task</param>
    /// <param name="anterior">Current version before this one; <see langword="null"/> for the first analysis</param>
    /// <param name="objetivo">Expected result</param>
    /// <param name="checklist">Checklist items (area, text and why it is vague, if it is), grouped, in order</param>
    /// <param name="testes">Tests, in order</param>
    /// <param name="duvidas">Questions, in order</param>
    /// <param name="modelo">Model that answered</param>
    /// <param name="tokensEntrada">Input tokens</param>
    /// <param name="tokensSaida">Output tokens</param>
    /// <param name="analisadaEm">Local date and time of the analysis</param>
    /// <returns>New version, numbered after <paramref name="anterior"/></returns>
    public static Analise Criar(
        long tarefaId,
        Analise? anterior,
        string objetivo,
        IReadOnlyList<(string? Grupo, string Texto, string? Vago)> checklist,
        IReadOnlyList<string> testes,
        IReadOnlyList<string> duvidas,
        string modelo,
        int tokensEntrada,
        int tokensSaida,
        DateTime analisadaEm)
    {
        var analise = new Analise
        {
            TarefaId = tarefaId,
            Versao = (short)((anterior?.Versao ?? 0) + 1),
            Objetivo = objetivo,
            Modelo = modelo.Length > ModeloMaxLength ? modelo[..ModeloMaxLength] : modelo,
            TokensEntrada = tokensEntrada,
            TokensSaida = tokensSaida,
            AnalisadaEm = analisadaEm,
        };

        var marcados = (anterior?.Itens ?? [])
            .Where(item => item.ConcluidoEm is not null)
            .GroupBy(item => (item.Tipo, Normalizar(item.Texto)))
            .ToDictionary(grupo => grupo.Key, grupo => grupo.First().ConcluidoEm);

        analise.AdicionarItens(TipoItemAnalise.Checklist, checklist, marcados);
        analise.AdicionarItens(TipoItemAnalise.Teste, testes.Select(texto => ((string?)null, texto, (string?)null)).ToList(), marcados);
        analise.AdicionarItens(TipoItemAnalise.Duvida, duvidas.Select(texto => ((string?)null, texto, (string?)null)).ToList(), marcados);

        return analise;
    }

    private void AdicionarItens(
        TipoItemAnalise tipo,
        IReadOnlyList<(string? Grupo, string Texto, string? Vago)> itens,
        Dictionary<(TipoItemAnalise, string), DateTime?> marcados)
    {
        for (var indice = 0; indice < itens.Count; indice++)
        {
            var (grupo, texto, vago) = itens[indice];
            marcados.TryGetValue((tipo, Normalizar(texto)), out var concluidoEm);
            _itens.Add(new AnaliseItem(tipo, (short)(indice + 1), texto, grupo, vago, concluidoEm));
        }
    }

    /// <summary>
    /// Text used to compare items: trimmed, lower case and without accents
    /// </summary>
    internal static string Normalizar(string texto)
    {
        var decomposto = texto.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var semAcentos = new StringBuilder(decomposto.Length);
        foreach (var caractere in decomposto)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(caractere) != UnicodeCategory.NonSpacingMark)
            {
                semAcentos.Append(caractere);
            }
        }

        return semAcentos.ToString().Normalize(NormalizationForm.FormC);
    }
}
