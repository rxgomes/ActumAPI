namespace Actum.Modules.Tarefas.Application;

/// <summary>
/// File to attach to the Asana task, independent of how it arrived (form upload, tests)
/// </summary>
/// <param name="Nome">File name</param>
/// <param name="Tipo">Content type (e.g. <c>image/png</c>)</param>
/// <param name="Tamanho">Size in bytes</param>
/// <param name="Abrir">Opens the file content for reading</param>
public sealed record ArquivoParaEnviar(string Nome, string Tipo, long Tamanho, Func<Stream> Abrir);
