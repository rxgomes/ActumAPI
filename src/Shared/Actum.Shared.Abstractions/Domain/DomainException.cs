namespace Actum.Shared.Abstractions.Domain;

/// <summary>
/// Business rule or domain validation violated. The message is in pt-BR and is meant to be shown to the user.
/// </summary>
/// <param name="message">Violation description, in pt-BR</param>
public sealed class DomainException(string message) : Exception(message);
