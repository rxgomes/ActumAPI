# Proposal

## Why

Os comentários do Asana costumam trazer solicitações, regras de negócio, restrições e sugestões que não estão na descrição da tarefa. Hoje a análise com IA já recebe os comentários gravados no banco, mas há três falhas:

- Comentários feitos no Asana depois da última sincronização não chegam à IA. A sincronização só relê o histórico quando a tarefa aparece como modificada.
- As instruções mandam ignorar "conversas que não alteram o que deve ser feito", e com isso regras e sugestões são descartadas.
- O resultado não tem onde guardar uma regra ou observação que não vira ação do checklist.

## What Changes

- **Comentários atualizados:** antes de chamar a IA, `POST /tarefas/{identifier}/analises` busca no Asana o histórico da tarefa e grava os comentários novos, como não lidos, como já faz a sincronização. Se o Asana falhar, a análise não é feita (`502`) e nada é gravado.
- **Instruções da IA:** todo comentário passa a ser considerado. Pedidos viram itens do checklist, dados de referência viram testes, e regras, restrições e sugestões viram a nova seção de regras. Continuam ignoradas só mensagens automáticas e agradecimentos ou avisos sem conteúdo.
- **Seção "Regras e observações":** a análise passa a ter itens do tipo `regra`, com a **origem** de cada um (a descrição ou o comentário, com autor e data). As regras podem ser marcadas como atendidas e herdam a marcação da versão anterior, como os outros itens.
- **Contrato:** `AnaliseResponse` ganha `regras`, `AnaliseItemResponse` ganha `origem` e `TipoItemAnalise` ganha o valor `regra` (não breaking).
- **Banco:** nova coluna anulável `origem` em `tarefas.analise_item`.

## Capabilities

### New Capabilities

Nenhuma.

### Modified Capabilities

- `task-analysis`: a análise sob demanda passa a atualizar os comentários pelo Asana antes de chamar a IA e a devolver a seção de regras com origem. A consulta e a marcação dos itens passam a incluir as regras.

## Impact

- **Módulos afetados:**
  - `Actum.Modules.Claude`: prompt, JSON Schema da resposta, `DemandaEstruturada` e um novo `RegraDaDemanda`.
  - `Actum.Modules.Tarefas`: `AnaliseService`, `Analise`, `AnaliseItem`, `TipoItemAnalise`, contratos e migration.
  - `Actum.Modules.Asana`: sem mudança. O módulo `Tarefas` já depende do contrato `IAsanaClient`.
- **Contrato da API:** campos e valor de enum novos, sem quebrar o que existe. `POST /tarefas/{identifier}/analises` passa a poder responder `502` quando o Asana falha (o endpoint já declara `502`). Impacto no front (`..\ActumUI`): regenerar os tipos, exibir a seção "Regras e observações" com a origem de cada item e tratar o `502` como os outros erros da análise. Isso fica numa change própria no ActumUI.
- **Custo:** a análise fica um pouco mais lenta (uma chamada ao Asana antes da IA) e a resposta da IA fica um pouco maior.

### Fora do escopo

- Atualizar retrabalhos, status ou outros campos da tarefa durante a análise. Isso continua sendo papel da sincronização.
- Enviar as anotações privadas do usuário para a IA.
- Ler anexos ou imagens dos comentários.
- Mudar a sincronização para reler comentários de tarefas não modificadas.
