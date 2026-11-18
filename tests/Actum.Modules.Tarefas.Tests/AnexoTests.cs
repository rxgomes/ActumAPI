using Actum.Modules.Asana.Contracts;
using Actum.Modules.Tarefas.Application;
using Actum.Modules.Tarefas.Persistence;
using Actum.Shared.Abstractions.Domain;
using Actum.Tests.Shared;

namespace Actum.Modules.Tarefas.Tests;

/// <summary>
/// Cenários da capability <c>task-attachments</c>, com as tarefas criadas pela sincronização e o Asana simulado.
/// </summary>
[Collection(SincronizacaoCollection.Nome)]
public sealed class AnexoTests(PostgresFixture postgres) : SincronizacaoTestBase(postgres, "Aureum-ERP")
{
    private const string Nome = "anexo";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ListaOsAnexosDoMaisAntigoParaOMaisRecenteAsync()
    {
        var tarefa = await TarefaAsync();
        Asana.AnexosDaTarefa[Gid(Nome)] =
        [
            new("a2", "layout.pdf", Base.AddHours(2), 2048, "asana"),
            new("a1", "print.png", Base.AddHours(1), 512, "asana"),
            new("a3", "planilha", Base.AddHours(3), null, "gdrive"),
        ];

        var anexos = await ServicoAsync(service => service.ListarAsync(tarefa, Ct));

        Assert.Equal(
            [("a1", "print.png", 512L, "asana"), ("a2", "layout.pdf", 2048L, "asana"), ("a3", "planilha", (long?)null, "gdrive")],
            anexos!.Select(anexo => (anexo.Gid, anexo.Nome, anexo.Tamanho, anexo.Hospedagem)));
        Assert.Equal(Base.AddHours(1), anexos![0].CriadoEm);
    }

    [Fact]
    public async Task CadaAnexoInformaSePodeSerLidoPelaAnaliseAsync()
    {
        var tarefa = await TarefaAsync();
        Asana.AnexosDaTarefa[Gid(Nome)] =
        [
            new("a1", "layout.pdf", Base.AddHours(1), 2048, "asana"),
            new("a2", "video.mp4", Base.AddHours(2), 2048, "asana"),
            new("a3", "planilha", Base.AddHours(3), null, "gdrive"),
        ];

        var anexos = await ServicoAsync(service => service.ListarAsync(tarefa, Ct));

        Assert.Equal(
            [
                (true, (string?)null),
                (false, "Tipo de arquivo não suportado pela análise."),
                (false, "Arquivos externos não podem ser lidos pela análise."),
            ],
            anexos!.Select(anexo => (anexo.LegivelNaAnalise, anexo.Motivo)));
    }

    [Fact]
    public async Task TarefaSemAnexosTemListaVaziaAsync()
    {
        var tarefa = await TarefaAsync();
        Asana.AnexosDaTarefa[Gid(Nome)] = [];

        Assert.Empty((await ServicoAsync(service => service.ListarAsync(tarefa, Ct)))!);
    }

    [Fact]
    public async Task TarefaInexistenteETarefaExcluidaNoAsanaAsync()
    {
        Assert.Null(await ServicoAsync(service => service.ListarAsync(Guid.NewGuid(), Ct)));

        var tarefa = await TarefaAsync();
        var erro = await Assert.ThrowsAsync<DomainException>(() => ServicoAsync(service => service.ListarAsync(tarefa, Ct)));
        Assert.Equal("A tarefa não existe mais no Asana.", erro.Message);
    }

    [Fact]
    public async Task DownloadDevolveOEnderecoNovoDoAnexoDaTarefaAsync()
    {
        var tarefa = await TarefaAsync();
        Asana.Downloads["a1"] = new("print.png", Gid(Nome), new Uri("https://s3.amazonaws.com/asana/print.png?assinatura=x"));

        var endereco = await ServicoAsync(service => service.ObterEnderecoDeDownloadAsync(tarefa, "a1", Ct));

        Assert.Equal(new Uri("https://s3.amazonaws.com/asana/print.png?assinatura=x"), endereco);
    }

    [Fact]
    public async Task DownloadDeAnexoDeOutraTarefaInexistenteOuDeTarefaInexistenteNaoEncontraAsync()
    {
        var tarefa = await TarefaAsync();
        Asana.Downloads["de-outra"] = new("segredo.pdf", "outra-tarefa", new Uri("https://s3.amazonaws.com/asana/segredo.pdf"));
        Asana.Downloads["a1"] = new("print.png", Gid(Nome), new Uri("https://s3.amazonaws.com/asana/print.png"));

        Assert.Null(await ServicoAsync(service => service.ObterEnderecoDeDownloadAsync(tarefa, "de-outra", Ct)));
        Assert.Null(await ServicoAsync(service => service.ObterEnderecoDeDownloadAsync(tarefa, "excluido", Ct)));
        Assert.Null(await ServicoAsync(service => service.ObterEnderecoDeDownloadAsync(Guid.NewGuid(), "a1", Ct)));
    }

    [Fact]
    public async Task AnexoSemEnderecoNoAsanaFalhaComMensagemAsync()
    {
        var tarefa = await TarefaAsync();
        Asana.Downloads["a1"] = new("arquivo", Gid(Nome), null);

        var erro = await Assert.ThrowsAsync<DomainException>(() => ServicoAsync(service => service.ObterEnderecoDeDownloadAsync(tarefa, "a1", Ct)));

        Assert.Equal("O Asana não oferece um endereço para baixar este anexo.", erro.Message);
    }

    private async Task<Guid> TarefaAsync()
    {
        Abertas(TarefaAsana(Nome));
        Historico(Nome);
        await SincronizarAsync();

        return (await LerTarefaAsync(Nome))!.Identifier;
    }

    private async Task<T> ServicoAsync<T>(Func<IAnexoService, Task<T>> acao)
    {
        await using var contexto = TarefasDbContext.Criar(Postgres.DataSource);

        return await acao(new AnexoService(contexto, Asana, AnaliseTests.Arquivos));
    }
}
