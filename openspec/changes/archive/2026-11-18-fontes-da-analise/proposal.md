# Proposal

## Why

A análise com IA sempre envia o mesmo contexto (título, descrição, comentários e retrabalhos), mas em muitas demandas o conteúdo real está nos anexos do Asana (especificação em PDF, prints, planilhas, documentos Word), ou o usuário quer conduzir a análise com instruções próprias e modelos de referência. Também só as dúvidas trazem o trecho que as gerou; os itens do checklist não dizem de onde vieram, e com várias fontes isso fica ainda mais necessário para conferir a análise. O front (`..\ActumUI`, change `fontes-da-analise`) depende desta change.

## What Changes

- `POST /tarefas/{identifier}/analises` passa a aceitar um corpo opcional com as **fontes da análise**:
  - modo `tarefa`: `descricao` (bool), `comentarios` (bool, inclui os retrabalhos) e `anexos` (gids de anexos da tarefa no Asana);
  - modo `prompt`: `prompt` (texto do usuário, no lugar da descrição e dos anexos, que pode citar arquivos como `@nome`), `anexos` (os citados) e `comentarios` (bool).
  - Sem corpo, o comportamento atual é mantido: descrição + comentários.
- Os anexos escolhidos são baixados do Asana na hora e enviados à Claude API: PDF e imagens como conteúdo nativo; arquivos de texto, Word (`.docx`) e Excel (`.xlsx`) como texto extraído pela API.
- Validação das fontes com `400` e `errors` por campo (`Fontes`, `Anexos`, `Prompt`): nenhuma fonte, anexo fora da tarefa, tipo não legível, tamanho acima do limite, prompt vazio ou longo demais.
- Cada versão grava as fontes usadas; `GET /tarefas/{identifier}/analises` devolve `fontes` em cada versão (`null` nas versões antigas).
- **Origem e trecho no checklist**: a IA devolve, para cada item do checklist, a origem e o trecho literal que o gerou, como já faz nas dúvidas. As origens de checklist, dúvidas e regras passam a aceitar também `anexo <nome>` (com a página, quando for PDF) e `prompt`.
- `GET /tarefas/{identifier}/anexos` informa, para cada anexo, `legivelNaAnalise` e o `motivo` quando não é legível.
- O cliente do Asana ganha a leitura do conteúdo de um anexo (bytes), sem enviar o token ao endereço temporário de download.
- Os comentários continuam sendo atualizados do Asana antes de toda análise, mesmo quando não entram no contexto.

## Capabilities

### New Capabilities
<!-- nenhuma -->

### Modified Capabilities
- `task-analysis`: fontes no `POST`, validação, fontes gravadas e devolvidas por versão, origem e trecho nos itens do checklist e origens `anexo`/`prompt`.
- `claude-client`: o contrato do analisador recebe as fontes escolhidas, o prompt e os arquivos; envio de PDF/imagem nativos e de texto extraído; erros de arquivo rejeitado pela Anthropic.
- `task-attachments`: lista de anexos com `legivelNaAnalise` e `motivo`.
- `asana-client`: leitura do conteúdo de um anexo.

## Impact

**Módulos afetados**
- `Actum.Modules.Tarefas`: `AnaliseService` (fontes, validação, download), `AnexoService` (legibilidade), domínio `Analise`/`AnaliseItem` (fontes gravadas), contratos, `AnaliseEndpoints`, configuração do EF e migration.
- `Actum.Modules.Claude`: `DemandaParaAnalise` (fontes, prompt, arquivos), `PromptDaDemanda` (instruções por modo, esquema com `origem`/`trecho` no checklist), `AnalisadorDeDemanda` (blocos de documento e imagem), extração de texto de `.docx`/`.xlsx`, regras de legibilidade e limites.
- `Actum.Modules.Asana`: `IAsanaClient.BaixarAnexoAsync`.
- Sem dependência nova entre módulos: Tarefas já usa os contratos públicos de Claude e de Asana.

**Dependência nova**: `DocumentFormat.OpenXml` (no módulo Claude) para extrair o texto de `.docx` e `.xlsx`.

**Contrato da API (não breaking)**
- `POST /tarefas/{identifier}/analises`: corpo novo e opcional; chamadas sem corpo continuam válidas.
- `AnaliseResponse.fontes` (novo, anulável), `AnaliseItemResponse.origem`/`trecho` passam a vir também no checklist, `AnexoResponse.legivelNaAnalise`/`motivo` (novos): só acréscimos.
- Impacto no front: regenerar `src/api/schema.d.ts`; a change `fontes-da-analise` do ActumUI consome esses campos.

**Banco**: colunas de fontes em `tarefas.analise` e tabela nova `tarefas.analise_fonte_anexo` (ver design.md).

**Base em andamento**: parte do trabalho ainda não commitado (lista e download de anexos, trecho das dúvidas).

**Fora do escopo**
- Arquivos externos (Google Drive, Dropbox), `.doc`/`.xls` antigos, `.pptx`, áudio e vídeo.
- Guardar no Actum cópia dos anexos ou do texto extraído (são baixados a cada análise).
- OCR de PDFs digitalizados além do que a Claude API já lê nativamente.
- Citations nativas da Claude API (incompatíveis com a saída estruturada usada pela análise): o trecho continua copiado pelo modelo.
- Prompts salvos ou modelos reutilizáveis entre tarefas.
- Mudar a regra de marcação herdada entre versões.
