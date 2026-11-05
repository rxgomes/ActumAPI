using System.Net;
using System.Text.Json;
using Actum.Modules.Claude.Application;
using Actum.Modules.Claude.Contracts;
using Actum.Modules.Claude.Errors;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace Actum.Modules.Claude.Tests;

public sealed class AnalisadorDeDemandaTests
{
    private const string ChaveDeTeste = "chave-de-teste-nao-real";

    private static readonly DemandaParaAnalise Demanda = new(
        "Aureum-ERP",
        "Ajustar cálculo do IPI",
        "O IPI devolvido deve ser abatido do total.",
        "Fiscal",
        [new ComentarioDaDemanda("Maria", new DateTime(2026, 10, 1, 9, 30, 0), "Considerar só notas de devolução.")],
        []);

    private readonly AnthropicFalso _anthropic = new();

    private AnalisadorDeDemanda Criar(Dictionary<string, string?>? configuracao = null)
    {
        var valores = configuracao ?? new Dictionary<string, string?> { ["Claude:ApiKey"] = ChaveDeTeste };
        valores.TryAdd("Claude:NovasTentativas", "0");

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(valores).Build();
        var httpClient = new HttpClient(_anthropic);
        AnalisadorDeDemanda.Configurar(httpClient);

        return new AnalisadorDeDemanda(httpClient, configuration, NullLogger<AnalisadorDeDemanda>.Instance);
    }

    [Fact]
    public async Task Resposta_concluida_devolve_a_demanda_estruturada_sem_itens_vazios()
    {
        _anthropic.ResponderEstruturado(
            "  Abater o IPI devolvido do total da nota.  ",
            ["Ajustar o cálculo", "  ", "Criar teste "],
            [""]);

        var resultado = await Criar().AnalisarAsync(Demanda, TestContext.Current.CancellationToken);

        Assert.Equal("Abater o IPI devolvido do total da nota.", resultado.Objetivo);
        Assert.Equal(["Ajustar o cálculo", "Criar teste"], resultado.Checklist.Select(item => item.Texto));
        Assert.Empty(resultado.Duvidas);
        Assert.Equal("claude-opus-5-5", resultado.Modelo);
        Assert.Equal(1200, resultado.TokensEntrada);
        Assert.Equal(345, resultado.TokensSaida);
    }

    [Fact]
    public async Task Checklist_traz_grupo_e_aviso_de_trecho_vago_e_os_testes_vem_separados()
    {
        _anthropic.ResponderMensagem(JsonSerializer.Serialize(new
        {
            objetivo = "Abater o IPI.",
            checklist = new[]
            {
                new { grupo = " Front ", texto = "Exibir os campos", vago = "" },
                new { grupo = "Back: cálculo", texto = "Somar no pagamento", vago = " Não diz em quais fluxos. " },
                new { grupo = "", texto = "Revisar", vago = "" },
            },
            testes = new[] { "Emitir a NF 2958 de Barra Mansa", " " },
            duvidas = new[] { "Quais fluxos de pagamento?" },
        }));

        var resultado = await Criar().AnalisarAsync(Demanda, TestContext.Current.CancellationToken);

        Assert.Equal(
            [
                new ItemDoChecklist("Front", "Exibir os campos", null),
                new ItemDoChecklist("Back: cálculo", "Somar no pagamento", "Não diz em quais fluxos."),
                new ItemDoChecklist(AnalisadorDeDemanda.GrupoPadrao, "Revisar", null),
            ],
            resultado.Checklist);
        Assert.Equal(["Emitir a NF 2958 de Barra Mansa"], resultado.Testes);
        Assert.Equal(["Quais fluxos de pagamento?"], resultado.Duvidas);
        Assert.Empty(resultado.Regras);
    }

    [Fact]
    public async Task Regras_vem_com_a_origem_e_sem_itens_vazios()
    {
        _anthropic.ResponderMensagem(JsonSerializer.Serialize(new
        {
            objetivo = "Abater o IPI.",
            checklist = Array.Empty<object>(),
            testes = Array.Empty<string>(),
            duvidas = Array.Empty<string>(),
            regras = new[]
            {
                new { texto = " O desconto não pode passar de 10%. ", origem = " comentário de Maria em 04/11/2026 14:30 " },
                new { texto = "Não alterar o layout da nota.", origem = "" },
                new { texto = " ", origem = "descrição" },
            },
        }));

        var resultado = await Criar().AnalisarAsync(Demanda, TestContext.Current.CancellationToken);

        Assert.Equal(
            [
                new RegraDaDemanda("O desconto não pode passar de 10%.", "comentário de Maria em 04/11/2026 14:30"),
                new RegraDaDemanda("Não alterar o layout da nota.", AnalisadorDeDemanda.OrigemPadrao),
            ],
            resultado.Regras);
    }

    [Fact]
    public async Task Requisicao_usa_modelo_esforco_saida_estruturada_e_fallback_padrao()
    {
        _anthropic.ResponderEstruturado("Objetivo", [], []);

        await Criar().AnalisarAsync(Demanda, TestContext.Current.CancellationToken);

        var (requisicao, corpo) = Assert.Single(_anthropic.Requisicoes);
        Assert.Equal("https://api.anthropic.com/v1/messages", requisicao.RequestUri?.ToString());
        Assert.Contains(ChaveDeTeste, requisicao.Headers.Authorization?.Parameter);
        Assert.Contains(AnalisadorDeDemanda.BetaDoFallback, string.Join(",", requisicao.Headers.GetValues("anthropic-beta")));
        Assert.DoesNotContain(ChaveDeTeste, corpo);

        using var json = JsonDocument.Parse(corpo);
        var raiz = json.RootElement;
        Assert.Equal("claude-opus-5-5", raiz.GetProperty("model").GetString());
        Assert.Equal(16000, raiz.GetProperty("max_tokens").GetInt32());
        Assert.Equal("default", raiz.GetProperty("fallbacks").GetString());
        Assert.False(raiz.TryGetProperty("thinking", out _));

        var outputConfig = raiz.GetProperty("output_config");
        Assert.Equal("medium", outputConfig.GetProperty("effort").GetString());
        Assert.Equal("json_schema", outputConfig.GetProperty("format").GetProperty("type").GetString());
        Assert.Equal(
            ["objetivo", "checklist", "testes", "duvidas", "regras"],
            outputConfig.GetProperty("format").GetProperty("schema").GetProperty("required").EnumerateArray().Select(item => item.GetString()));
    }

    [Fact]
    public async Task Modelo_e_esforco_vem_da_configuracao()
    {
        _anthropic.ResponderEstruturado("Objetivo", [], []);
        var configuracao = new Dictionary<string, string?>
        {
            ["Claude:ApiKey"] = ChaveDeTeste,
            ["Claude:Modelo"] = "claude-sonnet-5-5",
            ["Claude:Esforco"] = "High",
        };

        await Criar(configuracao).AnalisarAsync(Demanda, TestContext.Current.CancellationToken);

        using var json = JsonDocument.Parse(_anthropic.Requisicoes.Single().Corpo);
        Assert.Equal("claude-sonnet-5-5", json.RootElement.GetProperty("model").GetString());
        Assert.Equal("high", json.RootElement.GetProperty("output_config").GetProperty("effort").GetString());
    }

    [Fact]
    public async Task Modelo_gravado_e_o_que_respondeu()
    {
        _anthropic.ResponderMensagem(JsonSerializer.Serialize(new { objetivo = "x", checklist = Array.Empty<string>(), testes = Array.Empty<string>(), duvidas = Array.Empty<string>() }), modelo: "claude-opus-4-8");

        var resultado = await Criar().AnalisarAsync(Demanda, TestContext.Current.CancellationToken);

        Assert.Equal("claude-opus-4-8", resultado.Modelo);
    }

    [Fact]
    public async Task Chave_ausente_falha_sem_chamar_a_anthropic()
    {
        var analisador = Criar(new Dictionary<string, string?> { ["Claude:ApiKey"] = " " });

        await Assert.ThrowsAsync<ClaudeNaoConfiguradoException>(() => analisador.AnalisarAsync(Demanda, TestContext.Current.CancellationToken));
        Assert.Empty(_anthropic.Requisicoes);
    }

    [Fact]
    public async Task Recusa_vira_erro_de_recusa()
    {
        _anthropic.ResponderMensagem(string.Empty, stopReason: "refusal");

        await Assert.ThrowsAsync<ClaudeRecusaException>(() => Criar().AnalisarAsync(Demanda, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Resposta_truncada_vira_resposta_incompleta()
    {
        _anthropic.ResponderMensagem("{\"objetivo\": \"Abater", stopReason: "max_tokens");

        await Assert.ThrowsAsync<ClaudeRespostaIncompletaException>(() => Criar().AnalisarAsync(Demanda, TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("isto não é json")]
    [InlineData("{\"objetivo\": \"\", \"checklist\": [], \"testes\": [], \"duvidas\": []}")]
    [InlineData("{\"objetivo\": \"x\", \"checklist\": [], \"duvidas\": []}")]
    [InlineData("{\"objetivo\": \"x\", \"duvidas\": []}")]
    public async Task Json_fora_do_formato_vira_resposta_incompleta(string texto)
    {
        _anthropic.ResponderMensagem(texto);

        await Assert.ThrowsAsync<ClaudeRespostaIncompletaException>(() => Criar().AnalisarAsync(Demanda, TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, "authentication_error")]
    [InlineData(HttpStatusCode.Forbidden, "permission_error")]
    public async Task Chave_recusada_vira_chave_invalida(HttpStatusCode status, string tipo)
    {
        _anthropic.ResponderErro(status, tipo);

        await Assert.ThrowsAsync<ClaudeChaveInvalidaException>(() => Criar().AnalisarAsync(Demanda, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Conta_sem_credito_vira_erro_de_credito()
    {
        _anthropic.Responder(HttpStatusCode.BadRequest, new
        {
            type = "error",
            error = new
            {
                type = "invalid_request_error",
                message = "Your credit balance is too low to access the Anthropic API. Please go to Plans & Billing to upgrade or purchase credits.",
            },
        });

        var erro = await Assert.ThrowsAsync<ClaudeSemCreditoException>(() => Criar().AnalisarAsync(Demanda, TestContext.Current.CancellationToken));

        Assert.Contains("sem créditos", erro.Message);
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests, "rate_limit_error")]
    [InlineData((HttpStatusCode)529, "overloaded_error")]
    [InlineData(HttpStatusCode.InternalServerError, "api_error")]
    [InlineData(HttpStatusCode.BadRequest, "invalid_request_error")]
    public async Task Falhas_da_api_viram_indisponivel(HttpStatusCode status, string tipo)
    {
        _anthropic.ResponderErro(status, tipo);

        await Assert.ThrowsAsync<ClaudeIndisponivelException>(() => Criar().AnalisarAsync(Demanda, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Falha_transitoria_e_repetida_pelo_sdk()
    {
        _anthropic.ResponderErro((HttpStatusCode)529, "overloaded_error").ResponderEstruturado("Objetivo", ["Item"], []);
        var configuracao = new Dictionary<string, string?> { ["Claude:ApiKey"] = ChaveDeTeste, ["Claude:NovasTentativas"] = "1" };

        var resultado = await Criar(configuracao).AnalisarAsync(Demanda, TestContext.Current.CancellationToken);

        Assert.Equal(["Item"], resultado.Checklist.Select(item => item.Texto));
        Assert.Equal(2, _anthropic.Requisicoes.Count);
    }
}
