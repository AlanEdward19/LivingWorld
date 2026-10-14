# STATE.md — memória entre sessões e handoff entre áreas

Fonte única de continuidade. Quem entra numa área nova **lê este arquivo primeiro**.
Não duplique conteúdo de `ROADMAP.md`, `AGENTS.md` ou `docs/` aqui — aponte.

## Handoff
- **Fechado (2026-10-14)**: ADs 104–114 e as issues #20–#43 (incl. #25–#28, #40, #41): cliente Godot com arte/áudio,
  história e narrativa fechadas (Fases 10/12), primeira fatia de emergência (AD-110), encarnação de jogador (AD-111),
  console de admin com pausa de teto (AD-112), poço (AD-113), custo medido sem regressão (AD-114).
  Fase 28 fechada no motor (API e LOD); falta só o painel visual (#58). Cliente web removido (ADR-0022).
- **Abertas**: #57 roadmap/STATE em sincronia (esta entrega) · #58 painel COG-11 no Godot · #59 telas Godot para backend
  pronto · #60 provider de LLM de produção + custo (ADR-0016) · #61 Fase 24 resto · #62 Fase 26 resto (reescrita exige
  Fase 18) · #63 Fase 25 resto · #64 Fase 16 efeitos/custos em switch fechado · #65 FAM-32 (AD-088) · #66 ordem das fases
  14/15/17–23/27 · #67 verificação de `lodging-and-rent`/`godot-main-menu` · #68 interações presenciais ·
  #46 criador de mundo · #47 criador procedural de objetos.
- **Próxima unidade**: aguardando decisão #66 (ordem das fases `spec`). Até lá, só #58–#65, #67, #68.
- **Pendência de CI**: goldens do cliente foram regravados num Mac arm64; rodar o CI antes do merge.
- **Integração do extraordinário (2026-08-12)**: decisão **mista**, sem fase numerada nova para "super-heróis" —
  distribuída entre Fases 16, 17, 23, 24, 25, com toques em 10, 12, 22. ADR-0010 + `docs/domain/powers.md`.
- **Decisão de escopo (2026-07-29)**: Fase 14 (Unreal) adiada; o cliente é Godot (ADR-0022). Formalização por ADR: #66.
- **Dívida Fase 9**: PERF-12 hasher; `LongRunScaleTests` só nightly.
- **Gate**: `bash scripts/verify.sh`. No Windows, Git bash. Sem `Category=Scenario` no rotina.
- **População default ~40/100** após calibração econômica (Fase 5) — coerência, não "interessante".
- Fases 0–13 e 28 (motor) fechadas; 24–26 parciais; 14, 17–23, 27 sem implementação. Ver `ROADMAP.md`.
- **Fase 13**: períodos = startpoints dinâmicos; autoria de catálogo sem runtime de IA interno.
- **Eval**: `verify.sh`; `verify-mutation.sh` manual. Harness: [`HARNESS.md`](HARNESS.md).
- **Budget**: nenhum loop autônomo ativo.

## Decisões (AD-NNN)
Log completo em [docs/decisions-log.md](docs/decisions-log.md) — AD-001..048 (não duplique
aqui; a Fase 5 registrou AD-039..048, ver lá).

## Fases
Status por fase vive na tabela do [ROADMAP.md](ROADMAP.md). Não replique aqui.

## Riscos — mitigação aplicada
Cada linha aponta o mecanismo **estrutural** que impede o risco, não o teste que o detecta
depois. Onde o mecanismo já virou task, a fase está nomeada.

| Risco | Mecanismo que o impede | Fase |
|---|---|---|
| Determinismo quebrar silenciosamente | `BannedApiAnalyzers` torna `Random`/`DateTime.Now` **erro de compilação**; determinismo verificado em **dois processos** (pega ordem de `Dictionary`); golden hashes versionados tornam a quebra visível no diff | 0, 1 |
| Hash não cobrir o estado que ele deveria proteger | Hash partido em canônico/volátil + teste **gerado por reflexão**: campo não classificado reprova | 1 |
| Tick não terminar (evento que se re-agenda, decisão que não converge) | Teto declarado de iterações por tick, com abort nomeando o sistema culpado | 1, 4 |
| Genética virar destino | Peso `w_gene` é parâmetro declarado do cenário e auditável; controle de **deriva neutra** substitui limiar mágico; teste contrafactual de household rico vs pobre | 7 |
| Explosão de custo de memória e log | Log em dois tiers desde a Fase 3 + **cânone limitado por comunidade**, que torna o custo independente do tempo (ADR-0007). Sensor de bytes/NPC/ano na Fase 3, seis fases antes de doer; teto por NPC **vivo** (não por NPC já existido) e snapshot delta/binário na Fase 9 | 3, 9, 10 |
| SQLite travar escrita concorrente | Interceptor exige **zero round-trips durante o tick** — se o banco só é tocado no snapshot, trocar de provider é trocar string de conexão | 3 |
| Cliente web atrasar o objetivo #1 | Reordenado: Fase 2 é geografia-dados, mapa visual virou Fase 15 (AD-007) | 2, 15 |
| Conteúdo medieval calcificar em `src/` | Cenário `test-scifi` rodando no gate **desde a Fase 3** — se um cenário alienígena roda cedo, o medieval não enraíza. Elimina a dívida que a Fase 13 carregava | 3 |
| Estado órfão entre subsistemas | Sweep de integridade referencial genérico, dirigido por reflexão sobre todos os tipos de ID — ganha cobertura sozinho a cada fase nova | 3 |
| Verdade histórica vazar para o mundo do jogo | Consultas separadas (verdade vs crença) com teste de mutação: desligar a checagem tem de reprovar o critério | 10, 11 |
| Admin distraído congela todos | Pausa de admin com teto (`maxTicks`) e retomada automática, auditada append-only (AD-112, ADR-0015) | 26 |

## Riscos ainda sem mecanismo
| Risco | Por que segue aberto |
|---|---|
| Custo e latência de LLM em escala | Só dimensionável com provider real; decisão em ADR-0016; sem provider de produção (#60) |
| Balanceamento do mundo (economia estável, demografia plausível) | Nenhum gate prova que o mundo é *interessante*, só que é *coerente*. Exige julgamento humano |
| **Escopo extra canibalizar o caminho crítico** | Mitigado por AD-010 (fases 16–19 bloqueadas até a 8), mas o risco é de disciplina, não de mecanismo. Nenhum gate impede alguém de começar a Fase 18 antes da hora |
| Ramificação pura não entregar payoff narrativo | Mudar o passado não afeta a linha de origem (ADR-0008). O valor tem de vir de conhecimento atravessando linhas, perda permanente e comparação — nada disso está provado ainda |
| Potência genérica não caber em alguma fantasia | ADR-0010 aposta num modelo só. Se uma categoria não couber, a saída é esticar o modelo, não forkar — e isso pode ficar feio |
| Catch-up longo estourar a paciência do jogador | ADR-0012 dá o mecanismo, não o orçamento. Voltar a um branch parado há 40 anos pode custar minutos. Pré-aquecimento é hipótese, não solução provada |
| Emergência aberta produzir coisa implausível | ADR-0013 garante que o emergente é determinístico e composto, não que é *bom*. Nenhum gate distingue tecnologia plausível de monstro estatístico |
| 28 fases | O escopo agora é muito maior que o objetivo #1. As trilhas (AD-012) organizam, mas não reduzem. Risco real é diluir esforço antes de fechar o núcleo |
| Fidelidade perdida é perdida | Aceitar `LOD` como definição do mundo (ADR-0012) significa que um período que ninguém observou nunca pode ser ampliado. Coerente e irreversível — se incomodar depois, o custo é alto |
