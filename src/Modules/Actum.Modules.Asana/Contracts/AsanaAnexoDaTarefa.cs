namespace Actum.Modules.Asana.Contracts;

/// <summary>
/// File attached to an Asana task (in the task itself or in one of its comments)
/// </summary>
/// <param name="Gid">Attachment identifier in the Asana</param>
/// <param name="Nome">File name</param>
/// <param name="CriadoEm">Local date and time of the upload</param>
/// <param name="Tamanho">Size in bytes; <see langword="null"/> when the Asana does not know it (external files)</param>
/// <param name="Hospedagem">Where the file is stored (<c>asana</c>, <c>gdrive</c>, <c>dropbox</c>...)</param>
public sealed record AsanaAnexoDaTarefa(string Gid, string Nome, DateTime CriadoEm, long? Tamanho, string Hospedagem);

/// <summary>
/// Where to download an attachment now
/// </summary>
/// <param name="Nome">File name</param>
/// <param name="TarefaGid">Identifier of the task (or other object) the file is attached to; <see langword="null"/> when unknown</param>
/// <param name="Endereco">
/// Download address (temporary, valid for a few minutes) or, for external files without download, the address to
/// open the file; <see langword="null"/> when the Asana offers neither
/// </param>
public sealed record AsanaDownloadDoAnexo(string Nome, string? TarefaGid, Uri? Endereco);
