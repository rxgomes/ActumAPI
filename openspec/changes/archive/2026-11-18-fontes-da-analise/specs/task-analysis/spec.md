## ADDED Requirements

### Requirement: Fontes da análise
O `POST /tarefas/{identifier}/analises` SHALL aceitar um corpo opcional com as fontes: `modo` (`tarefa` ou `prompt`), `descricao`, `comentarios`, `anexos` (gids) e `prompt`. Sem corpo, MUST valer descrição e comentários. Empresa, título e projetos MUST sempre fazer parte do contexto; os retrabalhos acompanham os comentários.

#### Scenario: Sem corpo
- **WHEN** um cliente chama `POST /tarefas/{identifier}/analises` sem corpo
- **THEN** o contexto enviado ao analisador MUST ter a descrição, os comentários e os retrabalhos, sem anexos nem prompt, como antes desta mudança

#### Scenario: Só anexos
- **WHEN** o corpo tem `modo=tarefa`, `descricao=false`, `comentarios=false` e `anexos=["123"]`, e `123` é o `especificacao.pdf` da tarefa
- **THEN** o contexto MUST ter empresa, título, projetos e o conteúdo de `especificacao.pdf`, sem descrição, comentários nem retrabalhos

#### Scenario: Descrição e anexos
- **WHEN** o corpo tem `modo=tarefa`, `descricao=true`, `comentarios=false` e dois anexos
- **THEN** o contexto MUST ter a descrição e os dois anexos, na ordem informada

#### Scenario: Prompt com arquivos citados
- **WHEN** o corpo tem `modo=prompt`, `prompt="Siga o padrão de @modelo.pdf"`, `anexos` com o gid de `modelo.pdf` e `comentarios=true`
- **THEN** o contexto MUST ter o prompt, o conteúdo de `modelo.pdf` identificado pelo nome, os comentários e os retrabalhos, e MUST NOT ter a descrição

#### Scenario: Comentários fora do contexto
- **WHEN** `comentarios=false` e a tarefa tem no Asana um comentário que ainda não está no banco
- **THEN** o comentário MUST ser gravado como não lido, como em toda análise, mas MUST NOT ser enviado ao analisador

### Requirement: Validação das fontes
A API SHALL validar as fontes antes de baixar anexos ou chamar a Anthropic, respondendo `400` com `errors` por campo em pt-BR e sem gravar versão.

#### Scenario: Nenhuma fonte
- **WHEN** `modo=tarefa` com `descricao=false`, `comentarios=false` e `anexos` vazio
- **THEN** a resposta MUST ter status `400` com o erro "Escolha ao menos uma fonte para a análise." no campo `Fontes`

#### Scenario: Prompt vazio ou longo demais
- **WHEN** `modo=prompt` com `prompt` vazio, ou com mais de 20.000 caracteres
- **THEN** a resposta MUST ter status `400` com o erro no campo `Prompt`

#### Scenario: Anexo de outra tarefa ou removido
- **WHEN** um gid de `anexos` não está entre os anexos da tarefa no Asana
- **THEN** a resposta MUST ter status `400` com o erro "O anexo não está mais na tarefa no Asana." no campo `Anexos`, sem chamar a Anthropic

#### Scenario: Anexo não legível
- **WHEN** um anexo escolhido é externo, de tipo não suportado ou maior que o limite
- **THEN** a resposta MUST ter status `400` com o mesmo motivo de `legivelNaAnalise=false` da lista de anexos, no campo `Anexos`

#### Scenario: Total dos anexos acima do limite
- **WHEN** a soma dos tamanhos dos anexos escolhidos passa do limite total configurado
- **THEN** a resposta MUST ter status `400` com o erro no campo `Anexos` informando o limite

#### Scenario: Falha ao baixar um anexo
- **WHEN** o Asana falha ao entregar o conteúdo de um anexo escolhido
- **THEN** a resposta MUST seguir o status e a mensagem do erro do cliente do Asana, sem chamar a Anthropic e sem gravar versão

### Requirement: Fontes gravadas na versão
Cada versão SHALL gravar as fontes usadas (modo, descrição, comentários, anexos com gid e nome, na ordem, e o texto do prompt) e as devolver em `fontes`. Versões gravadas antes desta mudança MUST vir com `fontes` nulo.

#### Scenario: Versão com anexos
- **WHEN** a versão 2 foi feita com a descrição e o anexo `layout.pdf`
- **THEN** `GET /tarefas/{identifier}/analises` MUST devolver na versão 2 `fontes` com `modo=tarefa`, `descricao=true`, `comentarios=false` e o anexo `layout.pdf` com o gid

#### Scenario: Versão por prompt
- **WHEN** a versão foi feita no modo `prompt`
- **THEN** `fontes` MUST trazer `modo=prompt`, o texto do prompt e os anexos citados

#### Scenario: Versão antiga
- **WHEN** a versão foi gravada antes das fontes existirem
- **THEN** ela MUST vir com `fontes` nulo

### Requirement: Origem e trecho dos itens do checklist
Cada item do checklist SHALL trazer a origem e o trecho literal do contexto que o gerou, no mesmo formato das dúvidas. As origens de checklist, dúvidas e regras MUST ser `título`, `descrição`, `comentário de <autor> em <dd/MM/yyyy HH:mm>`, `anexo <nome>` (com `, página <n>` em PDF, quando souber) ou `prompt`.

#### Scenario: Item tirado de um anexo
- **WHEN** a análise gera um item do checklist a partir da frase "O relatório deve ser agrupado por filial" na página 3 de `especificacao.pdf`
- **THEN** o item MUST vir com origem `anexo especificacao.pdf, página 3` e o trecho com a frase copiada sem alteração

#### Scenario: Item tirado de uma imagem
- **WHEN** o item vem de um print sem texto copiável
- **THEN** o item MUST vir com origem `anexo <nome da imagem>` e o trecho pode vir nulo

#### Scenario: Checklist de análises antigas
- **WHEN** a versão foi feita antes de o checklist ter origem e trecho
- **THEN** os itens do checklist MUST vir com `origem` e `trecho` nulos

## MODIFIED Requirements

### Requirement: Análise sob demanda
A API SHALL expor `POST /tarefas/{identifier}/analises`, que primeiro busca no Asana o histórico da tarefa e grava os comentários que ainda não estão no banco (como não lidos), depois valida as fontes, baixa os anexos escolhidos, monta o contexto com as fontes (empresa, título e projetos sempre; descrição, comentários em ordem cronológica com os retrabalhos, anexos e prompt conforme o pedido), chama `IAnalisadorDeDemanda` e grava o resultado como nova versão da análise. A análise MUST acontecer apenas por essa chamada, nunca na sincronização.

#### Scenario: Primeira análise
- **WHEN** a tarefa existe, não está arquivada e não tem análise, e um cliente chama `POST /tarefas/{identifier}/analises`
- **THEN** a resposta MUST ter status `201` com a análise `versao=1`, o objetivo, o checklist agrupado por área (grupos e itens em ordem, cada item com o aviso de trecho vago quando houver, a origem e o trecho), os testes, as dúvidas e as regras em ordem (cada regra com a origem), todos não marcados, as fontes usadas, o modelo, os tokens e a data da análise

#### Scenario: Comentário feito no Asana depois da última sincronização
- **WHEN** a tarefa tem no Asana um comentário que ainda não está no banco e um cliente chama `POST /tarefas/{identifier}/analises` com os comentários entre as fontes
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
- **WHEN** o analisador falha (chave, recusa, indisponibilidade, arquivo rejeitado ou resposta incompleta)
- **THEN** nenhuma versão MUST ser gravada e a resposta MUST seguir o status e a mensagem do erro do cliente da Claude API

### Requirement: Consulta das análises
A API SHALL expor `GET /tarefas/{identifier}/analises`, que devolve todas as versões da tarefa, da mais recente para a mais antiga, cada uma com o objetivo, os itens em ordem (checklist, testes, dúvidas e regras, com origem e trecho quando houver), as fontes, se é a atual e os metadados (modelo, tokens, data).

#### Scenario: Tarefa sem análise
- **WHEN** a tarefa existe e nunca foi analisada
- **THEN** a resposta MUST ter status `200` com lista vazia

#### Scenario: Tarefa inexistente na consulta
- **WHEN** não existe tarefa com o `identifier`
- **THEN** a resposta MUST ter status `404`

#### Scenario: Versão anterior às regras
- **WHEN** a versão foi gravada antes da existência da seção de regras
- **THEN** ela MUST ser devolvida com a lista de regras vazia

### Requirement: Regras e observações da demanda
A análise SHALL trazer, além do checklist, dos testes e das dúvidas, a seção de regras: regras de negócio, restrições, padrões a seguir e sugestões tiradas do contexto enviado. Cada regra MUST ter a origem: `descrição`, o comentário (com o autor e a data e hora `dd/MM/yyyy HH:mm`), `anexo <nome>` ou `prompt`.

#### Scenario: Regra combinada num comentário
- **WHEN** um comentário de Maria em `2026-11-04T14:30:00` diz "o desconto não pode passar de 10%"
- **THEN** a análise MUST ter uma regra com esse conteúdo e origem indicando o comentário de Maria em `04/11/2026 14:30`

#### Scenario: Regra tirada de um anexo
- **WHEN** o anexo `padroes.docx` diz "valores sempre com duas casas decimais"
- **THEN** a análise MUST ter uma regra com esse conteúdo e origem `anexo padroes.docx`

#### Scenario: Pedido de ajuste num comentário
- **WHEN** um comentário pede uma ação a fazer, por exemplo "incluir também a filial 2"
- **THEN** o pedido MUST aparecer como item do checklist, e não só como regra

#### Scenario: Sem regras no contexto
- **WHEN** o contexto enviado não traz regra, restrição nem sugestão
- **THEN** a lista de regras MUST vir vazia

#### Scenario: Marcação herdada de regra
- **WHEN** a nova versão tem uma regra com o mesmo texto de uma regra marcada na versão anterior
- **THEN** a regra da nova versão MUST entrar marcada, com a data da marcação original

### Requirement: Trecho que gerou cada dúvida
Cada dúvida para o solicitante SHALL trazer a origem (`título`, `descrição`, o comentário com o autor e a data e hora `dd/MM/yyyy HH:mm`, `anexo <nome>` ou `prompt`) e o trecho literal do contexto que gerou a pergunta, para que quem pediu reconheça de onde ela saiu. A IA MUST NOT perguntar o que o contexto enviado já responde, diretamente ou por um princípio geral (por exemplo, uma justificativa com "pois"); nesse caso, o princípio MUST virar regra.

#### Scenario: Dúvida sobre a descrição
- **WHEN** a análise gera uma dúvida a partir de uma frase da descrição
- **THEN** a dúvida MUST vir com origem `descrição` e o trecho com a frase copiada sem alteração

#### Scenario: Dúvida sobre um anexo
- **WHEN** a análise gera uma dúvida a partir de uma frase de `especificacao.pdf`
- **THEN** a dúvida MUST vir com origem `anexo especificacao.pdf` (com a página, quando souber) e o trecho com a frase copiada sem alteração

#### Scenario: Dúvidas de análises antigas
- **WHEN** uma versão da análise foi feita antes do trecho existir
- **THEN** as dúvidas dessa versão MUST vir com `origem` e `trecho` nulos
