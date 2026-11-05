# task-analysis Specification

## Purpose

Define a análise da demanda de uma tarefa com IA: geração sob demanda, gravação em versões, consulta e marcação dos itens do checklist, dos testes e das dúvidas.

## Requirements

### Requirement: Análise sob demanda
A API SHALL expor `POST /tarefas/{identifier}/analises`, que primeiro busca no Asana o histórico da tarefa e grava os comentários que ainda não estão no banco (como não lidos), depois monta o contexto completo da tarefa (empresa, título, descrição em texto, projetos, todos os comentários em ordem cronológica e os retrabalhos), chama `IAnalisadorDeDemanda` e grava o resultado como nova versão da análise. A análise MUST acontecer apenas por essa chamada, nunca na sincronização.

#### Scenario: Primeira análise
- **WHEN** a tarefa existe, não está arquivada e não tem análise, e um cliente chama `POST /tarefas/{identifier}/analises`
- **THEN** a resposta MUST ter status `201` com a análise `versao=1`, o objetivo, o checklist agrupado por área (grupos e itens em ordem, cada item com o aviso de trecho vago quando houver), os testes, as dúvidas e as regras em ordem (cada regra com a origem), todos não marcados, o modelo, os tokens e a data da análise

#### Scenario: Comentário feito no Asana depois da última sincronização
- **WHEN** a tarefa tem no Asana um comentário que ainda não está no banco e um cliente chama `POST /tarefas/{identifier}/analises`
- **THEN** o comentário MUST ser gravado como não lido e MUST fazer parte do contexto enviado ao analisador

#### Scenario: Comentários já gravados
- **WHEN** todos os comentários da tarefa no Asana já estão no banco
- **THEN** nenhum comentário MUST ser duplicado e os já lidos MUST continuar lidos

#### Scenario: Falha do Asana ao atualizar os comentários
- **WHEN** o Asana falha ao devolver o histórico da tarefa
- **THEN** a resposta MUST seguir o status e a mensagem do erro do cliente do Asana, sem chamar a Anthropic e sem gravar versão nem comentários

#### Scenario: Tarefa inexistente
- **WHEN** não existe tarefa com o `identifier`
- **THEN** a resposta MUST ter status `404`, sem chamar o Asana nem a Anthropic

#### Scenario: Tarefa arquivada
- **WHEN** a tarefa está arquivada
- **THEN** a resposta MUST ter status `400` com mensagem em pt-BR, sem chamar o Asana nem a Anthropic

#### Scenario: Análise já em andamento
- **WHEN** uma análise da mesma tarefa ainda está em andamento e outra é solicitada
- **THEN** a resposta MUST ter status `409`, sem chamar o Asana nem a Anthropic

#### Scenario: Falha da IA
- **WHEN** o analisador falha (chave, recusa, indisponibilidade ou resposta incompleta)
- **THEN** nenhuma versão MUST ser gravada e a resposta MUST seguir o status e a mensagem do erro do cliente da Claude API

### Requirement: Versões da análise
Cada análise SHALL gerar uma nova versão numerada por tarefa (1, 2, 3...). A versão mais recente é a **atual**; as anteriores ficam como histórico somente leitura.

#### Scenario: Reanálise
- **WHEN** a tarefa já tem a versão 1 e um cliente chama `POST /tarefas/{identifier}/analises`
- **THEN** a versão 2 MUST ser gravada como atual e a versão 1 MUST permanecer inalterada

#### Scenario: Marcações herdadas
- **WHEN** a nova versão tem um item do mesmo tipo e com o mesmo texto (sem diferenciar maiúsculas, acentos e espaços nas pontas) de um item marcado na versão anterior
- **THEN** o item da nova versão MUST entrar marcado, com a data da marcação original

### Requirement: Consulta das análises
A API SHALL expor `GET /tarefas/{identifier}/analises`, que devolve todas as versões da tarefa, da mais recente para a mais antiga, cada uma com o objetivo, os itens em ordem (checklist, testes, dúvidas e regras, cada regra com a origem), se é a atual e os metadados (modelo, tokens, data).

#### Scenario: Tarefa sem análise
- **WHEN** a tarefa existe e nunca foi analisada
- **THEN** a resposta MUST ter status `200` com lista vazia

#### Scenario: Tarefa inexistente na consulta
- **WHEN** não existe tarefa com o `identifier`
- **THEN** a resposta MUST ter status `404`

#### Scenario: Versão anterior às regras
- **WHEN** a versão foi gravada antes da existência da seção de regras
- **THEN** ela MUST ser devolvida com a lista de regras vazia

### Requirement: Marcação dos itens
A API SHALL expor `PATCH /tarefas/{identifier}/analises/itens/{itemIdentifier}` com `{ "concluido": true|false }`, que marca o item (checklist: feito; teste: realizado; dúvida: resolvida; regra: atendida) gravando a data e hora local da marcação, ou desmarca limpando a data. Nada é enviado ao Asana.

#### Scenario: Marcar item da versão atual
- **WHEN** o item pertence à versão atual da tarefa e um cliente envia `concluido=true`
- **THEN** a resposta MUST ter status `200` com o item marcado e a data da marcação

#### Scenario: Marcar regra
- **WHEN** o item é uma regra da versão atual e um cliente envia `concluido=true`
- **THEN** a resposta MUST ter status `200` com a regra marcada, a data da marcação e a origem

#### Scenario: Desmarcar item
- **WHEN** um item marcado da versão atual recebe `concluido=false`
- **THEN** a resposta MUST ter status `200` com o item não marcado e sem data

#### Scenario: Item de versão anterior
- **WHEN** o item pertence a uma versão que não é a atual
- **THEN** a resposta MUST ter status `400` com mensagem em pt-BR

#### Scenario: Item inexistente
- **WHEN** o item não existe ou não pertence à tarefa informada
- **THEN** a resposta MUST ter status `404`

#### Scenario: Corpo inválido
- **WHEN** `concluido` não é informado
- **THEN** a resposta MUST ter status `400` com o erro de validação em pt-BR no campo `concluido`

### Requirement: Regras e observações da demanda
A análise SHALL trazer, além do checklist, dos testes e das dúvidas, a seção de regras: regras de negócio, restrições, padrões a seguir e sugestões tiradas da descrição e dos comentários. Cada regra MUST ter a origem: `descrição` ou o comentário, com o autor e a data e hora (`dd/MM/yyyy HH:mm`).

#### Scenario: Regra combinada num comentário
- **WHEN** um comentário de Maria em `2026-11-04T14:30:00` diz "o desconto não pode passar de 10%"
- **THEN** a análise MUST ter uma regra com esse conteúdo e origem indicando o comentário de Maria em `04/11/2026 14:30`

#### Scenario: Pedido de ajuste num comentário
- **WHEN** um comentário pede uma ação a fazer, por exemplo "incluir também a filial 2"
- **THEN** o pedido MUST aparecer como item do checklist, e não só como regra

#### Scenario: Sem regras no contexto
- **WHEN** a descrição e os comentários não trazem regra, restrição nem sugestão
- **THEN** a lista de regras MUST vir vazia

#### Scenario: Marcação herdada de regra
- **WHEN** a nova versão tem uma regra com o mesmo texto de uma regra marcada na versão anterior
- **THEN** a regra da nova versão MUST entrar marcada, com a data da marcação original
