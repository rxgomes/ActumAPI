using Actum.Modules.Claude.Contracts;

namespace Actum.Modules.Claude.Application;

/// <summary>
/// Rules of the files that the AI analysis can read (types and size limits of the <c>Claude:Arquivos</c> section)
/// </summary>
/// <author>Ricardo Gomes</author>
/// <created>2026-11-18</created>
/// <modified>2026-11-18</modified>
public interface IArquivosDaAnalise
{
    /// <summary>
    /// Tell whether a file can be read by the analysis and, if not, why (external file, unsupported type or larger
    /// than the limit of its type). A file of unknown size is readable; its size is checked after the download.
    /// </summary>
    /// <param name="nome">File name (the type comes from the extension)</param>
    /// <param name="tamanho">Size in bytes; <see langword="null"/> when unknown</param>
    /// <param name="hospedagem">Where the file is stored (<c>asana</c>, <c>gdrive</c>, <c>dropbox</c>...)</param>
    /// <returns>Whether it is readable, the reason in pt-BR and how it is read</returns>
    /// <author>Ricardo Gomes</author>
    /// <created>2026-11-18</created>
    /// <modified>2026-11-18</modified>
    LegibilidadeDoArquivo Avaliar(string nome, long? tamanho, string hospedagem);

    /// <summary>
    /// Maximum size of one file of the given name, by its type
    /// </summary>
    /// <param name="nome">File name</param>
    /// <returns>Maximum size in bytes</returns>
    /// <author>Ricardo Gomes</author>
    /// <created>2026-11-18</created>
    /// <modified>2026-11-18</modified>
    long TamanhoMaximo(string nome);

    /// <summary>
    /// Maximum sum of the sizes of the files of one analysis, in bytes
    /// </summary>
    /// <author>Ricardo Gomes</author>
    /// <created>2026-11-18</created>
    /// <modified>2026-11-18</modified>
    long TamanhoTotalMaximo { get; }

    /// <summary>
    /// Message, in pt-BR, for when the files of one analysis pass <see cref="TamanhoTotalMaximo"/>
    /// </summary>
    /// <author>Ricardo Gomes</author>
    /// <created>2026-11-18</created>
    /// <modified>2026-11-18</modified>
    string MensagemDoTotalMaximo { get; }
}
