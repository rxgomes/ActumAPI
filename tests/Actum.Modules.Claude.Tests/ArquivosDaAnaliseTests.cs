using Actum.Modules.Claude.Application;
using Actum.Modules.Claude.Contracts;
using Microsoft.Extensions.Configuration;

namespace Actum.Modules.Claude.Tests;

/// <summary>
/// Cenários de legibilidade dos arquivos na capability <c>claude-client</c>.
/// </summary>
public sealed class ArquivosDaAnaliseTests
{
    private const long Mb = 1024 * 1024;

    [Theory]
    [InlineData("especificacao.pdf", TipoDeArquivo.Pdf)]
    [InlineData("Tela.PNG", TipoDeArquivo.Imagem)]
    [InlineData("foto.jpeg", TipoDeArquivo.Imagem)]
    [InlineData("regras.docx", TipoDeArquivo.Word)]
    [InlineData("precos.xlsx", TipoDeArquivo.Excel)]
    [InlineData("consulta.sql", TipoDeArquivo.Texto)]
    [InlineData("notas.md", TipoDeArquivo.Texto)]
    public void Arquivo_suportado_do_asana_e_legivel(string nome, TipoDeArquivo tipo)
    {
        var legibilidade = Criar().Avaliar(nome, 1024, "asana");

        Assert.Equal(new LegibilidadeDoArquivo(true, null, tipo), legibilidade);
    }

    [Theory]
    [InlineData("gdrive")]
    [InlineData("dropbox")]
    public void Arquivo_externo_nao_e_legivel(string hospedagem)
    {
        var legibilidade = Criar().Avaliar("planilha.xlsx", null, hospedagem);

        Assert.False(legibilidade.Legivel);
        Assert.Equal("Arquivos externos não podem ser lidos pela análise.", legibilidade.Motivo);
    }

    [Theory]
    [InlineData("video.mp4")]
    [InlineData("apresentacao.pptx")]
    [InlineData("antigo.doc")]
    [InlineData("sem-extensao")]
    public void Tipo_nao_suportado_nao_e_legivel(string nome)
    {
        var legibilidade = Criar().Avaliar(nome, 10, "asana");

        Assert.False(legibilidade.Legivel);
        Assert.Equal("Tipo de arquivo não suportado pela análise.", legibilidade.Motivo);
    }

    [Theory]
    [InlineData("tela.png", 6 * Mb, "Arquivo maior que 5 MB.")]
    [InlineData("manual.pdf", 21 * Mb, "Arquivo maior que 20 MB.")]
    public void Arquivo_acima_do_limite_do_tipo_nao_e_legivel(string nome, long tamanho, string motivo)
    {
        var legibilidade = Criar().Avaliar(nome, tamanho, "asana");

        Assert.False(legibilidade.Legivel);
        Assert.Equal(motivo, legibilidade.Motivo);
    }

    [Fact]
    public void Tamanho_desconhecido_e_legivel()
    {
        Assert.True(Criar().Avaliar("manual.pdf", null, "asana").Legivel);
    }

    [Fact]
    public void Limites_vem_da_configuracao()
    {
        var arquivos = Criar(new Dictionary<string, string?> { ["Claude:Arquivos:MaxMbImagem"] = "1", ["Claude:Arquivos:MaxMbTotal"] = "3" });

        Assert.Equal("Arquivo maior que 1 MB.", arquivos.Avaliar("tela.png", 2 * Mb, "asana").Motivo);
        Assert.Equal(3 * Mb, arquivos.TamanhoTotalMaximo);
        Assert.Equal(20 * Mb, arquivos.TamanhoMaximo("manual.pdf"));
        Assert.Contains("3 MB", arquivos.MensagemDoTotalMaximo);
    }

    private static ArquivosDaAnalise Criar(Dictionary<string, string?>? valores = null)
    {
        return new ArquivosDaAnalise(new ConfigurationBuilder().AddInMemoryCollection(valores ?? []).Build());
    }
}
