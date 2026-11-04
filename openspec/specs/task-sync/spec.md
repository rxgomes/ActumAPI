# task-sync Specification

## Purpose

Define como o Actum mantém uma cópia local das tarefas do Asana atribuídas ao usuário, por empresa (workspace), atualizada sob demanda, sem nunca alterar no Asana os dados de gerenciamento interno.

## Requirements

### Requirement: Sincronização sob demanda
A API SHALL expor `POST /sincronizacoes`, que sincroniza todas as empresas com `sincronizar = true` e responde com o resumo: quantidade de tarefas novas, atualizadas, arquivadas e reaberturas detectadas.

#### Scenario: Sincronização bem-sucedida
- **WHEN** um cliente chama `POST /sincronizacoes` e o Asana responde normalmente
- **THEN** a resposta MUST ter status `200` com os totais, e a sincronização MUST ser registrada com `sucesso = true`

#### Scenario: Sincronização simultânea
- **WHEN** uma sincronização está em andamento e um cliente chama `POST /sincronizacoes`
- **THEN** a resposta MUST ter status `409` no formato Problem Details

#### Scenario: Falha no Asana
- **WHEN** o Asana fica indisponível durante a sincronização
- **THEN** a sincronização MUST ser registrada com `sucesso = false` e o erro, e as empresas já processadas MUST permanecer gravadas

#### Scenario: Última sincronização
- **WHEN** um cliente chama `GET /sincronizacoes/ultima` depois de pelo menos uma sincronização
- **THEN** a resposta MUST ter status `200` com data, resultado e totais da sincronização mais recente

### Requirement: Empresas a partir dos workspaces
Cada workspace acessível pelo usuário no Asana SHALL corresponder a uma empresa no Actum, criada na primeira sincronização em que aparecer, com `sincronizar = true` e uma cor atribuída.

#### Scenario: Primeiro uso
- **WHEN** a primeira sincronização é executada e o usuário tem os workspaces Aureum-ERP e aremas.com.br
- **THEN** MUST existir uma empresa para cada workspace, com o nome do workspace e cores diferentes

#### Scenario: Empresa desligada
- **WHEN** uma empresa está com `sincronizar = false`
- **THEN** a sincronização MUST NOT consultar as tarefas desse workspace

#### Scenario: Ajuste da empresa
- **WHEN** um cliente chama `PATCH /empresas/{identifier}` com uma nova cor ou com `sincronizar = false`
- **THEN** a empresa MUST ser atualizada, e `GET /empresas` MUST refletir a mudança

### Requirement: Apenas tarefas abertas atribuídas ao usuário
A sincronização SHALL considerar somente as tarefas não concluídas atribuídas ao usuário em cada workspace.

#### Scenario: Tarefa de outro responsável
- **WHEN** uma tarefa do workspace está atribuída a outra pessoa
- **THEN** ela MUST NOT ser criada no Actum

### Requirement: Carga inicial
Na primeira sincronização de cada empresa, as tarefas abertas SHALL ser criadas com status `em_andamento` e prazo interno igual ao prazo do Asana, para aparecerem como próximas ou atrasadas.

#### Scenario: Tarefa com prazo vencido na carga inicial
- **WHEN** a primeira sincronização de uma empresa encontra uma tarefa aberta com prazo no Asana anterior a hoje
- **THEN** a tarefa MUST ser criada com status `em_andamento` e `prazo_interno` igual ao prazo do Asana

#### Scenario: Tarefa sem prazo na carga inicial
- **WHEN** a primeira sincronização de uma empresa encontra uma tarefa aberta sem prazo no Asana
- **THEN** a tarefa MUST ser criada com status `em_andamento` e sem prazo interno

#### Scenario: Comentários da carga inicial
- **WHEN** uma tarefa é criada na carga inicial
- **THEN** seus comentários existentes MUST ser importados como lidos

#### Scenario: Carga inicial só uma vez
- **WHEN** a primeira sincronização de uma empresa termina com sucesso
- **THEN** as sincronizações seguintes dessa empresa MUST NOT aplicar a regra de carga inicial

### Requirement: Novas tarefas após a carga inicial
Depois da carga inicial da empresa, toda tarefa aberta ainda inexistente no Actum SHALL ser criada com status `entrada`, sem prazo interno. Conclusões e reaberturas anteriores à criação MUST ser gravadas como histórico (conclusões e retrabalhos), sem mudar o status `entrada`.

#### Scenario: Tarefa nova atribuída
- **WHEN** uma tarefa nova é atribuída ao usuário no Asana e a sincronização é executada
- **THEN** a tarefa MUST ser criada com status `entrada`, `prazo_interno` nulo e o prazo do Asana em `prazo_asana`

#### Scenario: Tarefa concluída antes do Actum e reaberta
- **WHEN** uma tarefa concluída no Asana antes de existir no Actum é reaberta e a sincronização é executada
- **THEN** a tarefa MUST ser criada com status `entrada` e um retrabalho de histórico para esse par, sem prazo pendente

### Requirement: Atualização dos dados vindos do Asana
Para tarefas já existentes, a sincronização SHALL atualizar título, descrição, projetos, link e prazo do Asana, e MUST NOT alterar prazo interno, prioridade nem status por causa dessas mudanças.

#### Scenario: Prazo alterado no Asana
- **WHEN** o prazo de uma tarefa é alterado no Asana e a sincronização é executada
- **THEN** `prazo_asana` MUST ser atualizado e `prazo_interno` MUST permanecer igual

#### Scenario: Sincronização repetida
- **WHEN** a sincronização é executada duas vezes seguidas sem mudanças no Asana
- **THEN** nenhuma tarefa, comentário ou retrabalho MUST ser duplicado

### Requirement: Comentários
A sincronização SHALL gravar os comentários novos das tarefas modificadas no Asana desde a sincronização anterior, como não lidos.

#### Scenario: Novo comentário
- **WHEN** alguém comenta em uma tarefa já existente no Actum e a sincronização é executada
- **THEN** o comentário MUST ser gravado com autor, texto, data e `lido = false`

### Requirement: Arquivamento
Uma tarefa local que não está `concluida` nem `arquivada` e não vem mais na lista de abertas do usuário SHALL ser arquivada, com o motivo: excluída, concluída no Asana ou reatribuída.

#### Scenario: Tarefa concluída por outra pessoa
- **WHEN** uma tarefa `em_andamento` é concluída diretamente no Asana e a sincronização é executada
- **THEN** a tarefa MUST ficar `arquivada` com motivo `concluida_no_asana`

#### Scenario: Tarefa reatribuída
- **WHEN** uma tarefa `entrada` é atribuída a outra pessoa no Asana e a sincronização é executada
- **THEN** a tarefa MUST ficar `arquivada` com motivo `reatribuida`

#### Scenario: Tarefa excluída
- **WHEN** uma tarefa é excluída no Asana e a sincronização é executada
- **THEN** a tarefa MUST ficar `arquivada` com motivo `excluida`

#### Scenario: Tarefa aguardando teste
- **WHEN** uma tarefa está `concluida` no Actum e não vem na lista de abertas
- **THEN** ela MUST permanecer `concluida`, sem consulta individual ao Asana

#### Scenario: Tarefa arquivada que volta
- **WHEN** uma tarefa arquivada como reatribuída volta a ser atribuída ao usuário
- **THEN** a tarefa MUST voltar para o status `entrada`
