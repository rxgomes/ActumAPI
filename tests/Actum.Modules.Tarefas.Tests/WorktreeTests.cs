using Actum.Modules.Tarefas.Domain;
using Actum.Shared.Abstractions.Domain;

namespace Actum.Modules.Tarefas.Tests;

/// <summary>
/// Pasta e situação do worktree de uma branch (<c>task-worktrees</c>) na entidade, sem banco
/// </summary>
public sealed class WorktreeTests
{
    [Theory]
    [InlineData("261112-ipi/devolvido", "261112-ipi-devolvido")]
    [InlineData("feature/fiscal/ipi", "feature-fiscal-ipi")]
    [InlineData("fix:a|b?c*d<e>f\"g", "fix-a-b-c-d-e-f-g")]
    [InlineData("release.", "release")]
    [InlineData("sem-troca", "sem-troca")]
    public void PastaDaBranch(string branch, string pasta)
    {
        Assert.Equal(pasta, Worktree.PastaDaBranch(branch));
    }

    [Fact]
    public void WorktreeNovoTemAsPartesDaBranchNaPastaDaTarefa()
    {
        var branch = new TarefaBranch(TipoBranch.Implementacao, "261112-ipi/devolvido", null, [ParteBranch.Back, ParteBranch.Front]);

        var worktree = branch.PrepararWorktree(@"E:\wt\aureum");

        Assert.Equal(@"E:\wt\aureum\261112-ipi-devolvido", worktree.Pasta);
        Assert.Equal(
            [(ParteBranch.Front, @"E:\wt\aureum\261112-ipi-devolvido\front"), (ParteBranch.Back, @"E:\wt\aureum\261112-ipi-devolvido\back")],
            worktree.PartesEmOrdem.Select(parte => (parte.Parte, parte.Caminho)));
        Assert.True(branch.TemWorktreeAtivo);
    }

    [Fact]
    public void SituacaoGeral()
    {
        var branch = new TarefaBranch(TipoBranch.Implementacao, "x", null, [ParteBranch.Front, ParteBranch.Back]);
        var worktree = branch.PrepararWorktree(@"E:\wt");
        var (front, back) = (worktree.PartesEmOrdem[0], worktree.PartesEmOrdem[1]);

        front.MarcarCriado(DateTime.Now);
        back.MarcarFalha("O git não conseguiu criar o worktree");
        Assert.Equal(SituacaoWorktree.Falhou, worktree.Situacao);

        back.MarcarCriado(DateTime.Now);
        Assert.Equal(SituacaoWorktree.Criado, worktree.Situacao);
        Assert.Null(back.UltimoErro);

        front.MarcarRemovido();
        back.MarcarRemovido();
        worktree.MarcarRemovido(DateTime.Now);
        Assert.Equal(SituacaoWorktree.Removido, worktree.Situacao);
        Assert.False(branch.TemWorktreeAtivo);
    }

    [Fact]
    public void RecriarReaproveitaOWorktreeRemovidoNaMesmaPasta()
    {
        var branch = new TarefaBranch(TipoBranch.Implementacao, "x", null, [ParteBranch.Front]);
        var worktree = branch.PrepararWorktree(@"E:\wt\antiga");
        worktree.MarcarRemovido(DateTime.Now);

        var recriado = branch.PrepararWorktree(@"E:\wt\nova");

        Assert.Same(worktree, recriado);
        Assert.Equal(@"E:\wt\antiga\x", recriado.Pasta);
        Assert.True(recriado.Ativo);
    }

    [Fact]
    public void ErroLongoEhCortado()
    {
        var parte = new TarefaBranch(TipoBranch.Implementacao, "x", null, [ParteBranch.Front]).PrepararWorktree(@"E:\wt").PartesEmOrdem[0];

        parte.MarcarFalha(new string('e', 3000));

        Assert.Equal(WorktreeParte.ErroMaxLength, parte.UltimoErro!.Length);
    }

    [Fact]
    public void BranchBaseNaoTemWorktree()
    {
        var branch = new TarefaBranch(TipoBranch.Base, "main", null, [ParteBranch.Front]);

        Assert.Throws<DomainException>(() => branch.PrepararWorktree(@"E:\wt"));
    }

    [Fact]
    public void EmpresaPrecisaDaPastaEDoCloneDeCadaParte()
    {
        var empresa = new Empresa("1", "Aureum", "#1976D2") { PastaWorktrees = @"E:\wt\aureum", CloneFront = @"E:\src\erp-web" };

        Assert.True(empresa.TemConfiguracaoLocal([ParteBranch.Front]));
        Assert.False(empresa.TemConfiguracaoLocal([ParteBranch.Front, ParteBranch.Back]));

        empresa.PastaWorktrees = null;
        Assert.False(empresa.TemConfiguracaoLocal([ParteBranch.Front]));
    }
}
