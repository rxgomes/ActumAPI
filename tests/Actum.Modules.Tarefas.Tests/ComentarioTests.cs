using System.Text;
using Actum.Modules.Asana.Contracts;
using Actum.Modules.Asana.Errors;
using Actum.Modules.Tarefas.Application;
using Actum.Modules.Tarefas.Domain;
using Actum.Modules.Tarefas.Persistence;
using Actum.Shared.Abstractions.Domain;
using Actum.Tests.Shared;

namespace Actum.Modules.Tarefas.Tests;

/// <summary>
/// Cenários da capability <c>task-comments</c>, com as tarefas criadas pela sincronização e o Asana simulado.
/// </summary>
[Collection(SincronizacaoCollection.Nome)]
public sealed class ComentarioTests(PostgresFixture postgres) : SincronizacaoTestBase(postgres, "Aureum-ERP")
{
    private const string Nome = "comentario";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private AsanaUsuarioResumo Maria => new("gid-maria", "Maria Angélica");

    private AsanaUsuarioResumo Joao => new("gid-joao", "João");

    private AsanaUsuarioResumo Eu => new(UsuarioGid, "Ricardo");

    [Fact]
    public async Task ParticipantesSugeremQuemReabriuSemOProprioUsuarioAsync()
    {
        var tarefa = await TarefaReabertaPorAsync("Maria Angelica");
        Asana.Participantes[Gid(Nome)] = [Eu, Joao, Maria];

        var participantes = await ServicoAsync(service => service.ListarParticipantesAsync(tarefa, Ct));

        Assert.Equal([("gid-joao", false), ("gid-maria", true)], participantes!.Select(participante => (participante.Gid, participante.Sugerido)));
    }

    [Fact]
    public async Task SemReaberturaSugereQuemComentouPorUltimoAsync()
    {
        var tarefa = await TarefaComComentariosAsync(
            Comentario("c1", "João", "primeiro", Base.AddHours(1)),
            Comentario("c2", "Ricardo", "minha resposta", Base.AddHours(3)),
            Comentario("c3", "Maria Angélica", "segundo", Base.AddHours(2)));
        Asana.Participantes[Gid(Nome)] = [Joao, Maria];

        var participantes = await ServicoAsync(service => service.ListarParticipantesAsync(tarefa, Ct));

        Assert.Equal(["gid-maria"], participantes!.Where(participante => participante.Sugerido).Select(participante => participante.Gid));
    }

    [Fact]
    public async Task SemReaberturaNemComentarioNinguemESugeridoAsync()
    {
        var tarefa = await TarefaComComentariosAsync();
        Asana.Participantes[Gid(Nome)] = [Joao, Maria];

        var participantes = await ServicoAsync(service => service.ListarParticipantesAsync(tarefa, Ct));

        Assert.All(participantes!, participante => Assert.False(participante.Sugerido));
    }

    [Fact]
    public async Task TarefaInexistenteETarefaExcluidaNoAsanaAsync()
    {
        Assert.Null(await ServicoAsync(service => service.ListarParticipantesAsync(Guid.NewGuid(), Ct)));

        var tarefa = await TarefaComComentariosAsync();
        var erro = await Assert.ThrowsAsync<DomainException>(() => ServicoAsync(service => service.ListarParticipantesAsync(tarefa, Ct)));
        Assert.Equal("A tarefa não existe mais no Asana.", erro.Message);
    }

    [Fact]
    public async Task EnviaAnexosEmOrdemEDepoisOComentarioComMencaoAsync()
    {
        var tarefa = await TarefaComComentariosAsync();
        Asana.Participantes[Gid(Nome)] = [Maria];

        var enviado = await ServicoAsync(service => service.EnviarAsync(
            tarefa, "  Segue o erro  ", ["gid-maria"], [Arquivo("print-1.png", "image/png", "png"), Arquivo("log.txt", "text/plain", "log")], Ct));

        Assert.Equal(["anexo:print-1.png", "anexo:log.txt", "comentar"], Asana.Escritas);
        Assert.Equal(("image/png", "png"), (Asana.Anexos[0].Tipo, Encoding.UTF8.GetString(Asana.Anexos[0].Conteudo)));
        var comentario = Assert.Single(Asana.Comentarios);
        Assert.Equal("Segue o erro\n\nAnexos: print-1.png, log.txt", comentario.Texto);
        Assert.Equal(["gid-maria"], comentario.Mencoes);
        Assert.Equal(["print-1.png", "log.txt"], enviado!.Anexos);
        Assert.True(enviado.Comentario.Lido);
        Assert.Equal("Ricardo", enviado.Comentario.Autor);
    }

    [Fact]
    public async Task SoAnexosPublicaALinhaDosAnexosSemConsultarParticipantesAsync()
    {
        var tarefa = await TarefaComComentariosAsync();

        await ServicoAsync(service => service.EnviarAsync(tarefa, null, [], [Arquivo("a.pdf", "application/pdf", "pdf")], Ct));

        Assert.Equal("Anexos: a.pdf", Assert.Single(Asana.Comentarios).Texto);
    }

    [Fact]
    public async Task ComentarioFicaGravadoComoLidoENaoDuplicaNaSincronizacaoAsync()
    {
        var tarefa = await TarefaComComentariosAsync();

        await ServicoAsync(service => service.EnviarAsync(tarefa, "Pronto para teste", [], [], Ct));

        var lida = await LerTarefaAsync(Nome);
        var comentario = Assert.Single(lida!.Comentarios);
        Assert.True(comentario.Lido);
        Assert.Equal("Pronto para teste", comentario.Texto);

        // A story publicada volta no histórico; a próxima sincronização (tarefa modificada) não duplica.
        Abertas(TarefaAsana(Nome, modificadaEm: Base.AddDays(1)));
        await SincronizarAsync();
        Assert.Single((await LerTarefaAsync(Nome))!.Comentarios);
    }

    [Theory]
    [InlineData("   ", 0, "Escreva um comentário ou anexe um arquivo.")]
    [InlineData("ok", 11, "Envie no máximo 10 arquivos por vez.")]
    public async Task ValidacoesSemChamarOAsanaAsync(string texto, int quantidade, string mensagem)
    {
        var tarefa = await TarefaComComentariosAsync();
        var arquivos = Enumerable.Range(1, quantidade).Select(indice => Arquivo($"f{indice}.txt", "text/plain", "x")).ToList();

        var erro = await Assert.ThrowsAsync<DomainException>(() => ServicoAsync(service => service.EnviarAsync(tarefa, texto, [], arquivos, Ct)));

        Assert.Equal(mensagem, erro.Message);
        Assert.Empty(Asana.Escritas);
    }

    [Fact]
    public async Task LimitesDeTamanhoEArquivoVazioAsync()
    {
        var tarefa = await TarefaComComentariosAsync();
        var grande = new ArquivoParaEnviar("video.mp4", "video/mp4", ComentarioService.TamanhoMaximoDoArquivo + 1, () => Stream.Null);
        var vazio = new ArquivoParaEnviar("vazio.txt", "text/plain", 0, () => Stream.Null);
        var metade = new ArquivoParaEnviar("parte.zip", "application/zip", 20L * 1024 * 1024, () => Stream.Null);

        var erroGrande = await Assert.ThrowsAsync<DomainException>(() => ServicoAsync(service => service.EnviarAsync(tarefa, null, [], [grande], Ct)));
        var erroVazio = await Assert.ThrowsAsync<DomainException>(() => ServicoAsync(service => service.EnviarAsync(tarefa, null, [], [vazio], Ct)));
        var erroTotal = await Assert.ThrowsAsync<DomainException>(() => ServicoAsync(service => service.EnviarAsync(tarefa, null, [], [metade, metade, metade], Ct)));

        Assert.Equal("O arquivo video.mp4 tem mais de 25 MB, o limite por arquivo.", erroGrande.Message);
        Assert.Equal("O arquivo vazio.txt está vazio.", erroVazio.Message);
        Assert.Equal("Os arquivos somam mais de 50 MB, o limite por envio.", erroTotal.Message);
        Assert.Empty(Asana.Escritas);
    }

    [Fact]
    public async Task MencaoForaDosParticipantesNaoEnviaNadaAsync()
    {
        var tarefa = await TarefaComComentariosAsync();
        Asana.Participantes[Gid(Nome)] = [Maria];

        var erro = await Assert.ThrowsAsync<DomainException>(
            () => ServicoAsync(service => service.EnviarAsync(tarefa, "oi", ["gid-joao"], [Arquivo("a.png", "image/png", "x")], Ct)));

        Assert.Equal("Só é possível mencionar participantes da tarefa no Asana.", erro.Message);
        Assert.Empty(Asana.Escritas);
    }

    [Fact]
    public async Task TarefaArquivadaNaoRecebeComentarioAsync()
    {
        var tarefa = await TarefaComComentariosAsync();
        await AlterarTarefaAsync(Nome, status: StatusTarefa.Arquivada);

        var erro = await Assert.ThrowsAsync<DomainException>(() => ServicoAsync(service => service.EnviarAsync(tarefa, "oi", [], [], Ct)));

        Assert.Equal("Tarefas arquivadas não recebem comentários.", erro.Message);
        Assert.Empty(Asana.Escritas);
    }

    [Fact]
    public async Task FalhaNoComentarioDepoisDosAnexosInformaOsAnexosEnviadosAsync()
    {
        var tarefa = await TarefaComComentariosAsync();
        Asana.FalharNaEscrita = "comentar";

        var erro = await Assert.ThrowsAsync<EnvioParcialException>(
            () => ServicoAsync(service => service.EnviarAsync(tarefa, "Segue", [], [Arquivo("print-1.png", "image/png", "png")], Ct)));

        Assert.Equal(["print-1.png"], erro.AnexosEnviados);
        Assert.IsType<AsanaIndisponivelException>(erro.Falha);
        Assert.Contains("print-1.png já foram enviados ao Asana", erro.Message);
        Assert.Empty((await LerTarefaAsync(Nome))!.Comentarios);
    }

    [Fact]
    public async Task FalhaSemAnexosEnviadosNaoEParcialAsync()
    {
        var tarefa = await TarefaComComentariosAsync();
        Asana.FalharNaEscrita = "comentar";

        await Assert.ThrowsAsync<AsanaIndisponivelException>(() => ServicoAsync(service => service.EnviarAsync(tarefa, "Segue", [], [], Ct)));
    }

    private static ArquivoParaEnviar Arquivo(string nome, string tipo, string conteudo)
    {
        var bytes = Encoding.UTF8.GetBytes(conteudo);
        return new ArquivoParaEnviar(nome, tipo, bytes.Length, () => new MemoryStream(bytes));
    }

    private async Task<Guid> TarefaComComentariosAsync(params AsanaStory[] comentarios)
    {
        Abertas(TarefaAsana(Nome));
        Historico(Nome, comentarios);
        await SincronizarAsync();

        return (await LerTarefaAsync(Nome))!.Identifier;
    }

    private async Task<Guid> TarefaReabertaPorAsync(string autor)
    {
        Abertas(TarefaAsana(Nome));
        await SincronizarAsync();
        var conclusao = Base.AddDays(1);
        await AlterarTarefaAsync(Nome, status: StatusTarefa.Concluida, concluidaEm: conclusao);
        Abertas();
        Asana.Tarefas[Gid(Nome)] = TarefaAsana(Nome, concluida: true, modificadaEm: conclusao);
        ConcluiuNoAsana(Nome, conclusao);
        await SincronizarAsync();

        var reabertaEm = conclusao.AddHours(2);
        Abertas(TarefaAsana(Nome, modificadaEm: reabertaEm));
        Historico(Nome, Reabertura("r1", autor, reabertaEm), Comentario("c1", "João", "comentário depois", reabertaEm.AddHours(1)));
        await SincronizarAsync();

        return (await LerTarefaAsync(Nome))!.Identifier;
    }

    private async Task<T> ServicoAsync<T>(Func<IComentarioService, Task<T>> acao)
    {
        await using var contexto = TarefasDbContext.Criar(Postgres.DataSource);

        return await acao(new ComentarioService(contexto, Asana));
    }
}
