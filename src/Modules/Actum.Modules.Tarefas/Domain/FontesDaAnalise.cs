namespace Actum.Modules.Tarefas.Domain;

/// <summary>
/// How the sources of an analysis were chosen (stored as snake_case text: <c>tarefa</c> or <c>prompt</c>)
/// </summary>
public enum ModoDaAnalise
{
    /// <summary>
    /// Content of the task: description, comments and/or attachments
    /// </summary>
    Tarefa,

    /// <summary>
    /// Request written by the user, in place of the description and the attachments, with the files it cites
    /// </summary>
    Prompt,
}

/// <summary>
/// Sources used by one version of the analysis
/// </summary>
/// <param name="Modo">How the sources were chosen</param>
/// <param name="Descricao">Whether the description was sent</param>
/// <param name="Comentarios">Whether the comments (and the reopenings) were sent</param>
/// <param name="Prompt">Request written by the user; <see langword="null"/> outside the prompt mode</param>
/// <param name="Anexos">Attachments sent (Asana identifier and name), in order</param>
public sealed record FontesDaAnalise(
    ModoDaAnalise Modo,
    bool Descricao,
    bool Comentarios,
    string? Prompt,
    IReadOnlyList<(string Gid, string Nome)> Anexos)
{
    /// <summary>
    /// Sources of an analysis requested without a body: description and comments
    /// </summary>
    public static FontesDaAnalise Padrao { get; } = new(ModoDaAnalise.Tarefa, true, true, null, []);
}

/// <summary>
/// Attachment sent as a source of one version of the analysis (owned by <see cref="Analise"/>)
/// </summary>
public sealed class AnaliseFonteAnexo
{
    public const int GidMaxLength = 50;

    public const int NomeMaxLength = 500;

    private AnaliseFonteAnexo()
    {
    }

    internal AnaliseFonteAnexo(short ordem, string anexoGid, string nome)
    {
        Ordem = ordem;
        AnexoGid = anexoGid;
        Nome = nome.Length > NomeMaxLength ? nome[..NomeMaxLength] : nome;
    }

    /// <summary>
    /// Position among the attachments of the version, starting at 1
    /// </summary>
    public short Ordem { get; private set; }

    /// <summary>
    /// Attachment identifier in the Asana
    /// </summary>
    public string AnexoGid { get; private set; } = string.Empty;

    /// <summary>
    /// File name at the time of the analysis
    /// </summary>
    public string Nome { get; private set; } = string.Empty;
}
