# Issue 91: histórico nos mundos do catálogo

## Problem Statement
Mundos criados pelo menu deixam história desativada. O runtime envia eventos ao sink,
mas não grava fatos. Habilitar as regras sozinho continua produzindo consultas vazias.

## Out of Scope
Novo formato público de cenário, migração de saves antigos, UI e dependências novas.

## Assumptions & Open Questions
| Assumption | Chosen default | Rationale |
|---|---|---|
| Configuração do catálogo | HistoryRules.Default com limiar 0.4 | Mortes por privação valem 0.414375; morte comum, 0.49725. |
| Metadados e produtores explícitos | Não gravar novamente | Evita fatos recursivos e duplicados. |
| Integração central | Autorizada pelo usuário em 2026-10-16 | O usuário pediu continuar após conhecer o impacto. |
| Perguntas abertas | Nenhuma | A issue autoriza definir os defaults do catálogo. |

Open questions: none.

## User Stories
Como jogador, vejo história produzida pela simulação sem trocar o código do cliente.
Como NPC, posso reagir a relatos de fome conhecidos pela minha comunidade.

### Acceptance Criteria
- WHEN um período de catálogo cria um mundo, o carregador SHALL habilitar história.
- WHEN um evento significativo ocorre, o motor SHALL gravar um fato no mesmo tick,
  antes de notificar o sink e mesmo sem sink, preservando kind, tick, payload e participantes.
- WHEN história está desligada ou o evento é irrelevante, o motor SHALL omitir o fato.
- WHEN um metadado histórico ou produtor explícito emite evento, o motor SHALL evitar duplicação.
- WHEN a última testemunha morre, o motor SHALL gerar relato e cânone pela conversão existente.
- WHEN cada período avança três anos, as rotas de relatos e crenças SHALL retornar itens,
  e biografias e crônicas SHALL ter claims ancorados.
- WHEN um relato de morte por fome chega ao sobrevivente, a utilidade de Buy SHALL subir 40
  no mesmo contexto, usando o consumidor de crenças existente e preservando o payload factual.
- WHEN dois processos usam o mesmo período e seed, o hash SHALL coincidir e outra seed divergir.
- WHEN história é ligada em 1.000 NPCs por 720 ticks, a mediana de µs/NPC-tick SHALL ser
  ≤103% do controle desligado em cinco pares alternados, após aquecimento.

## Requirement Traceability
| ID | Requirement | Status |
|---|---|---|
| HIST91-01 | Catálogo habilitado e narrativas pela API | verified |
| HIST91-02 | Gravação síncrona, filtragem e sem duplicação | verified |
| HIST91-03 | Consumidor causal de relato de fome | verified |
| HIST91-04 | Hash em dois processos | verified |
| HIST91-05 | Sensor de custo ≤103% | verified |

## Execução
Uma entrega atômica: ligar o carregador e gravação central, remover regravação dos digests,
conectar o relato de fome ao consumidor existente e incluir sensores de regressão.
Arquivos: ScenarioLoaderV2, TickContext, HistorySimulationDigest, DistortionEngine,
probe de Workers, testes RuntimeHistory/CatalogHistory/custo, fixture de escala e ADR-0037.
O sensor exige cache hop-0 invalidada por troca de cânone, sem copiar crenças vazias
para o scoring; cache derivada não muda RNG nem sobrevive a novos mundos.
Gate: scripts/verify.sh, integração do catálogo e sensores longrunning focados.
