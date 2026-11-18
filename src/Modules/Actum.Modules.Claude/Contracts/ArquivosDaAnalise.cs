namespace Actum.Modules.Claude.Contracts;

/// <summary>
/// How a file is read by the analysis
/// </summary>
public enum TipoDeArquivo
{
    /// <summary>
    /// Not readable by the analysis
    /// </summary>
    NaoSuportado,

    /// <summary>
    /// PDF, sent as a native document
    /// </summary>
    Pdf,

    /// <summary>
    /// PNG, JPEG, GIF or WEBP, sent as a native image
    /// </summary>
    Imagem,

    /// <summary>
    /// Plain text (<c>.txt</c>, <c>.md</c>, <c>.csv</c>, <c>.sql</c>...), sent as text
    /// </summary>
    Texto,

    /// <summary>
    /// Word document (<c>.docx</c>), sent as the extracted text
    /// </summary>
    Word,

    /// <summary>
    /// Excel workbook (<c>.xlsx</c>), sent as the extracted text
    /// </summary>
    Excel,
}

/// <summary>
/// Whether a file can be read by the analysis
/// </summary>
/// <param name="Legivel">Whether the file can be a source of the analysis</param>
/// <param name="Motivo">Why it cannot, in pt-BR; <see langword="null"/> when it can</param>
/// <param name="Tipo">How the file is read</param>
public sealed record LegibilidadeDoArquivo(bool Legivel, string? Motivo, TipoDeArquivo Tipo);

/// <summary>
/// File sent to the AI with the demand
/// </summary>
/// <param name="Nome">File name, as in the Asana (the AI cites it as the origin)</param>
/// <param name="Conteudo">File bytes</param>
public sealed record ArquivoDaDemanda(string Nome, byte[] Conteudo);
