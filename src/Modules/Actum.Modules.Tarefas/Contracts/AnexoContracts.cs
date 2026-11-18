namespace Actum.Modules.Tarefas.Contracts;

/// <summary>
/// File attached to the task in the Asana (in the task itself or in one of its comments)
/// </summary>
/// <param name="Gid">Attachment identifier in the Asana (used in the download address)</param>
/// <param name="Nome">File name</param>
/// <param name="CriadoEm">Local date and time of the upload</param>
/// <param name="Tamanho">Size in bytes; <see langword="null"/> when the Asana does not know it (external files)</param>
/// <param name="Hospedagem">Where the file is stored (<c>asana</c>, <c>gdrive</c>, <c>dropbox</c>...)</param>
public sealed record AnexoResponse(string Gid, string Nome, DateTime CriadoEm, long? Tamanho, string Hospedagem);
