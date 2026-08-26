# Fase 16 — Potência

**Status**: concluída — spec/design/tasks e validação em `.specs/features/phase-16-powers/`.

> **Reabertura pendente (2026-08-24)**: validação PASS cobriu arquitetura, mas `effects`/`costs`
> ainda são switch C# fechado (`ExtraordinaryInvocationEngine` / `ExtraordinaryLocomotion` —
> 8 chaves). Motor dinâmico + catálogo web ficam para reabrir depois. Detalhe:
> `project_phase16_powers_reopen.md`.

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
