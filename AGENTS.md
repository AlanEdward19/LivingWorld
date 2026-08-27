# AGENTS.md — Living World

> Índice/roteador para agentes de código. **Não é manual.** Regra detalhada mora em
> `rules/` e é carregada sob demanda. `docs/` é para humanos — referencie, não copie.

## BOUNDARIES (prioridade máxima)

**SEMPRE FAÇA**
- **PROGRESSIVE LOADING — NÃO LEIA TUDO.** Carregue só os arquivos que a Quick reference
  aponta para o pedido atual. Não abra o repo nem a documentação inteiros.
- Todo doc/rule/spec: 30–70 linhas (ideal), **≤100 (teto)**. Passou → quebre em `.md` + índice.
- Rode tarefa repetível por `bash scripts/<x>.sh`. Nunca monte `dotnet test` na mão.
- Antes de dizer "pronto": `bash scripts/verify.sh` deve sair 0.
- Toda aleatoriedade vem de RNG semeado do mundo. Ver `rules/simulation-determinism.md`.

**PERGUNTE QUANDO**
- For apagar/renomear spec, design ou doc; alterar migração aplicada; mudar contrato público.
- A tarefa exigir dependência nova ou decisão de arquitetura → registre ADR em `docs/adr/`.
- Uma fase do `ROADMAP.md` mudar de escopo.

**NUNCA FAÇA**
- Ler tudo de uma vez; gerar doc/rule/spec >100 linhas.
- Deixar a LLM escrever no estado do mundo (ver `rules/llm-boundary.md`).
- Usar `Random` sem seed, `DateTime.Now` ou GUID aleatório dentro de `LivingWorld.Simulation`.
- Editar migração já aplicada, código gerado, ou segredos (`.env`, chaves).

## Projeto
- **Propósito**: simulador persistente de sociedades, populações e indivíduos. O mundo
  avança sem jogador. O motor determinístico é a **fonte da verdade**; a LLM só interpreta.
- **Alvo**: API .NET + workers de simulação + cliente web React (mapa 2D). Unreal depois.
- **Stack**: .NET 10 / C# · SQLite (→ Postgres) · React+TS · Arquitetura: monólito modular por camadas.
- **Objetivo técnico #1**: 100 NPCs numa vila medieval por 100 anos, sem LLM, com famílias,
  profissões, economia, nascimentos, mortes e evolução de habilidades preservados.

## Comandos → use os scripts
| Preciso… | Rode |
|---|---|
| Testar | `bash scripts/test.sh [--watch] [--filter <padrão>]` |
| Lint/format | `bash scripts/lint.sh [--fix]` |
| Build | `bash scripts/build.sh` |
| Gate final | `bash scripts/verify.sh` |

## Quick reference — carregue SÓ a linha que casa com o pedido
| Tarefa | Carregue | Skill |
|---|---|---|
| Nova feature / fluxo | `rules/implementation.md` | `tlc-spec-driven` |
| Escrever/ajustar testes | `rules/tests.md` | `harness-engineering` |
| Escrever/revisar critério de fase | `rules/eval-criteria.md` | — |
| Sistema de simulação / tick / RNG | `rules/simulation-determinism.md` | — |
| Novo sistema / integração / “mundo vivo” | `rules/living-world-cohesion.md` | — |
| Qualquer coisa com LLM | `rules/llm-boundary.md` | — |
| Banco / entidade / migração | `rules/database-entities.md` | — |
| "O que vem depois?" | `ROADMAP.md` (índice) → `docs/roadmap/phase-NN-*.md` | `agentic-delivery` |
| Entender um subsistema | `docs/README.md` (índice) → `docs/domain/*.md` | — |
| Por que decidimos X? | `docs/adr/` | — |
| Rodar autônomo até passar | — | `loop-engineering` |
| Doc atual de lib/framework | **context7 MCP** | — |

> **context7 MCP**: para API/versão atual de uma lib (EF Core, xUnit, React), consulte o
> context7 em vez da memória; cite a versão no pedido para casar a doc certa.

## Regras gerais (detalhe em `rules/`)
- **Implementação**: unidades pequenas, 1 comportamento por tarefa. Sem one-shot hero.
- **Testes**: comportamento novo tem teste. O agente não se autoavalia — quem decide é o gate.
- **Simulação**: determinística e reprodutível. Mesma seed + mesmos ticks = mesmo mundo.
- **Coesão**: todo sistema novo entra **conectado** (consumidor causal real) — ver
  `rules/living-world-cohesion.md`. Stub presentation-only sem consumidor é dívida explícita,
  não entrega.
- **LLM**: propõe linguagem e intenção; o motor valida e aplica. Nunca o contrário.

## Harness
- Manifesto: `HARNESS.md` (ainda não existe — gerar via `harness-engineering`).
- Eval gates (0/1): `scripts/verify.sh`. Memória/continuidade entre sessões: `STATE.md`.

## Política de commit e limpeza
- **Commit automático ao fim de cada fase**, e só quando `scripts/verify.sh` sair 0.
  Mensagem referencia a fase (`feat(phase-03): ...`). Fora disso, não commite.
- **Limpeza após aprovação**: remova efêmeros (`tasks.md`, logs, contratos). Preserve
  `AGENTS.md`, `rules/`, `docs/`, `ROADMAP.md`, `STATE.md`, `spec.md`, `design.md`.
- **Loop**: não limpe a cada tarefa; só quando o loop inteiro terminar e for aprovado.

## Mapa de arquivos
| Caminho | Papel | Público | Ciclo |
|---|---|---|---|
| `AGENTS.md` | Índice/roteador | IA | permanente |
| `rules/*.md` | Regras testáveis (sob demanda) | IA | permanente |
| `scripts/*.sh` | Comandos repetíveis | IA | permanente |
| `docs/**`, `docs/adr/*` | Contexto e decisões | Humano | permanente |
| `ROADMAP.md`, `docs/roadmap/*` | Fases e critérios de aceite | IA+Humano | permanente |
| `STATE.md` | Handoff entre sessões/áreas | IA | permanente |
| `tasks.md`, logs, contratos | Trabalho de 1 tarefa | IA | efêmero |
