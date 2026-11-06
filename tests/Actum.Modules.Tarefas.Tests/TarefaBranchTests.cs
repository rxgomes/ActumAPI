using Actum.Modules.Tarefas.Domain;

namespace Actum.Modules.Tarefas.Tests;

/// <summary>
/// Cenários de "Partes e tag da branch" (<c>task-lifecycle</c>) na entidade, sem banco.
/// </summary>
public sealed class TarefaBranchTests
{
    [Fact]
    public void DuasPartesFicamNaOrdemFrontBackNaTag()
    {
        var branch = new TarefaBranch(TipoBranch.Base, "main", null, [ParteBranch.Back, ParteBranch.Front, ParteBranch.Back]);

        Assert.Equal([ParteBranch.Front, ParteBranch.Back], branch.Partes);
        Assert.Equal("(Front, Back) => main", branch.Tag);
    }

    [Theory]
    [InlineData(ParteBranch.Front, "(Front) => 261105-consumidor-final-nf")]
    [InlineData(ParteBranch.Back, "(Back) => 261105-consumidor-final-nf")]
    public void UmaParte(ParteBranch parte, string tag)
    {
        var branch = new TarefaBranch(TipoBranch.Implementacao, "261105-consumidor-final-nf", null, [parte]);

        Assert.Equal([parte], branch.Partes);
        Assert.Equal(tag, branch.Tag);
    }

    [Fact]
    public void SemPartesATagEONome()
    {
        var branch = new TarefaBranch(TipoBranch.Implementacao, "feature/ipi-devolvido", null, []);

        Assert.Empty(branch.Partes);
        Assert.Equal("feature/ipi-devolvido", branch.Tag);
    }
}
