using System.Text;
using Actum.Modules.Claude.Contracts;
using Actum.Modules.Claude.Errors;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Word = DocumentFormat.OpenXml.Wordprocessing;

namespace Actum.Modules.Claude.Application;

/// <summary>
/// Text of the files sent to the AI as text: plain text (UTF-8), Word (paragraphs and tables, in order) and Excel
/// (one section per sheet, cells separated by tab). Never cuts the text: above the limit, the file is rejected.
/// </summary>
internal static class ExtratorDeTexto
{
    /// <summary>
    /// Extract the text of the file; fails with <see cref="ClaudeArquivoIlegivelException"/> when the file cannot be
    /// opened or the text passes <paramref name="maxCaracteres"/>
    /// </summary>
    public static string Extrair(ArquivoDaDemanda arquivo, TipoDeArquivo tipo, int maxCaracteres)
    {
        string texto;
        try
        {
            texto = tipo switch
            {
                TipoDeArquivo.Texto => Texto(arquivo.Conteudo),
                TipoDeArquivo.Word => Documento(arquivo.Conteudo),
                TipoDeArquivo.Excel => Planilhas(arquivo.Conteudo),
                _ => throw new ClaudeArquivoIlegivelException(arquivo.Nome),
            };
        }
        catch (ClaudeException)
        {
            throw;
        }
        catch (Exception erro) when (erro is OpenXmlPackageException or InvalidDataException or IOException or FormatException or InvalidOperationException)
        {
            throw new ClaudeArquivoIlegivelException(arquivo.Nome, erro);
        }

        if (texto.Length > maxCaracteres)
        {
            throw ClaudeArquivoIlegivelException.TextoLongoDemais(arquivo.Nome, maxCaracteres);
        }

        return texto;
    }

    private static string Texto(byte[] conteudo)
    {
        // O decodificador ignora o BOM do UTF-8.
        return new UTF8Encoding(false, false).GetString(conteudo).TrimStart('﻿');
    }

    private static string Documento(byte[] conteudo)
    {
        using var fluxo = new MemoryStream(conteudo, writable: false);
        using var documento = WordprocessingDocument.Open(fluxo, false);
        var corpo = documento.MainDocumentPart?.Document?.Body
            ?? throw new InvalidDataException("Documento sem corpo.");

        var texto = new StringBuilder();
        foreach (var elemento in corpo.ChildElements)
        {
            switch (elemento)
            {
                case Word.Paragraph paragrafo:
                    texto.AppendLine(paragrafo.InnerText);
                    break;
                case Word.Table tabela:
                    foreach (var linha in tabela.Elements<Word.TableRow>())
                    {
                        texto.AppendLine(string.Join(" | ", linha.Elements<Word.TableCell>().Select(celula => celula.InnerText.Trim())));
                    }

                    break;
            }
        }

        return texto.ToString().Trim();
    }

    private static string Planilhas(byte[] conteudo)
    {
        using var fluxo = new MemoryStream(conteudo, writable: false);
        using var pasta = SpreadsheetDocument.Open(fluxo, false);
        var parte = pasta.WorkbookPart ?? throw new InvalidDataException("Pasta sem planilhas.");
        var compartilhados = parte.SharedStringTablePart?.SharedStringTable?.Elements<SharedStringItem>().Select(item => item.InnerText).ToList() ?? [];

        var texto = new StringBuilder();
        foreach (var planilha in parte.Workbook?.Sheets?.Elements<Sheet>() ?? [])
        {
            if (planilha.Id?.Value is not { } id || parte.GetPartById(id) is not WorksheetPart dados)
            {
                continue;
            }

            texto.AppendLine(CultureInvariant($"## {planilha.Name?.Value}"));
            foreach (var linha in dados.Worksheet?.Descendants<Row>() ?? [])
            {
                var valores = linha.Elements<Cell>().Select(celula => Valor(celula, compartilhados)).ToList();
                if (valores.All(string.IsNullOrWhiteSpace))
                {
                    continue;
                }

                texto.AppendLine(string.Join('\t', valores));
            }

            texto.AppendLine();
        }

        return texto.ToString().Trim();
    }

    private static string Valor(Cell celula, List<string> compartilhados)
    {
        if (celula.DataType?.Value == CellValues.InlineString)
        {
            return celula.InlineString?.InnerText ?? string.Empty;
        }

        // Fórmulas guardam em CellValue o último resultado calculado.
        var valor = celula.CellValue?.Text ?? string.Empty;
        if (celula.DataType?.Value == CellValues.SharedString && int.TryParse(valor, out var indice) && indice >= 0 && indice < compartilhados.Count)
        {
            return compartilhados[indice];
        }

        return valor;
    }

    private static string CultureInvariant(FormattableString texto) => FormattableString.Invariant(texto);
}
