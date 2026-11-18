using Actum.Modules.Tarefas.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Actum.Modules.Tarefas.Persistence;

/// <summary>
/// Mapping of <see cref="Empresa"/> to the <c>empresa</c> table
/// </summary>
internal sealed class EmpresaConfiguration : IEntityTypeConfiguration<Empresa>
{
    public void Configure(EntityTypeBuilder<Empresa> builder)
    {
        builder.Property(empresa => empresa.AsanaWorkspaceGid).HasMaxLength(Empresa.GidMaxLength);
        builder.HasIndex(empresa => empresa.AsanaWorkspaceGid).IsUnique();
        builder.Property(empresa => empresa.Nome).HasMaxLength(Empresa.NomeMaxLength);
        builder.Property(empresa => empresa.Cor).HasMaxLength(Empresa.CorLength);
        builder.Property(empresa => empresa.RepositorioFront).HasMaxLength(Empresa.RepositorioMaxLength);
        builder.Property(empresa => empresa.RepositorioBack).HasMaxLength(Empresa.RepositorioMaxLength);
        builder.Property(empresa => empresa.CloneFront).HasMaxLength(Empresa.CaminhoMaxLength);
        builder.Property(empresa => empresa.CloneBack).HasMaxLength(Empresa.CaminhoMaxLength);
        builder.Property(empresa => empresa.PastaWorktrees).HasMaxLength(Empresa.CaminhoMaxLength);

        // Default só para as linhas existentes na migration; a aplicação sempre envia a lista (vazia incluída).
        builder.Property(empresa => empresa.ArquivosFront).HasDefaultValueSql("'{}'::text[]").ValueGeneratedNever();
        builder.Property(empresa => empresa.ArquivosBack).HasDefaultValueSql("'{}'::text[]").ValueGeneratedNever();

        // Default só para inclusões por SQL; a aplicação sempre envia o valor (false incluído).
        builder.Property(empresa => empresa.Sincronizar).HasDefaultValue(true).ValueGeneratedNever();

        builder.Ignore(empresa => empresa.CargaInicialPendente);
    }
}

/// <summary>
/// Mapping of <see cref="Tarefa"/> to the <c>tarefa</c> table
/// </summary>
internal sealed class TarefaConfiguration : IEntityTypeConfiguration<Tarefa>
{
    public void Configure(EntityTypeBuilder<Tarefa> builder)
    {
        builder.Property(tarefa => tarefa.AsanaGid).HasMaxLength(Tarefa.GidMaxLength);
        builder.HasIndex(tarefa => tarefa.AsanaGid).IsUnique();
        builder.Property(tarefa => tarefa.Titulo).HasMaxLength(Tarefa.TituloMaxLength);
        builder.Property(tarefa => tarefa.Projetos).HasMaxLength(Tarefa.ProjetosMaxLength);
        builder.Property(tarefa => tarefa.LinkAsana).HasMaxLength(Tarefa.LinkMaxLength);
        builder.Property(tarefa => tarefa.PrazoInternoPendente).HasDefaultValue(false).ValueGeneratedNever();
        builder.Property(tarefa => tarefa.EmAndamento).HasDefaultValue(false).ValueGeneratedNever();

        builder.Property(tarefa => tarefa.Status)
            .HasConversion(new EnumSnakeCaseConverter<StatusTarefa>())
            .HasMaxLength(Tarefa.StatusMaxLength);
        builder.Property(tarefa => tarefa.StatusAnterior)
            .HasConversion(new EnumSnakeCaseConverter<StatusTarefa>())
            .HasMaxLength(Tarefa.StatusMaxLength);
        builder.Property(tarefa => tarefa.MotivoArquivamento)
            .HasConversion(new EnumSnakeCaseConverter<MotivoArquivamento>())
            .HasMaxLength(Tarefa.StatusMaxLength);

        builder.HasOne<Empresa>()
            .WithMany()
            .HasForeignKey(tarefa => tarefa.EmpresaId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(tarefa => tarefa.Comentarios)
            .WithOne()
            .HasForeignKey(comentario => comentario.TarefaId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(tarefa => tarefa.Retrabalhos)
            .WithOne()
            .HasForeignKey(retrabalho => retrabalho.TarefaId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(tarefa => tarefa.Branches)
            .WithOne()
            .HasForeignKey(branch => branch.TarefaId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(tarefa => tarefa.Conclusoes)
            .WithOne()
            .HasForeignKey(conclusao => conclusao.TarefaId)
            .OnDelete(DeleteBehavior.Cascade);

        // Calculada a partir de Branches; sem isso o EF a mapearia como uma segunda navegação.
        builder.Ignore(tarefa => tarefa.BranchesPendentes);
        builder.Ignore(tarefa => tarefa.NovaBranchSugerida);

        builder.HasIndex(tarefa => new { tarefa.EmpresaId, tarefa.Status });
        builder.HasIndex(tarefa => tarefa.PrazoInterno);
    }
}

/// <summary>
/// Mapping of <see cref="Comentario"/> to the <c>comentario</c> table
/// </summary>
internal sealed class ComentarioConfiguration : IEntityTypeConfiguration<Comentario>
{
    public void Configure(EntityTypeBuilder<Comentario> builder)
    {
        builder.Property(comentario => comentario.AsanaStoryGid).HasMaxLength(Comentario.GidMaxLength);
        builder.HasIndex(comentario => comentario.AsanaStoryGid).IsUnique();
        builder.Property(comentario => comentario.Autor).HasMaxLength(Comentario.AutorMaxLength);
    }
}

/// <summary>
/// Mapping of <see cref="Retrabalho"/> to the <c>retrabalho</c> table
/// </summary>
internal sealed class RetrabalhoConfiguration : IEntityTypeConfiguration<Retrabalho>
{
    public void Configure(EntityTypeBuilder<Retrabalho> builder)
    {
        // Sem unicidade: o recálculo a partir do histórico renumera os retrabalhos na mesma gravação.
        builder.HasIndex(retrabalho => new { retrabalho.TarefaId, retrabalho.Numero });
        builder.HasIndex(retrabalho => retrabalho.ReabertaEm);
        builder.Property(retrabalho => retrabalho.AsanaStoryGid).HasMaxLength(Retrabalho.GidMaxLength);
        builder.HasIndex(retrabalho => retrabalho.AsanaStoryGid).IsUnique().HasFilter("asana_story_gid is not null");
        builder.Property(retrabalho => retrabalho.ReabertaPor).HasMaxLength(Retrabalho.ReabertaPorMaxLength);
    }
}

/// <summary>
/// Mapping of <see cref="Sincronizacao"/> to the <c>sincronizacao</c> table
/// </summary>
internal sealed class SincronizacaoConfiguration : IEntityTypeConfiguration<Sincronizacao>
{
    public void Configure(EntityTypeBuilder<Sincronizacao> builder)
    {
        builder.HasIndex(sincronizacao => sincronizacao.IniciadaEm);
        builder.Property(sincronizacao => sincronizacao.Tipo)
            .HasConversion(new EnumSnakeCaseConverter<TipoSincronizacao>())
            .HasMaxLength(Sincronizacao.TipoMaxLength)
            .HasDefaultValue(TipoSincronizacao.Sincronizacao)
            .ValueGeneratedNever();
    }
}

/// <summary>
/// Mapping of <see cref="TarefaBranch"/> to the <c>tarefa_branch</c> table
/// </summary>
internal sealed class TarefaBranchConfiguration : IEntityTypeConfiguration<TarefaBranch>
{
    public void Configure(EntityTypeBuilder<TarefaBranch> builder)
    {
        builder.Property(branch => branch.Tipo)
            .HasConversion(new EnumSnakeCaseConverter<TipoBranch>())
            .HasMaxLength(TarefaBranch.TipoMaxLength);
        builder.Property(branch => branch.Nome).HasMaxLength(TarefaBranch.NomeMaxLength);
        builder.Property(branch => branch.AsanaTagGid).HasMaxLength(TarefaBranch.GidMaxLength);
        builder.HasIndex(branch => new { branch.TarefaId, branch.Nome }).IsUnique();

        builder.HasOne<Retrabalho>()
            .WithMany()
            .HasForeignKey(branch => branch.RetrabalhoId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Ignore(branch => branch.Enviada);
        builder.Ignore(branch => branch.Partes);
        builder.Ignore(branch => branch.Tag);
        builder.Ignore(branch => branch.TemWorktreeAtivo);

        builder.HasOne(branch => branch.Worktree)
            .WithOne()
            .HasForeignKey<Worktree>(worktree => worktree.TarefaBranchId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

/// <summary>
/// Mapping of <see cref="Worktree"/> to the <c>worktree</c> table
/// </summary>
internal sealed class WorktreeConfiguration : IEntityTypeConfiguration<Worktree>
{
    public void Configure(EntityTypeBuilder<Worktree> builder)
    {
        builder.Property(worktree => worktree.Pasta).HasMaxLength(Worktree.CaminhoMaxLength);

        builder.HasMany(worktree => worktree.Partes)
            .WithOne()
            .HasForeignKey(parte => parte.WorktreeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Ignore(worktree => worktree.Ativo);
        builder.Ignore(worktree => worktree.PartesEmOrdem);
        builder.Ignore(worktree => worktree.Situacao);
    }
}

/// <summary>
/// Mapping of <see cref="WorktreeParte"/> to the <c>worktree_parte</c> table
/// </summary>
internal sealed class WorktreeParteConfiguration : IEntityTypeConfiguration<WorktreeParte>
{
    public void Configure(EntityTypeBuilder<WorktreeParte> builder)
    {
        builder.Property(parte => parte.Parte)
            .HasConversion(new EnumSnakeCaseConverter<ParteBranch>())
            .HasMaxLength(WorktreeParte.ParteMaxLength);
        builder.Property(parte => parte.Situacao)
            .HasConversion(new EnumSnakeCaseConverter<SituacaoWorktree>())
            .HasMaxLength(WorktreeParte.SituacaoMaxLength);
        builder.Property(parte => parte.Caminho).HasMaxLength(Worktree.CaminhoMaxLength);
        builder.Property(parte => parte.UltimoErro).HasMaxLength(WorktreeParte.ErroMaxLength);

        builder.HasIndex(parte => new { parte.WorktreeId, parte.Parte }).IsUnique();
    }
}

/// <summary>
/// Mapping of <see cref="Analise"/> to the <c>analise</c> table
/// </summary>
internal sealed class AnaliseConfiguration : IEntityTypeConfiguration<Analise>
{
    public void Configure(EntityTypeBuilder<Analise> builder)
    {
        builder.HasIndex(analise => new { analise.TarefaId, analise.Versao }).IsUnique();
        builder.Property(analise => analise.Modelo).HasMaxLength(Analise.ModeloMaxLength);

        builder.HasOne<Tarefa>()
            .WithMany()
            .HasForeignKey(analise => analise.TarefaId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(analise => analise.Itens)
            .WithOne()
            .HasForeignKey(item => item.AnaliseId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

/// <summary>
/// Mapping of <see cref="AnaliseItem"/> to the <c>analise_item</c> table
/// </summary>
internal sealed class AnaliseItemConfiguration : IEntityTypeConfiguration<AnaliseItem>
{
    public void Configure(EntityTypeBuilder<AnaliseItem> builder)
    {
        builder.Property(item => item.Tipo)
            .HasConversion(new EnumSnakeCaseConverter<TipoItemAnalise>())
            .HasMaxLength(AnaliseItem.TipoMaxLength);
        builder.Property(item => item.Grupo).HasMaxLength(AnaliseItem.GrupoMaxLength);
        builder.Property(item => item.Origem).HasMaxLength(AnaliseItem.OrigemMaxLength);
        builder.Property(item => item.Trecho).HasMaxLength(AnaliseItem.TrechoMaxLength);
        builder.HasIndex(item => new { item.AnaliseId, item.Tipo, item.Ordem }).IsUnique();
    }
}

/// <summary>
/// Mapping of <see cref="Conclusao"/> to the <c>conclusao</c> table
/// </summary>
internal sealed class ConclusaoConfiguration : IEntityTypeConfiguration<Conclusao>
{
    public void Configure(EntityTypeBuilder<Conclusao> builder)
    {
        builder.Property(conclusao => conclusao.AsanaStoryGid).HasMaxLength(Conclusao.GidMaxLength);
        builder.HasIndex(conclusao => conclusao.AsanaStoryGid).IsUnique();
        builder.Property(conclusao => conclusao.ConcluidaPor).HasMaxLength(Conclusao.ConcluidaPorMaxLength);
        builder.HasIndex(conclusao => new { conclusao.TarefaId, conclusao.ConcluidaEm });
    }
}

/// <summary>
/// Mapping of <see cref="Anotacao"/> to the <c>anotacao</c> table
/// </summary>
internal sealed class AnotacaoConfiguration : IEntityTypeConfiguration<Anotacao>
{
    public void Configure(EntityTypeBuilder<Anotacao> builder)
    {
        builder.HasOne<Tarefa>()
            .WithMany()
            .HasForeignKey(anotacao => anotacao.TarefaId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
