using System.Text;
using Actum.Modules.Claude.Application;
using Actum.Modules.Claude.Contracts;
using Actum.Modules.Claude.Errors;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Word = DocumentFormat.OpenXml.Wordprocessing;

namespace Actum.Modules.Claude.Tests;

/// <summary>
/// Cenários de extração de texto dos arquivos na capability <c>claude-client</c>. Os <c>.docx</c> e <c>.xlsx</c>
/// são gerados no próprio teste.
/// </summary>
public sealed class ExtratorDeTextoTests
{
    [Fact]
    public void Word_traz_paragrafos_e_tabelas_na_ordem()
    {
        var arquivo = new ArquivoDaDemanda("regras.docx", Documento());

        var texto = ExtratorDeTexto.Extrair(arquivo, TipoDeArquivo.Word, 10_000);

        Assert.Equal("Valores com duas casas.\nProduto | Preço\nCaneta | 2,50\nÚltimo parágrafo.", texto.ReplaceLineEndings("\n"));
    }

    [Fact]
    public void Excel_traz_cada_planilha_com_as_linhas_separadas_por_tabulacao()
    {
        var arquivo = new ArquivoDaDemanda("precos.xlsx", Planilha());

        var texto = ExtratorDeTexto.Extrair(arquivo, TipoDeArquivo.Excel, 10_000);

        Assert.Equal("## Preços\nProduto\tValor\nCaneta\t2.5\n\n## Filiais\nBarra Mansa", texto.ReplaceLineEndings("\n"));
    }

    [Fact]
    public void Texto_e_lido_em_utf8_sem_o_bom()
    {
        var conteudo = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes("select 'ação';")).ToArray();

        var texto = ExtratorDeTexto.Extrair(new ArquivoDaDemanda("consulta.sql", conteudo), TipoDeArquivo.Texto, 10_000);

        Assert.Equal("select 'ação';", texto);
    }

    [Theory]
    [InlineData(TipoDeArquivo.Word)]
    [InlineData(TipoDeArquivo.Excel)]
    public void Arquivo_corrompido_falha_com_o_nome(TipoDeArquivo tipo)
    {
        var arquivo = new ArquivoDaDemanda("quebrado.docx", Encoding.UTF8.GetBytes("não é um zip"));

        var erro = Assert.Throws<ClaudeArquivoIlegivelException>(() => ExtratorDeTexto.Extrair(arquivo, tipo, 10_000));

        Assert.Equal("Não foi possível ler o arquivo quebrado.docx.", erro.Message);
    }

    [Fact]
    public void Texto_acima_do_limite_falha_sem_cortar()
    {
        var arquivo = new ArquivoDaDemanda("base.csv", Encoding.UTF8.GetBytes(new string('x', 101)));

        var erro = Assert.Throws<ClaudeArquivoIlegivelException>(() => ExtratorDeTexto.Extrair(arquivo, TipoDeArquivo.Texto, 100));

        Assert.Contains("base.csv", erro.Message);
        Assert.Contains("100 caracteres", erro.Message);
    }

    private static byte[] Documento()
    {
        using var fluxo = new MemoryStream();
        using (var documento = WordprocessingDocument.Create(fluxo, WordprocessingDocumentType.Document))
        {
            var principal = documento.AddMainDocumentPart();
            principal.Document = new Word.Document(new Word.Body(
                Paragrafo("Valores com duas casas."),
                new Word.Table(Linha("Produto", "Preço"), Linha("Caneta", "2,50")),
                Paragrafo("Último parágrafo.")));
        }

        return fluxo.ToArray();

        static Word.Paragraph Paragrafo(string texto) => new(new Word.Run(new Word.Text(texto)));

        static Word.TableRow Linha(params string[] celulas) =>
            new(celulas.Select(celula => new Word.TableCell(Paragrafo(celula))));
    }

    private static byte[] Planilha()
    {
        using var fluxo = new MemoryStream();
        using (var pasta = SpreadsheetDocument.Create(fluxo, SpreadsheetDocumentType.Workbook))
        {
            var parte = pasta.AddWorkbookPart();
            parte.Workbook = new Workbook(new Sheets());

            var compartilhados = parte.AddNewPart<SharedStringTablePart>();
            compartilhados.SharedStringTable = new SharedStringTable(
                new SharedStringItem(new Text("Produto")),
                new SharedStringItem(new Text("Caneta")));

            Adicionar(parte, 1, "Preços", new Row(Compartilhada(0), Inline("Valor")), new Row(Compartilhada(1), Numero("2.5")), new Row());
            Adicionar(parte, 2, "Filiais", new Row(Inline("Barra Mansa")));
        }

        return fluxo.ToArray();

        static void Adicionar(WorkbookPart parte, uint id, string nome, params Row[] linhas)
        {
            var planilha = parte.AddNewPart<WorksheetPart>();
            planilha.Worksheet = new Worksheet(new SheetData(linhas));
            parte.Workbook!.Sheets!.Append(new Sheet { Id = parte.GetIdOfPart(planilha), SheetId = id, Name = nome });
        }

        static Cell Compartilhada(int indice) => new() { DataType = CellValues.SharedString, CellValue = new CellValue(indice.ToString(System.Globalization.CultureInfo.InvariantCulture)) };

        static Cell Inline(string texto) => new() { DataType = CellValues.InlineString, InlineString = new InlineString(new Text(texto)) };

        static Cell Numero(string valor) => new() { CellValue = new CellValue(valor) };
    }
}
