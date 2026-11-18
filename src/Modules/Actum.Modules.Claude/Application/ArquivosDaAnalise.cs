using Actum.Modules.Claude.Contracts;
using Microsoft.Extensions.Configuration;

namespace Actum.Modules.Claude.Application;

/// <summary>
/// <see cref="IArquivosDaAnalise"/> by the file extension, with the limits of the <c>Claude:Arquivos</c> section
/// (read on every call, so a changed limit is picked up without restart)
/// </summary>
internal sealed class ArquivosDaAnalise(IConfiguration configuration) : IArquivosDaAnalise
{
    public const string Externo = "Arquivos externos não podem ser lidos pela análise.";

    public const string NaoSuportado = "Tipo de arquivo não suportado pela análise.";

    public LegibilidadeDoArquivo Avaliar(string nome, long? tamanho, string hospedagem)
    {
        var tipo = Tipo(nome);
        if (!string.IsNullOrEmpty(hospedagem) && !string.Equals(hospedagem, "asana", StringComparison.OrdinalIgnoreCase))
        {
            return new LegibilidadeDoArquivo(false, Externo, tipo);
        }

        if (tipo == TipoDeArquivo.NaoSuportado)
        {
            return new LegibilidadeDoArquivo(false, NaoSuportado, tipo);
        }

        var maximo = TamanhoMaximo(tipo);
        if (tamanho > maximo)
        {
            return new LegibilidadeDoArquivo(false, $"Arquivo maior que {EmMb(maximo)} MB.", tipo);
        }

        return new LegibilidadeDoArquivo(true, null, tipo);
    }

    public long TamanhoMaximo(string nome) => TamanhoMaximo(Tipo(nome));

    public long TamanhoTotalMaximo => Limites().TotalMaximo;

    public string MensagemDoTotalMaximo => $"Os anexos escolhidos somam mais de {EmMb(TamanhoTotalMaximo)} MB. Escolha menos arquivos.";

    /// <summary>
    /// Type of a file by its extension (case insensitive)
    /// </summary>
    public static TipoDeArquivo Tipo(string nome)
    {
        return Path.GetExtension(nome).TrimStart('.').ToLowerInvariant() switch
        {
            "pdf" => TipoDeArquivo.Pdf,
            "png" or "jpg" or "jpeg" or "gif" or "webp" => TipoDeArquivo.Imagem,
            "docx" => TipoDeArquivo.Word,
            "xlsx" => TipoDeArquivo.Excel,
            "txt" or "md" or "csv" or "tsv" or "json" or "xml" or "sql" or "log" or "yaml" or "yml" or "html" or "htm" => TipoDeArquivo.Texto,
            _ => TipoDeArquivo.NaoSuportado,
        };
    }

    private long TamanhoMaximo(TipoDeArquivo tipo)
    {
        var limites = Limites();
        return tipo == TipoDeArquivo.Imagem ? limites.ImagemMaxima : limites.DocumentoMaximo;
    }

    private LimitesDosArquivos Limites() => LimitesDosArquivos.Ler(configuration);

    private static long EmMb(long bytes) => bytes / (1024 * 1024);
}

/// <summary>
/// Limits of the <c>Claude:Arquivos</c> section, with defaults
/// </summary>
/// <param name="DocumentoMaximo">Maximum size of a PDF, text, Word or Excel file, in bytes</param>
/// <param name="ImagemMaxima">Maximum size of an image, in bytes</param>
/// <param name="TotalMaximo">Maximum sum of the files of one analysis, in bytes</param>
/// <param name="MaxCaracteresDeTexto">Maximum characters of the text extracted from one file</param>
internal sealed record LimitesDosArquivos(long DocumentoMaximo, long ImagemMaxima, long TotalMaximo, int MaxCaracteresDeTexto)
{
    public const string Secao = "Claude:Arquivos";

    public const int MaxMbDocumentoPadrao = 20;

    public const int MaxMbImagemPadrao = 5;

    public const int MaxMbTotalPadrao = 20;

    public const int MaxCaracteresDeTextoPadrao = 300_000;

    /// <summary>
    /// Read the section, applying the defaults
    /// </summary>
    public static LimitesDosArquivos Ler(IConfiguration configuration)
    {
        var secao = configuration.GetSection(Secao);

        return new LimitesDosArquivos(
            Mb(secao["MaxMbDocumento"], MaxMbDocumentoPadrao),
            Mb(secao["MaxMbImagem"], MaxMbImagemPadrao),
            Mb(secao["MaxMbTotal"], MaxMbTotalPadrao),
            int.TryParse(secao["MaxCaracteresDeTexto"], out var caracteres) && caracteres > 0 ? caracteres : MaxCaracteresDeTextoPadrao);

        static long Mb(string? valor, int padrao) =>
            (int.TryParse(valor, out var mb) && mb > 0 ? mb : padrao) * 1024L * 1024L;
    }
}
