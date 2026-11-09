# Fase 16 — Potência

**Status**: parcial — arquitetura concluída (`.specs/features/phase-16-powers/`); motor dinâmico entregue pela issue #64 (`.specs/features/power-primitives-registry/plan.md`), faltam as decisões abaixo.

> **Estado real (2026-11-08, issue #64)**: efeitos/custos são tokens de dados resolvidos por
> `IExtraordinaryMechanic`; os atributos do NPC vivem numa tabela única (`NpcStatAccessors`), sem
> `switch` por nome em `Simulation/Extraordinary/` (teste de arquitetura). Cada mecânica declara
> seus `Primitives`; o catálogo (`ExtraordinaryPrimitiveCatalog`) é derivado do registro e o
> `ExtraordinaryScenarioLoader` recusa na carga token que nenhuma mecânica aceite.
> **Falta (decisão do dono)**: rota `GET /authoring/extraordinary/primitives` (contrato novo, #46-5);
> fechar a fase no `ROADMAP.md`; medir o sensor de escala (≤103%/102%) e rodar o Verifier independente.

**Objetivo**: mutante/mago/artefato/implantado viram **um modificador declarado** sobre sistemas
existentes (fonte, efeito, custo, probabilidade, falha, consequência social), conservando
dinheiro/recursos. Critérios finais: `rules/eval-criteria.md`.

## Tasks
1. **`Extraordinary.Enabled` por mundo**: desligado = zero portadores/aquisição/manifestação no caminho quente.
2. **Descritor como dado de cenário** (`powers.md`): sem enum de poder; eixos consultáveis por reflexão.
3. **Modo** (`Passive`/`Active`/`Triggered`/`Conditional`) decide disponibilidade.
4. **Confiabilidade**: `Guaranteed` sem RNG; `ResolutionCheck` via ADR-0011/ADR-0005.
5. **Custo opcional no uso** (não no sucesso). `Costs = []` válido. Falha nunca no-op quando há `ResolutionCheck`.
6. **`PowerAcquisitionRule` declarativa** + progressão opcional; nunca `RandomlyGivePower`.
7. **`ManifestationStateDescriptor` opcional** (transformação); permanente com `RequiredState = none`.
8. **Vulnerabilidade intrínseca** ≠ contramedida (Fase 24).
9. **Predisposição herdável** (Fase 6); habilidade de poder **nunca** no nascimento.
10. **Reação social pela cultura** (`society.md`) — o poder não carrega a reação.
11. **Escassez de cenário** (ADR-0010); portador no LOD agregado como contagem.
12. **Cenário `test-powers` pareado** (+ `test-extraordinary-disabled`).

## Critérios de verificação
- Nenhuma mutação fora do descritor; todo efeito declarado coberto no cenário.
- Falha cobra o mesmo custo do sucesso (quando há custo + `ResolutionCheck`), 10/10 seeds.
- `Enabled = false` zera o sistema (inspeção, não cronômetro).
- Conservação: dinheiro/estoque só por transação/cunhagem/destruição (10 anos gate / 100 nightly).
- Habilidade não atravessa nascimento; predisposição sim (`BirthSampleTarget`).
- Mesma seed, culturas opostas → reações de sinal contrário, 10/10.
- Desligar o sistema muda o hash canônico em 10 anos.

## Resoluções de ativação
- Tempo bloqueado até Fase 18. Longevidade reagenda morte com cadeia causal.
- Portador agregado usa stream da região. Atenção hostil = evento genérico (Fase 17).
- Prevalência é parâmetro/baseline, não gate.

## Fora do escopo
Fases 17–19/12. Escassez sem gate (ADR-0010).

## Ver também
[powers.md](../domain/powers.md) · [npc.md](../domain/npc.md) · [society.md](../domain/society.md) ·
[genetics-and-family.md](../domain/genetics-and-family.md) · [simulation-lod.md](../domain/simulation-lod.md) ·
[ADR-0010](../adr/ADR-0010-potencia-como-modificador-unificado.md) ·
[ADR-0005](../adr/ADR-0005-simulacao-deterministica-semeada.md) ·
[rules/eval-criteria.md](../../rules/eval-criteria.md)
