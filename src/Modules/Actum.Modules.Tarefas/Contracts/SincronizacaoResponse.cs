namespace Actum.Modules.Tarefas.Contracts;

/// <summary>
/// Summary of a synchronization run
/// </summary>
/// <param name="Identifier">Synchronization identifier</param>
/// <param name="IniciadaEm">Local date and time of the start</param>
/// <param name="FinalizadaEm">Local date and time of the end; <see langword="null"/> while running</param>
/// <param name="Sucesso">Whether every company was synchronized</param>
/// <param name="TarefasNovas">Tasks created</param>
/// <param name="TarefasAtualizadas">Existing tasks changed in the Asana, or brought back from the archive</param>
/// <param name="TarefasArquivadas">Tasks archived</param>
/// <param name="Reaberturas">Reworks detected</param>
/// <param name="Erro">Failure message, in pt-BR, when <paramref name="Sucesso"/> is <see langword="false"/></param>
public sealed record SincronizacaoResponse(
    Guid Identifier,
    DateTime IniciadaEm,
    DateTime? FinalizadaEm,
    bool Sucesso,
    int TarefasNovas,
    int TarefasAtualizadas,
    int TarefasArquivadas,
    int Reaberturas,
    string? Erro);
