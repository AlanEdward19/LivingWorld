# Fase 16.2 Context

**Gathered:** 2026-08-25
**Spec:** `.specs/features/phase-16-2-power-evolution/spec.md`
**Status:** Ready for design — todas as decisões confirmadas, sem pendência

---

## Feature Boundary

Progressão de poder por estágio (idade E/OU uso) + herança genética com 3 caminhos possíveis
(ambos os poderes dos pais / só um / mistura recombinada), sem teto anti-inflação, cobertura
completa (qualquer mecânica registrada na 16.1 participa, sem exceção). Depende do registro de
mecânicas da `phase-16-1-power-engine` já existir.

---

## Implementation Decisions

Todas as 4 decisões abertas na primeira versão desta spec foram respondidas explicitamente
pelo usuário em 2026-08-25:

1. **Gatilho de estágio — "Ambos"**: idade E contador de uso, autor do poder escolhe por
   estágio se usa um, outro, ou os dois (AND) como limiar.
2. **Modelo de mistura — "Siga sua proposta... todas essas possibilidades"**: usuário pediu
   explicitamente que EXISTA possibilidade de o filho ter (a) ambos os poderes dos pais, (b)
   só um deles, (c) uma mistura — as 3, não uma escolha única de modelo. Reformulei a spec
   pra tratar isso como um espaço de 3 resultados possíveis, escolhido deterministicamente
   com pesos configuráveis em regra de cenário (nunca hardcoded pra sempre escolher o mesmo
   caminho).
3. **Teto anti-inflação — "Pode somar"**: removida a história/AC de teto que a v1 desta spec
   tinha proposto (contradiria a resposta do usuário) — magnitude do resultado "mistura" pode
   exceder qualquer um dos pais isoladamente, sem limite artificial.
4. **Escopo — "Completo por favor"**: removida a limitação de "amostra de 2-3 poderes" — nova
   história dedicada (`EVO-20..22`) exige que TODA categoria de mecânica da 16.1 participe
   (estágio funciona em qualquer uma, mistura entre categorias diferentes nunca erra por
   "incompatibilidade").

### Agent's Discretion

- Onde o contador de uso/estágio corrente vive — Design decide entre `ExtraordinaryCarrierState`
  (preferencial, mesmo padrão já usado na 16.1 pra estado por-poder) vs. campo em `Npc`.
- Algoritmo exato de seleção determinística (hash de seed+NpcId+índice) pros 3 resultados de
  herança e pra escolha de eixo/pai dentro do resultado "mistura" — livre pra Design escolher,
  desde que reproduzível e sem RNG não semeado.
- Pesos default dos 3 resultados (ambos/um só/mistura) quando o cenário não declara nada —
  Design/Tasks decide um default razoável (ex.: uniforme 1/3 cada), documentado como
  configurável, não fixo.

---

## Specific References

- `.specs/features/phase-16-1-power-engine/spec.md` (`PWR-40..41`) — texto original do P3 que
  esta fase promove a feature própria.
- `.specs/features/phase-16-1-power-engine/design.md` — registro de mecânicas
  (`IExtraordinaryMechanic`) que esta fase consome sem adicionar namespace novo de token; lista
  de categorias de mecânica que a nova história de cobertura completa (`EVO-20..22`) precisa
  varrer.
- `NatalitySystem`/`MortalityPlanner` — pontos de integração já confirmados no recon da 16.1.

---

## Deferred Ideas

- Herança de mais de 2 "pais" (poliamoria genética de poder) — não pedido, `NatalitySystem`
  hoje só modela 2 pais por concepção.
