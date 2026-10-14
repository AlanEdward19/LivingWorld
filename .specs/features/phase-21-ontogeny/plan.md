# Fase 21 — Ontogenia [Estado: Ativo - 21a entregue (AD-115, ADR-0026); 21b #71, 21c #72]

Substitui o formato antigo (`spec.md`/`design.md`/`tasks.md`, apagados em 2026-10-14 com aprovação do usuário; histórico no git).
Decisões herdadas do design: marco é **filtro pré-utility** (não pontua zero), choro é
`ActionType` comum pontuado pela fome, criança agregada usa **resample** no pool, predisposição é o `Npc.RateGene`
da Fase 6, sistema fica inerte após a última janela (`OntogenyLifecycleGate`).

## Problem
NPCs nascem com todas as ações disponíveis. Não há bebê, aprendizado por convívio, janela crítica nem idioma
herdado de quem cria. Isso fere o foco de realismo e o objetivo de vida humana plausível.

## Fluxo
Nascimento → só `Cry`/mamar/dormir elegíveis → exposição acumulada (cuidadores do `Household`) → progresso por
marco via `Resolver` (perfil `Agregado`, taxa = `RateGene`) → janela fecha → teto cai (perda parcial) →
trauma/doença regride com causa → adulto: sistema inerte.

## Fatias (uma issue, um PR cada, nesta ordem)
- **21a Modelo**: marcos como dado de cenário (6 eixos, janelas, pré-requisitos), `ActionCatalog.RequiredMilestone`,
  `MilestoneEligibilityFilter`, `Cry`, `OntogenyLifecycleGate`, flag de desligar.
- **21b Aquisição**: `ExposureAccumulator`, `MilestoneProgressSystem`, `WindowClosureSystem`, gene como taxa.
- **21c Idioma, regressão, agregado**: `LanguageFluencyResolver`, crença copiada pelo canal ambiental,
  `MilestoneRegressionSystem`, `MilestoneProgressSum` no pool + resample na materialização.

## Critérios (EARS) — finais, sob `rules/eval-criteria.md`
Gate: horizonte ≤ 10 anos; braços pareados na mesma seed, ≥ 20 seeds; limiares vêm do cenário `test-ontogeny`.
1. **21a** WHEN o catálogo é criado, SHALL falhar nomeando toda `ActionType` sem `RequiredMilestone` (reflexão);
   a cada tick em 10 anos nenhum NPC executa ação cujo marco não atingiu. Mutação: desligar o filtro reprova.
2. **21a** WHEN um NPC nasce, SHALL ter progresso 0 em todos os eixos e só `Cry`, mamar e dormir elegíveis;
   `Cry` é escolhida pela utilidade da fome, sem script.
3. **21a** WHEN o subsistema é desligado por flag, o hash canônico em 10 anos SHALL mudar.
4. **21b** Convívio causal: braço cuidador ausente atinge menos marcos até a idade de corte do cenário em ≥ 18/20 seeds.
5. **21b** Janela falseável: ordem estrita `prazo > tardia > nenhuma` em ≥ 18/20 seeds e `tardia` nunca iguala `prazo`.
6. **21b** Gene: genes e exposição idênticos → NPCs byte-idênticos (20/20); genes diferentes e exposição idêntica →
   idades de aquisição diferem (20/20).
7. **21c** Idioma: órfão criado por cuidador de outro idioma termina com fluência maior no idioma do cuidador
   (20/20) e fluência é 0 ao nascer.
8. **21c** Toda queda de marco tem, no mesmo tick, evento de trauma/doença com causa; assert a cada tick (10 anos).
9. **21c** Criança em pool agregado materializada duas vezes na mesma seed recebe o mesmo `DevelopmentState`.
10. **Todas** Determinismo em dois processos; `Cry`/marcos sem `Random`/`DateTime`; hash estável por golden.

## Portas de mão única
Decididas na 21a (ADR-0026): `Npc.Development` canônico (null explícito = completo), `OntogenyRules` canônico, ontogenia ligada
por padrão, `ActionType.Cry` (duração opcional). A 21b acrescenta teto de janela ao snapshot e a 21c `MilestoneProgressSum` no pool:
novo ADR/pergunta em cada uma. As exigências por ação vivem em `OntogenyRules.Requirements` (cobertura por reflexão), não em `ActionCatalog`.

## Dívida explícita da 21a
`Cry` só tem consumidor na própria utility (o estado é visível no rastro da decisão). O consumidor causal — choro atendido pelo
cuidador conta como contato de exposição — entra na 21b. Criança materializada do pool agregado nasce adulta (materialização
sempre ≥ 18 anos); o resample por pool é da 21c. Viagem e compra que atendem uma intenção elegível são assistidas.

## Desempenho
(21a medido: 101,6% µs/NPC-tick e 101,4% alocação contra a base; ver AD-115.)
Sensor de escala antes/depois de cada fatia: µs/NPC-tick ≤ 103%, alocação/tick ≤ 102%, hash idêntico com flag desligada.
Adulto fora de janela custa O(1) (`OntogenyLifecycleGate`).

## Fora de escopo
Escola (Fase 8), ofício (Fase 6), defeito congênito e doença (Fase 22), prosa (Fase 12), ML em runtime (ADR-0005).

## Perguntas resolvidas pelo design
Agregado → resample; marco escalar por eixo; ação indisponível sai do conjunto (filtro); choro é utilidade normal;
sistema para após a última janela.
