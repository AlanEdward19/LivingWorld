# Profundidade econômica — Specification

> **Spec, não gate. Sem implementação.** Origem: `docs/audits/phase-gap-review.md` (F5/F8) e AD-089.
> Roadmap F5 pede 5 recursos (trigo, madeira, ferro, água, pedra) e 8 profissões; o cenário padrão
> tem 3 recursos e 2 profissões, e o ferro não tem consumidor.

## Problem Statement

`DefaultEconomyRules` produz trigo/água na fazenda e ferro na ferraria, sem nenhum insumo
(`Inputs = {}`): é coleta, não cadeia. O ferro é produzido e precificado, mas ninguém o consome.
Receitas de construção (`BuildingRecipe.Inputs`) só existem em cenários autorados; no padrão o
catálogo de cidade é vazio, então crescer não gasta material. Acrescentar madeira/pedra/lenhador/
pedreiro sem consumidor seria stub (`rules/living-world-cohesion.md`). A spec amarra produção,
demanda e crescimento da cidade num só desenho.

## Goals

- [ ] **Cadeia com insumo**: ao menos uma receita com `Inputs` reais (ferreiro consome ferro e
      madeira), medida por conservação de recurso (o sensor já existe).
- [ ] **Demanda de construção no padrão**: o catálogo de cidade padrão declara `BuildingRecipe` com
      madeira/pedra/ferro; construir (casa, oficina) gasta material. Sem material,
      `ConstructionSystem` falha sem mudar o hash (já testado).
- [ ] **Recursos 3 (madeira) e 5 (pedra)** e profissões lenhador e pedreiro com oficina própria
      (`LocationType` 3 e 4), salário, janela de rotina e peso no sorteio inicial.
- [ ] **Preço e escassez reagem**: faltou madeira → preço sobe → construção atrasa → moradia
      aperta → migração (a cadeia já existe em Cidades). Teste causal com braço controle.
- [ ] **Ferro com consumidor**: remove o stub atual.

## Out of Scope

| Item | Razão |
| --- | --- |
| Curandeiro, professor | Sem sistema de saúde/escola que os consuma; viraria stub. Spec própria. |
| Comércio entre cidades | Depende de rotas e preço regional; Fase 10+. |
| Tecnologia emergente de ferramenta | Fase 24. |

## Requisitos

| ID | Requisito |
| --- | --- |
| ECOD-01 | Receita padrão com `Inputs` não vazio; consumo igual ao declarado (teste exato). |
| ECOD-02 | Catálogo de cidade padrão com ≥2 receitas de construção usando recursos 3/5/4. |
| ECOD-03 | Lenhador/pedreiro: `Hired` ocorre, produção > 0, salário pago, rotina de trabalho. |
| ECOD-04 | Conservação de moeda e de cada recurso (3 e 5 incluídos) exata a cada amostra. |
| ECOD-05 | Escassez de madeira (braço tratamento) atrasa construção vs controle em ≥18/20 seeds. |
| ECOD-06 | Nenhum recurso/profissão sem produtor e consumidor (varredura por reflexão no catálogo). |

## Medição obrigatória (antes e depois)

- **Vila estável**: `MeasureHarness` (100 NPCs × 10 anos × 5 seeds, população por ano). Critério: soma da
  população no ano 10 não cai mais que 10% (a sede de AD-089 custou 3,6%).
- **Performance**: µs/NPC-tick no fixture de 1.000 não sobe mais que 5%; alocação/tick dentro de ±2%. Mais 2
  oficinas por cidade (`SeedDefaultWorkplaces`); `ScaleScenarioFixture` precisa do mesmo catálogo.
- **Goldens**: muda todos os hashes (`tests/golden`, pins de `OptimizationEquivalenceTests`) — regravar uma vez, no fim.
- **Calibração**: a reserva inicial de 50 trigo/água por casa (AD-046) mantém a vila viva; madeira/pedra precisam
  de reserva equivalente, ou a construção só começa depois do primeiro ciclo de produção.

## Decisões abertas

1. Construção compra no mercado (preço) ou consome estoque da cidade? Recomendado: mercado, reaproveita preço.
2. Peso inicial de lenhador/pedreiro no sorteio: uniforme ou menor que o de lavrador?
