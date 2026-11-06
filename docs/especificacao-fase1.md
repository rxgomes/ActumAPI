# Actum — Visão geral da Fase 1 (MVP)

> Data: 2026-11-03
> Autor: Ricardo Gomes
>
> Este documento resume o produto e as decisões. Os requisitos detalhados, o design e as tarefas de implementação ficam nas changes do OpenSpec em `openspec/changes/`.

## 1. Objetivo

O Actum é um organizador pessoal das demandas recebidas pelo Asana. O gerenciamento (prazo interno, status, retrabalho, branches) fica **localmente**, em PostgreSQL, sem depender do Asana. O Asana é usado como:

- **entrada**: tarefas abertas atribuídas ao usuário nas duas empresas (workspaces Aureum-ERP e aremas.com.br, com o mesmo login);
- **saída**: ao concluir, adicionar as tags de branch e marcar a tarefa como concluída.

| Problema | Solução |
|---|---|
| Tarefas reabertas ficam "atrasadas" com o prazo antigo | Detecção de reabertura na sincronização e prazo interno próprio |
| Dificuldade de priorizar | Painel por prazo interno, filtrável por empresa |
| Tarefas de duas empresas misturadas | Cada tarefa vinculada à sua empresa |
| Tags de branch inseridas à mão | Branches informadas ao iniciar, enviadas como tags só na conclusão |

## 2. Changes do OpenSpec (ordem de implementação)

| # | Change | O que entrega |
|---|---|---|
| 1 | `setup-inicial-da-solucao` | Fundação copiada do Clavus: solução, Shared, host, health, OpenAPI, Problem Details, CORS, entidade base |
| 2 | `integracao-asana` | Módulo `Asana`: `IAsanaClient`, token, paginação, `429`, tradução de erros, `GET /asana/conexao` |
| 3 | `sincronizacao-de-tarefas` | Módulo `Tarefas`: empresas, tarefas, comentários, carga inicial, arquivamento, detecção de retrabalho |
| 4 | `ciclo-de-vida-da-tarefa` | Iniciar, bloquear, prazo interno, branches, conclusão com tags |
| 5 | `painel-de-tarefas` | Painel agrupado, listagem, detalhe da tarefa |

Para implementar: `/opsx:apply <change>`; ao terminar e revisar: `/opsx:archive <change>`.

## 3. Decisões

| Tema | Decisão |
|---|---|
| Gerenciamento | Local (PostgreSQL); prazo e status internos nunca vão para o Asana |
| Sincronização | Sob demanda (botão), todas as empresas de uma vez |
| Escopo | Só tarefas atribuídas ao usuário |
| Carga inicial | Na primeira sincronização de cada empresa, as tarefas abertas entram `em_andamento` com prazo interno = prazo do Asana (aparecem em Próximas/Atrasadas) |
| Tarefas novas | Depois da carga inicial, entram em `entrada` |
| Concluída antes do Actum e reaberta | Entra em `entrada`, sem histórico |
| Retrabalho | Detectado na sincronização; prazo interno fica pendente até o usuário definir o novo |
| Branches | Informadas pelo usuário ao iniciar, com as partes (front e/ou back); a branch de implementação é criada no GitHub a partir da base, nos repositórios das partes. Tag = `(Front, Back) => nome` (só o nome nas branches registradas antes das partes) |
| Conclusão | Adiciona só as tags ainda não enviadas, depois conclui no Asana; a nova tentativa após falha continua de onde parou |
| Retrabalho com branch nova | A nova tag é adicionada e as antigas são mantidas |
| Stack | .NET 10, Minimal API, monolito modular, EF Core + PostgreSQL local; Vue 3 + Quasar |
| Repositórios | `E:\Ixact\Actum\ActumAPI` e `E:\Ixact\Actum\ActumUI` |
| Convenções | As mesmas do Clavus (`CLAUDE.md`, `openspec/config.yaml`) |
| Token do Asana | `dotnet user-secrets` (`Asana:Token`), nunca versionado |
| Token do GitHub | `dotnet user-secrets` (`GitHub:Token`, Contents: read and write), nunca versionado |

## 4. Status da tarefa

```
entrada ──iniciar──▶ em_andamento ◀──▶ bloqueada
                         │
                      concluir (tags + conclusão no Asana)
                         ▼
                     concluida ──reaberta no Asana──▶ retrabalho
                         ▲                               │
                         └─────────── concluir ──────────┘

arquivada: saiu da lista do usuário no Asana (concluída por terceiros, reatribuída ou excluída)
```

## 5. Front (ActumUI): referência de layout

Mesmo estilo do ERP Aureum:

- menu lateral à esquerda com grupos recolhíveis;
- cabeçalho azul-escuro com o nome da aplicação;
- título da página com a data abaixo;
- cartões brancos com borda arredondada, ícone em quadrado de fundo claro e contadores com cor por categoria (ex.: Retrabalho em laranja, Atrasadas em vermelho, Hoje em azul).

Telas da Fase 1: **Painel**, **Detalhe da tarefa** e os diálogos **Iniciar**, **Novo prazo**, **Nova branch** e **Concluir** (com a lista das tags que serão adicionadas). As specs do front serão escritas no repositório ActumUI.

## 6. Próximas fases

| Fase | Item |
|---|---|
| 2 | Estruturação da demanda com IA (Claude API): objetivo, checklist, dúvidas |
| 3 | Enviar comentários, anexos e prints (Ctrl+V) para o Asana |
| 4 | Dashboard com indicadores de retrabalho por empresa, anotações privadas |
