# Proposal

## Why

As demandas chegam pelo Asana como texto livre: uma descrição longa, comentários espalhados e, nas tarefas reabertas, o pedido de ajuste misturado ao histórico. Antes de começar, o usuário precisa ler tudo e montar mentalmente o que é para fazer e o que ainda não está claro. A Fase 2 (docs/especificacao-fase1.md) usa a Claude API para estruturar a demanda em **objetivo**, **checklist agrupado por área** (com aviso nos trechos vagos), **testes** e **dúvidas**, gravados no Actum e acompanhados durante a execução.

## What Changes

- Novo módulo `Actum.Modules.Claude` (sem banco), análogo ao módulo `Asana`: encapsula o SDK oficial da Anthropic e expõe o contrato público `IAnalisadorDeDemanda`, que recebe o contexto da tarefa e devolve objetivo, checklist agrupado por área (ex.: Front, Back: cálculo, Back: integração) com aviso nos itens vagos, testes e dúvidas.
- Chave da API lida de `Claude:ApiKey` (dotnet user-secrets), nunca logada ou devolvida. Modelo padrão `claude-opus-5-5`, configurável.
- `GET /claude/situacao`: informa se a chave está configurada e qual modelo será usado, sem chamar a Anthropic (para o front explicar como configurar).
- **Análise sob demanda** no módulo `Tarefas`: `POST /tarefas/{identifier}/analises` envia à Anthropic o contexto completo da tarefa (título, descrição, empresa, projetos, comentários e histórico de retrabalho) e grava o resultado como uma nova **versão** da análise.
- `GET /tarefas/{identifier}/analises`: todas as versões, da mais recente para a mais antiga, com os itens.
- `PATCH /tarefas/{identifier}/analises/itens/{itemIdentifier}`: marca ou desmarca um item da versão atual (item do checklist como feito, teste como realizado, dúvida como resolvida).
- Na reanálise (por exemplo, depois de uma reabertura), itens com o mesmo texto de itens já marcados na versão anterior entram marcados.
- Falhas da Anthropic viram Problem Details em pt-BR (`400` para chave ausente/recusada e recusa do modelo; `502` para indisponibilidade, limite de uso e resposta incompleta).

## Capabilities

### New Capabilities

- `claude-client`: acesso à Claude API (configuração da chave e do modelo, situação da configuração, chamada com saída estruturada e tradução de falhas).
- `task-analysis`: análise da demanda da tarefa com IA, versões da análise e marcação dos itens.

### Modified Capabilities

Nenhuma.

## Impact

- **Módulos afetados:** novo `Actum.Modules.Claude` (registrado no `ModuleCatalog`); `Actum.Modules.Tarefas` passa a depender do contrato público `IAnalisadorDeDemanda` do módulo `Claude` (mesmo padrão da dependência de `IAsanaClient`).
- **Dependência nova:** pacote NuGet `Anthropic` (SDK oficial em C#), apenas no módulo `Claude`.
- **Contrato da API:** novos endpoints `GET /claude/situacao`, `POST /tarefas/{identifier}/analises`, `GET /tarefas/{identifier}/analises` e `PATCH /tarefas/{identifier}/analises/itens/{itemIdentifier}`. Nenhum contrato existente muda (não breaking).
- **Impacto no front (`..\ActumUI`):** nova seção *Análise com IA* no detalhe da tarefa (botão *Analisar com IA* / *Reanalisar*, objetivo, checklist por grupo com os avisos de trecho vago, testes e dúvidas marcáveis, versões anteriores, aviso quando a chave não está configurada). Será uma change própria no ActumUI, depois desta.
- **Banco:** schema `tarefas` ganha as tabelas `analise` e `analise_item` (migrations `20261103_Analise` e `20261103_AnaliseGrupos`).
- **Custo e privacidade:** cada análise é uma chamada paga à Anthropic e envia o conteúdo da tarefa (inclusive comentários de terceiros) para fora da máquina. Só acontece quando o usuário clica; os tokens consumidos ficam gravados na análise.

### Fora do escopo

- Análise automática na sincronização ou na reabertura.
- Editar o texto do objetivo, dos itens ou incluir itens manuais.
- Responder dúvidas no Actum ou enviá-las ao Asana como comentário (Fase 3).
- Anexos e imagens da tarefa no contexto enviado à IA.
- Streaming da resposta para o front, painel de custos e outros provedores de IA.
