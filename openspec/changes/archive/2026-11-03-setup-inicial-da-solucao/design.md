# Design

## Context

O repositório contém apenas `CLAUDE.md`, `docs/` e `openspec/`. As decisões de fundação já foram tomadas e justificadas nas changes arquivadas do Clavus, em `E:\Ixact\Clavus\ClavusAPI\openspec\changes\archive\`: `setup-inicial-da-solucao`, `cors-e-problem-details` e a da entidade base. Este design registra apenas o que muda ao portá-las para o Actum. Comportamento esperado em `specs/`.

## Goals / Non-Goals

**Goals:**
- Ter no Actum a mesma estrutura, convenções e garantias testadas do Clavus.
- Fazer a cópia de forma mecânica e verificável (mesmos testes, novo namespace).

**Non-Goals:**
- Evoluir ou alterar a fundação. Melhorias entram em changes próprias, nos dois projetos.
- Extrair um pacote compartilhado entre os projetos.

## Decisions

### 1. Copiar em vez de referenciar

Os projetos `Clavus.Shared.*` são copiados para `src/Shared/Actum.Shared.*`, com renomeação de namespace e de nomes (`AddClavusDatabase` → `AddActumDatabase`, `ConnectionStrings:Clavus` → `ConnectionStrings:Actum`).

- **Alternativa considerada:** referenciar os projetos do Clavus por caminho relativo ou publicar um pacote NuGet local. Foi descartada porque acopla o ciclo de vida dos dois repositórios, e no Actum a fundação deve ficar estável enquanto o Clavus evolui.

### 2. Layout

```
Actum.slnx
global.json
Directory.Build.props
Directory.Packages.props
.editorconfig
docker-compose.yml
scripts/criar-usuario-dev.sql
src/
  Actum.Api/
  Shared/
    Actum.Shared.Abstractions/
    Actum.Shared.Infrastructure/
  Modules/                         # vazio nesta change (.gitkeep)
tests/
  Actum.Api.Tests/
  Actum.Shared.Infrastructure.Tests/
```

### 3. Banco local

- Banco e usuário `actum`/`actum` no PostgreSQL local (o mesmo servidor 18 usado pelo Clavus, porta 5432).
- Variável de testes `ACTUM_TEST_CONNECTION_STRING` e bancos temporários com prefixo `actum_test_`.

### 4. CORS

`Cors:AllowedOrigins` em `Development` com `http://localhost:5173` (porta padrão do Vite no ActumUI).

## Risks / Trade-offs

- **[Correções feitas no Clavus não chegam ao Actum]** → Aceito. Ao corrigir algo na fundação de um projeto, avaliar replicar no outro.
- **[Conflito de porta se Clavus e Actum rodarem juntos]** → Definir em `launchSettings.json` portas diferentes das do Clavus (ex.: API em `5180`).

## Migration Plan

Não há sistema em uso. Implantação: merge na `main`. Rollback: reverter o commit.
