namespace Actum.Modules.Tarefas.Domain;

/// <summary>
/// Status names in pt-BR, for messages shown to the user
/// </summary>
internal static class StatusTarefaTexto
{
    public static string Descrever(StatusTarefa status)
    {
        return status switch
        {
            StatusTarefa.Entrada => "entrada",
            StatusTarefa.EmAndamento => "em andamento",
            StatusTarefa.Bloqueada => "bloqueada",
            StatusTarefa.Concluida => "concluída",
            StatusTarefa.Retrabalho => "retrabalho",
            StatusTarefa.Arquivada => "arquivada",
            _ => status.ToString(),
        };
    }
}
