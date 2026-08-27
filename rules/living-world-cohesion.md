# rules/living-world-cohesion.md — carregada em: novo sistema / integração / feature de simulação

Regra permanente desde a Fase 16.3 (Living World Cohesion). O motor não é uma coleção de
sistemas paralelos — é **um mundo causal**. Todo sistema novo entra conectado ou não entra.

## Princípio
Sistemas coexistem sem consequências cruzadas = cenário de fundo pintado. Um mundo vivo:
cada sistema **produz** estado que outro **consome** (decisão, economia, ecologia, combate,
mortalidade, etc.), com cadeia causal auditável.

## SEMPRE FAÇA
- **Consumidor real antes de fechar.** Atributo/entidade/evento novo tem pelo menos um
  consumidor causal no tick (decisão, produção, movimento, mortalidade, ecologia…).
  Sem consumidor = `FUTURE_DEPENDENCY` documentado no audit — nunca stub “para depois”
  fingindo integração.
- **Decisão via `DecisionContext`.** Scoring de Agent (`SelectByUtility` e sucessores)
  recebe contexto escopado (AD-011). Não leia `WorldState`/`Npc` crú no loop de utility;
  exponha fatores novos pelo builder do contexto.
- **Proveniência.** Eventos relevantes carregam `EventId` / `CauseEventId` / `SourceSystem`
  (AD-013). Cadeias cross-system são reconstruíveis sem `CreateXStory()` hardcoded.
- **Poder modula, não substitui.** Efeito extraordinário multiplica/desvia a simulação de
  base (fauna, flora, temperatura, combate…). A base roda com `Extraordinary.Enabled =
  false`.
- **REUSE > EXTEND > REFACTOR > REPLACE > CREATE.** Antes de criar sistema paralelo,
  estenda o existente (needs, economy, relationships, memory, powers, ecology).
- **Orçamento Fase 9.** Entidades leves (animal/planta) têm custo próprio menor que NPC;
  massa não fura `PerfRules` — degrada (decaimento preguiçoso), não trava o tick.

## NUNCA FAÇA
- Sistema “só de apresentação” (UI/API) que nunca entra em decisão, economia ou ecologia —
  salvo LOD/zoom explicitamente by-design.
- Pipeline de decisão paralelo (“Power AI”, storyteller forçando outcome, LLM decidindo).
  Utility AI (ou Decision Source declarado) é o árbitro único; LLM só narra (ver
  `llm-boundary.md`).
- Enum/`ActionType` por poder ou mecânica específica — categoria + candidato dinâmico
  (AD-012).
- Feature nova que ignora fauna/flora/clima/corpo/relações já no mundo quando o efeito
  deveria tocá-los.

## Checklist rápido (antes do commit da task)
1. O que este sistema **escreve**? Quem **lê** no mesmo tick ou no seguinte?
2. Com poderes desligados, o comportamento de base ainda existe?
3. Há `Fact`/`WorldEvent` com causa quando a transição importa?
4. Sensor/audit: CAUSAL, não PRESENTATION_ONLY?

## Ver também
`docs/audits/living-world-cohesion-audit.md` · `.specs/features/phase-16-3-world-cohesion/` ·
AD-011..013 em `.specs/STATE.md` · `rules/simulation-determinism.md` · `rules/llm-boundary.md`
