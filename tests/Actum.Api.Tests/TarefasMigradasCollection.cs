namespace Actum.Api.Tests;

/// <summary>
/// Classes de teste que aplicam as migrations do módulo Tarefas no banco temporário compartilhado. Ficam na mesma
/// collection para não rodarem em paralelo, senão duas aplicam a mesma migration ao mesmo tempo.
/// </summary>
[CollectionDefinition(Nome)]
public sealed class TarefasMigradasCollection
{
    public const string Nome = "Banco com o módulo Tarefas";
}
