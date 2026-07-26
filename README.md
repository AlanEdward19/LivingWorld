# Living World

Simulador persistente de sociedades, populações e indivíduos. O mundo avança sem jogador:
famílias, economia, nascimentos, mortes e habilidades são resolvidos por um motor
determinístico. A LLM entra depois só para interpretar e conversar — nunca como fonte
da simulação.

**Objetivo técnico imediato:** 100 NPCs numa vila medieval por 100 anos, sem LLM, com
estado reprodutível (mesma seed + mesmos ticks = mesmo mundo).

## Stack

| Camada | Tecnologia |
|--------|------------|
| Motor e API | .NET 10 / C# |
| Persistência | SQLite (caminho para Postgres) |
| Cliente web | React + TypeScript (fase posterior) |
| Arquitetura | Monólito modular por camadas |

## Pré-requisitos

- [.NET SDK 10](https://dotnet.microsoft.com/download)
- Git Bash (Windows) ou shell compatível com `bash` para os scripts em `scripts/`

## Desenvolvimento

```bash
bash scripts/build.sh          # build Release, warnings como erro
bash scripts/test.sh           # testes (cenários longos ficam de fora)
bash scripts/lint.sh --fix     # formatação
bash scripts/verify.sh         # gate completo: docs + build + lint + test
```

Cenários longos (ex.: 100 anos):

```bash
bash scripts/test.sh --filter Category=Scenario
```

Cenários de população ficam em `scenarios/` (ex.: `default.json`, `test-scifi.json`).

## Estrutura do repositório

```
src/
  LivingWorld.Domain/          # entidades e regras de domínio
  LivingWorld.Simulation/      # tick, sistemas, RNG semeado
  LivingWorld.Infrastructure/  # persistência e integrações
  LivingWorld.Api/             # HTTP (fases posteriores)
  LivingWorld.AI/              # contrato LLM (fases posteriores)
  LivingWorld.Workers/         # workers de simulação
tests/LivingWorld.Tests/
docs/                          # domínio, ADRs, roadmap por fase
rules/                         # regras operacionais para agentes de código
```

## Documentação

- [ROADMAP.md](ROADMAP.md) — fases, trilhas e critérios de aceite
- [docs/README.md](docs/README.md) — índice do domínio (tempo, NPC, economia, mapa, etc.)
- [docs/adr/](docs/adr/) — decisões de arquitetura
- [AGENTS.md](AGENTS.md) — roteador para quem usa IA no repositório

## Licença

A definir.
