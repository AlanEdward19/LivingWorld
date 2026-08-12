# docs/adr/ — Architecture Decision Records (para HUMANOS)

Uma decisão por arquivo: `ADR-NNNN-<slug>.md`. ADR é **imutável** — para reverter, crie
um novo com status "substitui ADR-NNNN". O `AGENTS.md` só aponta para cá; não copie
conteúdo de ADR para dentro dele.

**Gatilho**: dependência nova ou decisão de arquitetura. Use `ADR.template.md`.

| ADR | Decisão | Status |
|---|---|---|
| [0001](ADR-0001-monolito-modular-dotnet.md) | Monólito modular em .NET 10, uma solution, camadas por projeto | aceito |
| [0002](ADR-0002-sqlite-agora-postgres-depois.md) | SQLite agora, Postgres depois — sem recurso exclusivo de SQLite | aceito |
| [0003](ADR-0003-cliente-web-react-ts.md) | Cliente web em React + TypeScript, separado da API | aceito |
| [0004](ADR-0004-abstracao-de-provider-llm.md) | Interface `ILlmProvider` agora, provider concreto na Fase 11 | aceito |
| [0005](ADR-0005-simulacao-deterministica-semeada.md) | Simulação determinística com RNG semeado por stream | aceito |
| [0006](ADR-0006-snapshot-mais-event-log.md) | Persistência por snapshot periódico + event log append-only | aceito |
| [0007](ADR-0007-memoria-historica-degradavel.md) | História como relato degradável (especializa a retenção do 0006) | aceito |
| [0008](ADR-0008-ramificacao-como-modelo-temporal.md) | Ramificação como único modelo de viagem no tempo — sem paradoxo, sem fusão | aceito |
| [0009](ADR-0009-branchid-no-esquema-desde-a-fase-3.md) | `BranchId` no esquema e no hash desde a Fase 3 | aceito |
| [0010](ADR-0010-potencia-como-modificador-unificado.md) | Potência (mutante, deus, mago, alien) como modificador unificado | aceito |
| [0011](ADR-0011-primitivo-unico-de-resolucao.md) | Um primitivo de resolução (o "d20"), com variância declarada por domínio | aceito |
| [0012](ADR-0012-catchup-preguicoso-de-branch.md) | Catch-up preguiçoso de branch dormente — determinismo compra preguiça | aceito |
| [0013](ADR-0013-emergencia-aberta-motor-estrutura-llm-nome.md) | Emergência aberta: o motor cria a estrutura, a LLM só nomeia | aceito |
| [0014](ADR-0014-canonico-vs-volatil.md) | Canônico se alimenta uma decisão; volátil se é recomputável ou cosmético | aceito |
| [0015](ADR-0015-pausa-global-e-auditoria-de-admin.md) | Pausa global de admin; auditoria de leitura separada da intervenção | aceito |
| [0016](ADR-0016-identidade-publica-do-mundo.md) | `WorldId` como hash puro da seed (nunca persistido); nome do mundo como campo volátil novo | aceito |
