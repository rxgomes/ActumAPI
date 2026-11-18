## MODIFIED Requirements

### Requirement: Lista de anexos
A API SHALL expor `GET /tarefas/{identifier}/anexos`, que lê do Asana os anexos da tarefa e devolve, do mais antigo para o mais recente, `gid`, `nome`, `criadoEm` (horário local), `tamanho` (bytes, nulo quando o Asana não sabe), `hospedagem` (`asana`, `gdrive`, `dropbox`...), `legivelNaAnalise` (se pode ser fonte da análise com IA) e `motivo` (por que não pode, nulo quando pode).

#### Scenario: Tarefa com anexos
- **WHEN** a tarefa tem no Asana os anexos `print.png` e `layout.pdf`
- **THEN** a resposta MUST ter status `200` com os dois anexos, do mais antigo para o mais recente

#### Scenario: Tarefa sem anexos
- **WHEN** a tarefa não tem anexos no Asana
- **THEN** a resposta MUST ter status `200` com a lista vazia

#### Scenario: Tarefa excluída no Asana
- **WHEN** a tarefa não existe mais no Asana
- **THEN** a resposta MUST ter status `400` com a mensagem "A tarefa não existe mais no Asana."

#### Scenario: Legibilidade na análise
- **WHEN** a tarefa tem `layout.pdf` no Asana e `video.mp4` e um arquivo do Google Drive
- **THEN** `layout.pdf` MUST vir com `legivelNaAnalise=true` e `motivo` nulo, e os outros dois com `legivelNaAnalise=false` e o motivo dado pelo módulo Claude
