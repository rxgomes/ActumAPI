# Tasks

## 1. Pré-requisitos

- [x] 1.1 Concluir e commitar o trabalho em andamento (lista/download de anexos, trecho das dúvidas, migration `20261113_AnaliseItemTrecho`); verificar com `dotnet build` e `dotnet test` passando e `git status` limpo nesses arquivos
- [x] 1.2 Adicionar `DocumentFormat.OpenXml` em `Directory.Packages.props` e referenciá-lo em `Actum.Modules.Claude.csproj`; verificar com `dotnet build`

## 2. Módulo Asana

- [x] 2.1 Criar `AsanaConteudoDoAnexo` (Contracts) e `IAsanaClient.BaixarAnexoAsync(anexoGid, tamanhoMaximo, ct)` com XML doc completo (`<author>`, `<created>`, `<modified>`)
- [x] 2.2 Implementar no `AsanaClient` numa requisição sem `Authorization` (cabeçalhos redigidos, só `https`, tempo limite de 2 min), leitura em streaming com corte em `tamanhoMaximo` (`AsanaAnexoGrandeDemaisException` → `400`) e falhas como indisponibilidade (`502`); verificar em `tests/Actum.Modules.Asana.Tests/AnexosDoAsanaTests.cs`: conteúdo devolvido, nenhum `Authorization` no download, anexo inexistente → `null`, arquivo acima do máximo e erro no download
- [x] 2.3 Atualizar `AsanaClientFalso` (testes de Tarefas) com o novo método; verificar com `dotnet build` dos testes

## 3. Módulo Claude

- [x] 3.1 Criar `IArquivosDaAnalise` (contrato público, XML doc) e a implementação com tipos por extensão, motivos em pt-BR e limites de `Claude:Arquivos` (padrões 20/5/20 MB e 300.000 caracteres); registrar no módulo; verificar com testes de legibilidade: externo, tipo não suportado (`.mp4`, `.pptx`, `.doc`), grande demais por tipo, tamanho desconhecido legível
- [x] 3.2 Criar `ExtratorDeTexto` (Word: parágrafos e tabelas em ordem; Excel: planilhas com `## nome` e linhas separadas por tabulação, com strings compartilhadas e valores de fórmula; texto UTF-8) e `ClaudeArquivoIlegivelException` (`400`); verificar com testes usando `.docx`/`.xlsx` gerados no próprio teste pelo OpenXml, arquivo corrompido e texto acima do limite (erro, sem corte)
- [x] 3.3 Alterar `DemandaParaAnalise` (descrição, comentários e retrabalhos anuláveis, `Prompt`, `Arquivos`), `ItemDoChecklist` (`Origem`, `Trecho`) e criar `ArquivoDaDemanda`; atualizar o XML doc
- [x] 3.4 Alterar `PromptDaDemanda`: sistema com as novas fontes, modo prompt, origem/trecho no checklist, origens `anexo <nome>`/`prompt` e aviso de que o conteúdo é material da demanda; esquema com `origem`/`trecho` obrigatórios no checklist; `MontarMensagem` devolvendo os blocos (PDF/imagem nativos com o nome antes, texto extraído em `<arquivo nome>`, `<prompt_do_usuario>`, blocos omitidos quando a fonte é `null`); verificar em `PromptDaDemandaTests`: sem corpo igual ao atual, só anexos sem descrição/comentários, modo prompt sem descrição, ordem dos arquivos, esquema do checklist
- [x] 3.5 Alterar `AnalisadorDeDemanda`: enviar os blocos montados, ler `origem`/`trecho` do checklist (vazios → ausentes), traduzir `400` da Anthropic em requisição com arquivos para `ClaudeArquivoRejeitadoException` (`400`) e subir `MaxTokensPadrao` para 32.000; verificar em `AnalisadorDeDemandaTests` (com `AnthropicFalso`) que a requisição tem os blocos `document`/`image` em base64, que o checklist sai com origem/trecho e que o `400` vira a mensagem em pt-BR; atualizar `ClaudeExceptionHandler` se preciso

## 4. Módulo Tarefas

- [x] 4.1 Criar os contratos `FontesDaAnaliseRequest` (com `ErrorMessage` em pt-BR), `ModoDaAnalise` (snake_case), `FontesDaAnaliseResponse`, `AnexoDaFonteResponse`; acrescentar `Fontes` em `AnaliseResponse` e `LegivelNaAnalise`/`Motivo` em `AnexoResponse`; atualizar o XML doc (OpenAPI)
- [x] 4.2 Domínio: `Analise` com as fontes (modo, descrição, comentários, prompt e a coleção `FonteAnexo` com ordem, gid e nome) e `Analise.Criar` recebendo fontes e origem/trecho do checklist; verificar em `AnaliseTests` que a versão guarda as fontes e que o checklist guarda origem/trecho, e que a marcação herdada continua igual
- [x] 4.3 Persistência: mapear as colunas `fonte_*` em `analise` e `analise_fonte_anexo` (`OwnsMany`, FK em cascata, índice único `(analise_id, ordem)`); gerar a migration `20261118_AnaliseFontes`; verificar o SQL gerado (`dotnet ef migrations script`) com nomes em snake_case e aplicar no banco local
- [x] 4.4 `AnexoService.ListarAsync`: preencher `LegivelNaAnalise`/`Motivo` com `IArquivosDaAnalise`; verificar em `AnexoTests` com anexo legível, externo e de tipo não suportado
- [x] 4.5 `AnaliseService.AnalisarAsync(identifier, fontes, ct)`: validação (`FontesInvalidas` com erros em `Fontes`/`Anexos`/`Prompt`), conferência dos anexos na lista da tarefa e da legibilidade, limite total, download sequencial com `BaixarAnexoAsync` (conferindo a tarefa), montagem da demanda conforme as fontes e gravação das fontes; `ParaResposta` com `Fontes` (`null` para versões antigas); verificar em `AnaliseTests` (com os falsos): sem corpo = descrição + comentários, só anexos, modo prompt, comentários atualizados mesmo fora do contexto, cada erro de validação sem chamar o analisador, falha do Asana no download sem gravar versão
- [x] 4.6 `AnaliseEndpoints`: corpo opcional (`EmptyBodyBehavior.Allow`), `FontesInvalidas` → `ValidationProblem`, `.ProducesValidationProblem()` e descrição do POST atualizada; verificar em `tests/Actum.Api.Tests/AnaliseApiTests.cs`: `POST` sem corpo (`201`, compatível), com fontes (`201` e `fontes` na resposta), `400` com `errors.Fontes`/`errors.Anexos`/`errors.Prompt`, `GET` com `fontes` nulo para versão antiga

## 5. Documentação e verificação integrada

- [x] 5.1 Conferir em `http://localhost:5180/openapi/v1.json` (e no Scalar) o corpo do `POST /analises`, `AnaliseResponse.fontes`, `AnexoResponse.legivelNaAnalise`/`motivo` e as descrições em pt-BR; documentar a seção `Claude:Arquivos` onde as demais opções do Claude estão descritas
- [x] 5.2 Rodar `dotnet build` e `dotnet test` sem erros
- [ ] 5.3 Teste manual com a chave configurada numa tarefa com PDF, PNG, `.docx` e `.xlsx` anexados: análise sem corpo, só com anexos, descrição + anexos e por prompt citando um modelo; conferir as fontes gravadas, a origem `anexo <nome>` e o trecho nos itens do checklist, os tokens e que o front atual (sem corpo) continua funcionando

## Workflow follow-up

- Avisar o ActumUI para regenerar `src/api/schema.d.ts` e aplicar a change `fontes-da-analise` do front.
- Arquivar a change (`/opsx:archive`) após a revisão.
