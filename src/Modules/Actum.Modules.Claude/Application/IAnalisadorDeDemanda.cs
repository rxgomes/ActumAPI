using Actum.Modules.Claude.Contracts;
using Actum.Modules.Claude.Errors;

namespace Actum.Modules.Claude.Application;

/// <summary>
/// Structures the demand of a task into objective, checklist and questions using the Claude API
/// </summary>
/// <author>Ricardo Gomes</author>
/// <created>2026-11-03</created>
/// <modified>2026-11-03</modified>
public interface IAnalisadorDeDemanda
{
    /// <summary>
    /// Send the task context to the Claude API and return the structured demand, in pt-BR. Fails with
    /// <see cref="ClaudeNaoConfiguradoException"/> (no call made), <see cref="ClaudeChaveInvalidaException"/>,
    /// <see cref="ClaudeSemCreditoException"/>, <see cref="ClaudeRecusaException"/>,
    /// <see cref="ClaudeRespostaIncompletaException"/> or
    /// <see cref="ClaudeIndisponivelException"/>; never returns a partial result.
    /// </summary>
    /// <param name="demanda">Task context</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Objective, checklist and questions, with the model that answered and the tokens consumed</returns>
    /// <author>Ricardo Gomes</author>
    /// <created>2026-11-03</created>
    /// <modified>2026-11-03</modified>
    Task<DemandaEstruturada> AnalisarAsync(DemandaParaAnalise demanda, CancellationToken cancellationToken);
}
