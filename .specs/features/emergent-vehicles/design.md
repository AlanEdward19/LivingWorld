# Veículos emergentes — Design

> Segue `rules/living-world-cohesion.md`: REUSE > EXTEND > CREATE. Nenhum sistema paralelo de
> decisão, nenhum `enum` de veículo, nenhuma LLM no estado (ADR-0013, `rules/llm-boundary.md`).

## Reuso (o que já existe e vira ponto de encaixe)
| Existe | Papel aqui |
|---|---|
| Nó de tecnologia da Fase 24 | O veículo **é** um nó de categoria *mobilidade*; limiar de novidade, denominação e esquecimento vêm de lá |
| `TravelResolution.TicksBetween` + `CostWeights` | Custo = `Base × distância × fator_do_veículo[terreno]` (+ subida); sem veículo, o fator é o de hoje |
| `ExtraordinaryLocomotion` (ADR-0021) | Mesmo contrato de passos reais por hora; veículo entrega um perfil (velocidade por terreno, carga) |
| `DecisionContext` | Nova fatia `AccessibleMobility`; utilidade de `Travel`/`Work`/`Buy` lê o tempo poupado |
| `SkillTeachingSystem`, `Relationship`, relatos (Fase 10) | Difusão do saber, com distorção |
| `ProductionSystem` / receitas | Construção: receita **gerada do descritor** cujo produto é uma instância, não um estoque |
| `LazyNeed` (AD-069) | Desgaste e cansaço do animal: valor + tick + taxa, materializado na leitura |
| `Animal` (fauna) + `Relationship` | Domesticação como vínculo gradual NPC↔animal (ver questão em aberto) |

## Dados
- **`MobilityDescriptor`** (canônico, estrutural): vetor de eixos — propulsão, contato, fator por
  terreno, carga, passageiros, requisitos, manutenção, modo de falha. O **id é derivado dos
  eixos**; dois mundos com os mesmos eixos têm o mesmo id (ADR-0013).
- **`Denominação`** (canônica): `cultura × descritor → token`. **Rótulo**: volátil, recomputável,
  nenhuma decisão o lê (ADR-0014). Sem LLM, nome determinístico por composição.
- **`VehicleInstance`** (canônica, leve): id, descritor, dono (NPC/domicílio/workplace),
  célula, condição (`LazyNeed`), animal atrelado, carga. Sem lista varrida por tick.
- **`MobilityKnowledge`** (canônica): quem sabe construir/usar qual descritor, com grau.

## Fluxo causal
1. **Pressão** (Monthly, por comunidade): trajeto médio, carga carregada, falhas de `Buy`,
   fome por distância — leitura de estado, vira *motivo nomeado*.
2. **Descoberta** (stream `mobility-discover-<comunidade>`): pressão × observação × habilidade ×
   recurso disponível → hipótese; protótipo gasta recursos e horas de trabalho (Midshift).
3. **Novidade**: distância no espaço de eixos contra os descritores conhecidos → nova entidade
   ou variação (regra única da Fase 24).
4. **Adoção**: `DecisionContext` expõe o melhor perfil acessível; a escolha passa pela
   utilidade, pesada por personalidade, riqueza e memória de acidente — nunca por flag.
5. **Uso**: ao iniciar `Travel`, a duração sai do perfil do veículo; passos reais por hora.
6. **Vida do veículo** (Daily): desgaste, ração do animal (estoque do domicílio), falha por
   modo declarado; acidente grava evento com causa e atinge saúde/mortalidade existentes.
7. **Difusão/esquecimento**: ensino e comércio levam o saber; sem artesão vivo o nó se perde.

## Canônico × volátil, determinismo, proveniência
Canônico: descritores, denominações, instâncias, conhecimento. Volátil: rótulos, projeções
para o cliente. Toda aleatoriedade por `StreamFor` nomeado. Eventos `VehicleDiscovered`,
`VehicleBuilt`, `VehicleBroke`, `VehicleAccident` carregam `CauseEventId` e `SourceSystem`.

## Desempenho (Fase 9)
Nada por NPC-hora: o perfil é resolvido na partida da ação e memoizado por (perfil, origem,
destino) como o `MarketIndex`. Instâncias limitadas pelos donos; fora do escopo observado, a
condição só é lida quando consultada. Com a flag desligada o caminho é o de hoje (hash intacto).

## Ordem de entrega (unidades pequenas, cada uma com consumidor)
1. Descritor e id por eixos (puro, invariantes). 2. Uso em `Travel` com instância de fixture.
3. Receita gerada e construção. 4. Descoberta + limiar de novidade. 5. Difusão/esquecimento.
6. Animal e domesticação. 7. Desgaste, falha e acidente. 8. Decisão e personalidade.
9. Critérios pareados (`test-mobility`: com/sem tração, com/sem isolamento, com/sem contato).

## Riscos
Número de variantes por século (tamanho do índice, Fase 26); vizinhança com estradas
emergentes (acoplar o fator de terreno); equilíbrio sem gate (ADR-0013); layout do fixture de
escala exagera trajetos — medir antes e depois com o harness de `docs/audits/realism-cadence.md`.
