using Actum.Modules.Asana;
using Actum.Modules.Claude;
using Actum.Modules.Tarefas;
using Actum.Shared.Abstractions;

namespace Actum.Api.Modules;

/// <summary>
/// Lista explícita dos módulos de negócio compostos pelo host.
/// Para adicionar um módulo, inclua uma instância dele em <see cref="All"/>.
/// </summary>
internal static class ModuleCatalog
{
    public static readonly IReadOnlyList<IModule> All =
    [
        new AsanaModule(),
        new ClaudeModule(),
        new TarefasModule(),
    ];
}
