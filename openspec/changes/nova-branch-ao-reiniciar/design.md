# Design

## Context

- Esta change parte do código da `criar-branch-ao-iniciar`, que ainda não foi commitada nem arquivada: `BranchesNoGitHub` (`ConferirBaseAsync`, `CriarImplementacaoAsync`), `ParteBranch`, `Tarefa.Iniciar(base, impl, prazo, partes)` e `Tarefa.AdicionarBranch(tipo, nome, partes)`.
- A reabertura no Asana é tratada por `Tarefa.RecalcularRetrabalhos`, que cria o `Retrabalho` e põe a tarefa em `retrabalho` pausada (`EmAndamento = false`) com `PrazoInternoPendente = true`. As branches anteriores continuam na tarefa.
- `Tarefa.Retomar()` só liga `EmAndamento`. `TarefaService.RetomarAsync` usa o `AplicarAsync` genérico, sem GitHub.
- `Tarefa.Desarquivar()` devolve a tarefa à `entrada` sem limpar as branches. `Tarefa.Iniciar` adiciona a base sem conferir se o nome já existe, e o índice único `(tarefa_id, nome)` de `tarefa_branch` recusa a gravação.
- `AdicionarBranch` vincula a branch ao retrabalho aberto mais recente (`RetrabalhosAbertos().MaxBy(Numero)`) e devolve `null` para nome repetido, que o serviço traduz em `TarefaResultado.BranchRepetida` (`409`).

## Goals / Non-Goals

**Goals:**
- Retomar com nova branch numa única chamada, com o mesmo fluxo do início: valida, confere todas as bases, cria e só então grava. Assim não sobra uma tarefa em andamento sem branch nem uma branch registrada com a tarefa pausada.
- Reaproveitar a base já registrada, no início e no retomar.

**Non-Goals:**
- Mudar o cálculo de retrabalhos, a conclusão ou o envio das tags.
- Endpoint novo. O corpo opcional de `retomar` cobre o caso.

## Decisions

### Corpo opcional em `retomar`, e não um endpoint novo
`RetomarTarefaRequest(BranchBase, BranchImplementacao, Partes)`, com as mesmas anotações de `IniciarTarefaRequest` (`[Required]`, `StringLength`, `MinLength(1)` em `Partes`), recebido no endpoint como `[FromBody] RetomarTarefaRequest? request`. Na Minimal API, o parâmetro anulável aceita requisição sem corpo. Quando o corpo vem, a validação dos campos é a de sempre (`400` com `errors`).

Alternativas consideradas:
- `POST /tarefas/{identifier}/retrabalho/iniciar`: foi descartada porque repetiria as regras de status do retomar e criaria uma segunda forma de pôr a tarefa em andamento.
- O front chamar `branches` duas vezes e depois `retomar`: foi descartada porque não é atômica e deixaria branches registradas com a tarefa pausada quando o retomar falhasse.

### Domínio: `Tarefa.RetomarComBranch`
```
Tarefa.RetomarComBranch(base, impl, partes) -> (TarefaBranch Base, bool BaseNova, TarefaBranch Implementacao)?
  1. mesmas validações de Retomar()            <- DomainException (status, bloqueada, já em andamento)
  2. NomeDaBranch(base), NomeDaBranch(impl), base != impl, GarantirPartes(partes)
  3. impl já registrada na tarefa              -> null (409)
  4. base registrada como implementacao        -> DomainException
  5. base registrada como base                 -> reaproveita (BaseNova = false)
     senão                                     -> nova TarefaBranch(Base, ..., retrabalhoAberto?.Id, partes)
  6. nova TarefaBranch(Implementacao, ..., retrabalhoAberto?.Id, partes)
  7. EmAndamento = true
```
O retrabalho é o mesmo de `AdicionarBranch` (aberto mais recente). Fora do retrabalho, as branches ficam sem vínculo.

A base reaproveitada mantém as partes com que foi registrada. A conferência no GitHub usa as partes do pedido, porque é nelas que a implementação é criada. Uma base `main` registrada só com `back` e reaproveitada num retomar com `front` continua com a tag `(Back) => main`. Isso é aceito: a base é a mesma branch, e reescrever as partes de uma branch possivelmente já enviada ao Asana mudaria uma tag que já existe.

Alternativa considerada: registrar uma segunda base com o mesmo nome. Foi descartada porque o índice único `(tarefa_id, nome)` existe justamente para que cada nome vire uma só tag.

### `Iniciar` com base reaproveitada
`Tarefa.Iniciar` passa a aplicar os passos 3 a 5 acima antes de mudar o status. Para isso, o retorno passa a indicar a implementação repetida (`null`, traduzido em `409`, como no `AdicionarBranch`). Na entrada não há retrabalho aberto, então as branches ficam sem vínculo, como hoje.

Para não repetir o código, os passos 2 a 6 ficam num método privado, `RegistrarBranchesDoCiclo`, usado por `Iniciar` e por `RetomarComBranch`. Cada método público mantém as próprias regras de status e de prazo.

### Serviço
`TarefaService.RetomarAsync(identifier, RetomarTarefaRequest? request, ct)`:
- sem corpo: continua usando `AplicarAsync(t => t.Retomar())`;
- com corpo: carrega, chama `RetomarComBranch` (`null` → `BranchRepetida`), chama `BranchesNoGitHub.CriarImplementacaoAsync(empresa, partes do pedido, base, impl)` e só então `SaveChangesAsync`.

A falha do GitHub sai antes do `SaveChangesAsync`. Como a entidade foi alterada só em memória e o `DbContext` é por requisição, nada é gravado.

A interface `ITarefaService.RetomarAsync` muda de assinatura, o que significa atualizar `<modified>` no XML doc.

### `novaBranchSugerida`
É uma propriedade calculada em `Tarefa`, `NovaBranchSugerida`: `Status == Retrabalho` e o retrabalho aberto mais recente não tem `TarefaBranch` de tipo `Implementacao` com `RetrabalhoId` igual ao seu `Id`. É devolvida em `TarefaDetalheResponse`, preenchida onde o detalhe é montado (`PainelService`, que já carrega retrabalhos e branches). A regra fica na API, e o front só lê o campo.

Alternativa considerada: o front deduzir a sugestão a partir de `retrabalhos` e `branches`. Foi descartada porque reimplementaria uma regra de domínio no front.

### Banco
Sem alteração de schema e sem migration. `tarefa_branch.retrabalho_id`, `parte_front` e `parte_back` já existem.

## Risks / Trade-offs

- [Branch órfã no GitHub quando o retomar falha depois de criar num repositório] → Igual ao início: ela é aceita como "já existe" na nova tentativa.
- [Usuário escolhe "Continuar na branch atual" e depois quer a branch nova] → "Nova branch" continua disponível, e `novaBranchSugerida` segue `true` até existir uma implementação vinculada ao retrabalho. O front volta a oferecer a opção no próximo Iniciar.
- [Base reaproveitada com partes diferentes do pedido] → A tag da base não muda (ver Decisões). A implementação nova carrega as partes certas.
- [Retomar com corpo em tarefa pausada comum, sem retrabalho] → É aceito (as branches ficam sem vínculo), mas o front só oferece o diálogo quando `novaBranchSugerida = true`.

## Migration Plan

1. Implementar e publicar depois da `criar-branch-ao-iniciar`, que fornece o cliente do GitHub.
2. Publicar a API antes do front ou junto com ele. O front antigo continua funcionando, porque o corpo é opcional.

Rollback: reverter o código. Nada é migrado, e as branches registradas pelo retomar continuam válidas.
