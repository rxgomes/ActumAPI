# claude-client Specification

## Purpose

Define como o Actum acessa a Claude API da Anthropic: configuração da chave e do modelo, verificação da configuração, chamada de estruturação da demanda com saída estruturada e tradução uniforme das falhas para mensagens em pt-BR.

## Requirements

### Requirement: Chave da API configurada e protegida
O cliente da Claude API SHALL ler a chave da configuração `Claude:ApiKey`. A chave MUST NOT ser registrada em logs, gravada no banco nem devolvida em respostas da API.

#### Scenario: Chave ausente
- **WHEN** `Claude:ApiKey` não está configurada e uma análise é solicitada
- **THEN** o cliente MUST falhar com erro de chave não configurada, com mensagem em pt-BR que indica o comando `dotnet user-secrets set "Claude:ApiKey" "<chave>"`, sem chamar a Anthropic

#### Scenario: Chave recusada
- **WHEN** a Anthropic responde `401` ou `403`
- **THEN** o cliente MUST falhar com erro de chave inválida, com mensagem em pt-BR, e a resposta da API MUST ter status `400`

#### Scenario: Conta sem créditos
- **WHEN** a Anthropic recusa a chamada por falta de créditos na conta (saldo insuficiente)
- **THEN** o cliente MUST falhar com erro de conta sem créditos, com mensagem em pt-BR orientando a adicionar créditos em Plans & Billing no Console da Anthropic, e a resposta da API MUST ter status `400` (tentar de novo não resolve)

### Requirement: Modelo configurável
O cliente SHALL usar o modelo da configuração `Claude:Modelo`, com padrão `claude-opus-5-5`, e o esforço da configuração `Claude:Esforco` (`low`, `medium`, `high` ou `max`), com padrão `medium`, enviado explicitamente em toda chamada.

#### Scenario: Sem configuração de modelo
- **WHEN** `Claude:Modelo` não está configurado e uma análise é feita
- **THEN** a chamada à Anthropic MUST usar o modelo `claude-opus-5-5` com esforço `medium`

### Requirement: Situação da configuração
A API SHALL expor `GET /claude/situacao`, que informa se a chave está configurada e o modelo em uso, sem chamar a Anthropic.

#### Scenario: Chave configurada
- **WHEN** `Claude:ApiKey` está configurada e um cliente chama `GET /claude/situacao`
- **THEN** a resposta MUST ter status `200`, `configurado=true` e o `modelo` em uso, e MUST NOT conter a chave

#### Scenario: Chave não configurada
- **WHEN** `Claude:ApiKey` não está configurada e um cliente chama `GET /claude/situacao`
- **THEN** a resposta MUST ter status `200` e `configurado=false`

### Requirement: Estruturação com saída estruturada
O contrato `IAnalisadorDeDemanda` SHALL receber o contexto da demanda (empresa, título, descrição, projetos, comentários com autor e data, retrabalhos com número, data e autor da reabertura) e devolver, em pt-BR, `objetivo`, `checklist`, `testes` e `duvidas`. A chamada MUST usar saída estruturada com JSON Schema, de modo que a resposta seja sempre um JSON nesse formato, e MUST devolver também o modelo que respondeu e os tokens de entrada e de saída.

#### Scenario: Demanda estruturada
- **WHEN** a Anthropic conclui a resposta (`stop_reason` = `end_turn`)
- **THEN** o analisador MUST devolver o objetivo, o checklist, os testes e as dúvidas do JSON, sem itens vazios, com o modelo e os tokens consumidos; item sem grupo MUST ir para o grupo `Geral` e `vago` vazio MUST virar ausente

#### Scenario: Tarefa reaberta
- **WHEN** o contexto tem retrabalhos
- **THEN** o pedido à IA MUST destacar o retrabalho mais recente e os comentários a partir dele, para que o checklist priorize o que foi pedido na reabertura

### Requirement: Checklist agrupado com trechos vagos
Cada item do `checklist` SHALL ter `grupo` (a área ou camada, como `Front`, `Back: cálculo` ou `Back: integração TecnoSpeed`), `texto` (a ação) e `vago` (o que está vago no pedido daquela ação, ausente quando está claro). Itens do mesmo grupo MUST vir juntos, na ordem de execução. Os `testes` MUST trazer como validar a entrega, com os dados de referência citados, sem repeti-los no checklist.

#### Scenario: Demanda com áreas e trecho vago
- **WHEN** a demanda pede ajustes no front e no back e um dos pedidos diz apenas "ver onde mais mexer"
- **THEN** o checklist MUST separar os itens nos grupos das áreas e o item desse pedido MUST trazer em `vago` o que falta definir

#### Scenario: Demanda sem áreas distintas
- **WHEN** a IA não indica grupo para um item
- **THEN** o item MUST ir para o grupo `Geral`

### Requirement: Respostas que não podem ser usadas
O analisador SHALL verificar o motivo de parada antes de ler o conteúdo e MUST NOT gravar resultado parcial.

#### Scenario: Recusa do modelo
- **WHEN** a Anthropic devolve `stop_reason` = `refusal` (mesmo após o fallback do servidor)
- **THEN** o analisador MUST falhar com mensagem em pt-BR informando que a IA não analisou a demanda, e a resposta da API MUST ter status `400`

#### Scenario: Resposta truncada
- **WHEN** a Anthropic devolve `stop_reason` = `max_tokens` ou um JSON que não segue o formato
- **THEN** o analisador MUST falhar com erro de resposta incompleta, e a resposta da API MUST ter status `502`

### Requirement: Indisponibilidade da Anthropic
Falhas transitórias SHALL ser repetidas pelo SDK (até 2 novas tentativas, com espera) e, se persistirem, MUST virar erro de indisponibilidade com mensagem em pt-BR.

#### Scenario: Limite de uso ou sobrecarga
- **WHEN** a Anthropic responde `429`, `529` ou `5xx` em todas as tentativas, ou a conexão falha
- **THEN** a resposta da API MUST ter status `502` com mensagem em pt-BR pedindo para tentar novamente em instantes
