# Tasks

## 1. Actum.Modules.Claude

- [x] 1.1 Em `Contracts/DemandaParaAnalise.cs`, adicionar `RegraDaDemanda(string Texto, string Origem)` e o campo `IReadOnlyList<RegraDaDemanda> Regras` em `DemandaEstruturada`, com XML doc. Verificar com `dotnet build`.
- [x] 1.2 Em `Application/PromptDaDemanda.cs`:
  - reescrever o parágrafo dos comentários e incluir a seção `regras` no `Sistema` (decisões 4 e 5 do design);
  - adicionar `regras` (`texto`, `origem`, obrigatórios) ao `Esquema`;
  - passar o atributo `data` dos comentários para `dd/MM/yyyy HH:mm`.

  Atualizar `tests/Actum.Modules.Claude.Tests/PromptDaDemandaTests.cs` (formato da data, `regras` no esquema e menção a regras e sugestões nas instruções). Verificar que `dotnet test tests/Actum.Modules.Claude.Tests` passa.
- [x] 1.3 Em `Application/AnalisadorDeDemanda.cs`, ler `regras` da resposta (`RespostaJson`) e preencher `DemandaEstruturada.Regras`. Uma resposta sem `regras` vira lista vazia. Cobrir em `AnalisadorDeDemandaTests.cs` a resposta com regras e sem regras. Verificar que `dotnet test tests/Actum.Modules.Claude.Tests` passa.

## 2. Actum.Modules.Tarefas: banco e domínio

- [x] 2.1 Adicionar `TipoItemAnalise.Regra` (doc: "marcada significa atendida"), `AnaliseItem.Origem` (`OrigemMaxLength = 200`, truncada no construtor) e o mapeamento em `AnaliseItemConfiguration`. Em `Analise.Criar`, receber `regras` (texto e origem) e adicioná-las depois das dúvidas, com a mesma herança de marcação. Verificar com `dotnet build`.
- [x] 2.2 Gerar a migration `20261105_AnaliseItemOrigem` (só `add column origem varchar(200) null` em `tarefas.analise_item`) e aplicá-la no banco local. Verificar com `select origem from tarefas.analise_item limit 1`, sem aspas.

## 3. Actum.Modules.Tarefas: serviço e contratos

- [x] 3.1 Em `Contracts/AnaliseContracts.cs`, adicionar `Regras` em `AnaliseResponse` e `Origem` em `AnaliseItemResponse` (anulável, preenchida só nas regras), com `<param>` atualizados (`regra` no `Tipo`). Mapear em `AnaliseService.ParaResposta`. Verificar com `dotnet build`.
- [x] 3.2 Em `AnaliseService.AnalisarAsync`, injetar `IAsanaClient` e ler a tarefa com tracking. Depois das verificações de arquivada e de análise em andamento, chamar `ListarStoriesAsync`, `AdicionarComentarios(stories, lidos: false)` e `SaveChangesAsync`, e então montar a demanda com os comentários atualizados e gravar as regras. Uma falha do Asana sobe sem gravar nada e libera a tarefa. Verificar com `dotnet build`.
- [x] 3.3 Em `tests/Actum.Modules.Tarefas.Tests/AnaliseTests.cs` e no `AnalisadorDeDemandaFalso` (que passa a aceitar regras), cobrir:
  - comentário novo no Asana entra no contexto e fica gravado como não lido;
  - comentários já gravados não duplicam e continuam lidos;
  - falha do Asana não chama a IA, não grava versão e libera a tarefa (se o Asana falso ainda não simula falha em `ListarStoriesAsync`, adicionar isso);
  - tarefa inexistente, arquivada e análise em andamento não chamam o Asana;
  - regras gravadas em ordem e com origem;
  - marcação e herança de marcação de regra;
  - versão sem regras devolvida com lista vazia.

  Verificar que `dotnet test tests/Actum.Modules.Tarefas.Tests` passa.

## 4. Verificação

- [x] 4.1 Com a API rodando, conferir no `openapi/v1.json` os campos `regras` e `origem` e o valor `regra` em `TipoItemAnalise`. Em uma tarefa real com comentários contendo regras, conferir em `POST /tarefas/{identifier}/analises` que um comentário feito no Asana depois da última sincronização aparece no detalhe e que as regras vêm com a origem correta.

## Workflow follow-up

- Fazer a change `comentarios-na-analise` no ActumUI (tipos, seção "Regras e observações" com origem e marcação).
- Arquivar a change com `/opsx:archive` após revisão.
