# STATE.md — memória entre sessões e handoff entre áreas

Fonte única de continuidade. Quem entra numa área nova **lê este arquivo primeiro**.
Não duplique conteúdo de `ROADMAP.md`, `AGENTS.md` ou `docs/` aqui — aponte.

## Handoff
- **#91 (2026-10-16, `codex/issue-91`)**: história ligada nos mundos do catálogo, gravação síncrona de fatos,
  relato de fome consumido pela utility de Buy e cache hop-0 dentro do teto de custo. ADR-0037;
  spec/evidências em `.specs/features/issue-91-catalog-history/`. Gates e Verifier PASS; relatório `validation.md`, 3 mutações detectadas e custo 99,36% do controle.
- **Fechado (2026-10-14)**: ADs 104–114 e as issues #20–#43 (incl. #25–#28, #40, #41): cliente Godot com arte/áudio,
  história e narrativa fechadas (Fases 10/12), primeira fatia de emergência (AD-110), encarnação de jogador (AD-111),
  console de admin com pausa de teto (AD-112), poço (AD-113), custo medido sem regressão (AD-114).
  Fase 28 fechada (motor e painel visual). Cliente web removido (ADR-0022).
- **Abertas**: #60 provider de LLM de produção + custo (ADR-0016) · #61 Fase 24 resto · #62 Fase 26 resto (reescrita exige Fase 18) ·
  #63 Fase 25 resto · #64 Fase 16 efeitos/custos em switch fechado ·
  #68 interações presenciais · #46 criador de mundo · #47 criador procedural de objetos. Fechadas por este PR: #57, #66, #70–#72, #93.
- **Decisão de escopo (2026-10-14, #66)**: ordem das fases `spec` = **21 → 22 → 23**, depois 18, 27 e 17/19/20.
  Fase 14 substituída pelo Godot (ADR-0025, voz/Unreal adiados); Fase 15 fechada.
- **Próxima unidade**: Fase 22 (imperfeição; criar plano lean e issues por fatia). Fase 21 entregue com as dívidas (21a–c e #93, AD-115..118, ADR-0028..0031); plano `.specs/features/phase-21-ontogeny/plan.md`.
- **Concluído (2026-10-15)**: #58 (painel Cérebro, escopo de observação, sandbox "E se…?", ADR-0026) e #59 (conversa, narrativas, crenças, jogador, console admin, economia, tecnologias, materializar), cada fatia com Verifier PASS em `.specs/features/godot-*/verification.md`. Branches empilhadas até `feat/godot-materialize`; falta abrir os PRs. Dívidas: pausa pública com ator/motivo e período do mundo continuado pedem rota nova (issue própria); retomar sessão de jogador após reiniciar o cliente.
- **#46 criador de mundo (branch `feat/world-creator`, 2026-10-15)**: fatias 1 (nomes das profissões), 2 (aparência herdada opt-in + habilidades iniciais, ADR-0032) e 4 (criar família e local de trabalho com o mundo rodando, ADR-0033) feitas, planos em `.specs/features/profession-names/`, `world-creator-appearance/` e `world-creator-live/`. Faltam 3 (modelos, depende de #67), 5 (plantas, depende de #47) e 6 (poderes, depende de #64). Profissões/habilidades próprias e emergentes: #96.
- **#47 objetos (branch `feat/objetos-catalogo-fatia1`, 2026-10-15)**: fatia 1 (formato `Objects`/`objectsFormat` + catálogo com fallback, mobília migrada, ADR-0034) feita; plano em `.specs/features/object-catalog/`. Faltam 2 (uso/animação), 3 (efeitos pelo motor), 4 (comida), 5 (invenções, depende da #61), 6 (mods).
- **CI após #47**: `develop` verde no run `38885386561` (gate e cliente); integração com #65/#67 validada novamente antes de atualizar o PR.
- **#47 fatia 2 (branch `feat/objetos-uso-fatia2`, sobre a fatia 1 já mergeada (#99), 2026-10-16)**: pontos de uso com `action`/`pose`, `usingObject` no interior e pose por (ação, objeto), ADR-0037; plano em `.specs/features/object-use/`. Faltam 3 a 6.
- **Integração do extraordinário (2026-08-12)**: decisão **mista**, sem fase numerada nova para "super-heróis" —
  distribuída entre Fases 16, 17, 23, 24, 25, com toques em 10, 12, 22. ADR-0010 + `docs/domain/powers.md`.
- **Dívida Fase 9**: PERF-12 hasher; `LongRunScaleTests` só nightly.
- **Gate**: `bash scripts/verify.sh`. No Windows, Git bash. Sem `Category=Scenario` no rotina.
- **População default ~40/100** após calibração econômica (Fase 5) — coerência, não "interessante".
- Fases 0–13 e 28 (motor) fechadas; 24–26 parciais; 14, 17–23, 27 sem implementação. Ver `ROADMAP.md`.
- **Fase 13**: períodos = startpoints dinâmicos; autoria de catálogo sem runtime de IA interno.
- **Eval**: `verify.sh`; `verify-mutation.sh` manual. Harness: [`HARNESS.md`](HARNESS.md).
- **Budget**: nenhum loop autônomo ativo.

- **Verificado (#65/#67, 2026-10-16)**: branch `feat/issues-65-67`, ADR-0036/0035. Relatórios independentes PASS em `family-diversity-calibration`, `lodging-and-rent` e `saved-worlds`; 20 seeds × 100 anos no cenário completo, mundos persistentes e provas causais de distância/SQL na composição API. Após integrar #47: gate 2342/3 skips, integração completa 443, Godot 270; prova da resolução em `saved-worlds/merge-verification.md`. Entrega por PR para `develop`; fechamento das issues no merge.

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
