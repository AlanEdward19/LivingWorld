# Cérebro legível com ganhos aprendidos (experimento, AD-142)

> Decisão: [ADR-0046](../adr/ADR-0046-cerebro-com-ganhos-sinapticos-aprendidos.md). Desenho e contas:
> `.specs/features/neural-brain/`. **Desligado por padrão**; liga com `{"Brain":{"Enabled":true}}` no cenário
> ou `world.UseBrain(BrainRules.Default)`. Desligado, o hash, os goldens e o snapshot são os de antes.

## A rede
- **Entrada**: os 22 `UtilityTerm` (fome, sede, despensa, luto, medo, memória de fome...).
- **Camada do meio**: 8 `UtilityGroup` que a fórmula já tinha: Base, Comfort, Personality, Context, Affect
  (luto, medo, alegria, vergonha, solidão), Pressure (renda, memória de fome), Foresight, Continuity.
- **Saída**: as 10 `ActionType`; vence a maior nota, como antes.
- **Sinapse**: grupo da ação × `1 + g/1000`, com `g` inteiro (milésimos) por NPC em `Npc.Brain`
  (`BrainState.Gains`, 80 int16), preso a [−500, +1000]: nenhum grupo cai abaixo de metade (a fome não some).

## De onde vem o ganho (nada escrito por nome de ação)
1. **Nascimento** (`NatalitySystem`): `h × média dos pais + √(1−h²) × segregação` (±100 milésimos, stream
   `brain` do bebê), `h` = herdabilidade de personalidade do cenário. Fundadores nascem neutros (zero).
2. **Criação** (`SkillTeachingSystem.SampleRearing`, a cada 30 dias da infância): 1% na direção da média dos
   cuidadores da casa; quem convive mais puxa mais.
3. **Experiência** (`BrainMechanic`): na escolha pela utilidade o NPC guarda a participação de cada grupo na nota da
   vencedora (`PendingShares`); na conclusão daquela ação mede a recompensa vivida = necessidades que subiram +
   despensa que encheu valendo a fome ou sede que sentia (até 2 dias) + 100 × mudança de valência. A surpresa é
   recompensa − o que essa ação costuma lhe dar (`ActionMeans`, média móvel 1/8). Cada grupo muda
   `taxa(idade) × 0,1 × surpresa × participação`; taxa 1 até 20 anos, cai em linha reta até 0,25 aos 60.

## Exemplo concreto
Adulta de 25 anos, despensa vazia, fome 10 (déficit 90). A utilidade escolhe Comprar; 85% da nota veio de Base
(fome), 15% de Pressure (memória de fome). Ela compra 4 dias de comida: recompensa ≈ 90 (perspectiva de saciar a
fome), Comprar costumava lhe dar ≈ 20, surpresa +70: `g[Comprar, Base] += 0,1 × 70 × 0,85 ≈ +6`. Da próxima
vez, com fome, Comprar pesa 0,6% a mais para ela; para a irmã que nunca passou fome, não.

## Consumidor causal
`BehaviorDecisionSystem.SelectByUtility(brainGains:)` (os ganhos do NPC, como a elegibilidade, fora do
`DecisionContext` para não pesar a alocação desligado): o ganho muda a nota e a escolha urgente. Desligado, a conta é
especializada por tipo (`NoBrain`) e o JIT a reduz à de antes.
O AD-138 continua valendo: o rastro mostra cada termo com o valor que somou (já com ganho).

## Ver o cérebro (opção b)
`BrainReadout.Read(world, npcId)`: projeção volátil termo → grupo → ação, com valor e ganho; a soma das sinapses
de uma ação é a nota dela. Ainda não exposta na API nem no `brain_panel.gd` (mudança de contrato público).

## Rede por NPC (opção a), só no sandbox
`NeuralPolicySandbox`: 28-16-10 em int16 com acumulador int32. Nenhum sistema a chama; existe para medir custo
(`BrainDecisionCostHarness`).

## Medir
- Custo por decisão: `LW_BRAIN_COST_OUT=f bash scripts/test.sh --project longrunning --filter BrainDecisionCostHarness`.
- Experimento (vila, com e sem): `LW_BRAIN_OUT=f LW_BRAIN_SEEDS=5 LW_BRAIN_YEARS=10 ... --filter BrainExperimentHarness`.
- Motor: `LW_AFTER_ENV=LW_MEASURE_BRAIN=1 LW_MEASURE_SCALE_ONLY=1 bash scripts/compare-simulation-cost.sh .`

## Resultado do experimento (AD-142, 2026-11-13)
Não aprovado para ligar por padrão. Custo do motor dentro do teto a 1.000 NPCs (+0,6%), fora a 5.000 (+7,0%); bytes
fora (+3,3% JSON por vivo, +6,8% positional por NPC-ano); diversidade de dias no mesmo ofício não sobe; 20 seeds ×
100 anos: população final 149,3 → 140,9 e mais mortes por fome e sede. Herança funciona (pais → filho 0,55), ganhos
pequenos (|g| médio 1,4%). Números completos em `docs/decisions-log.md` (AD-142).
