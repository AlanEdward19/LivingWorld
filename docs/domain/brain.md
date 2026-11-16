# Cérebro legível com ganhos aprendidos (AD-142, ligado por padrão no AD-160b)

> Decisão: [ADR-0046](../adr/ADR-0046-cerebro-com-ganhos-sinapticos-aprendidos.md). Desenho, contas e vereditos:
> `.specs/features/neural-brain/` (`verdict-ad160b.md` é o atual). **Ligado por padrão** no cenário de código e nos mundos
> JSON; `{"Brain":{"Enabled":false}}` ou `world.UseBrain(null)` desliga (`BrainRules.Off`, `LW_BRAIN=0` nos relatórios).

## A rede
- **Entrada**: os 22 `UtilityTerm` (fome, sede, despensa, luto, medo, memória de fome...).
- **Camada do meio**: 8 `UtilityGroup`: Base, Comfort, Personality, Context, Affect, Pressure, Foresight, Continuity.
- **Saída**: as 10 `ActionType`; vence a maior nota.
- **Sinapse**: grupo da ação × `1 + g/1000`, `g` inteiro (milésimos) por NPC em `Npc.Brain` (`BrainState.Gains`, 80
  int16), em degraus de 20, preso a [−500, +1000]. **O grupo Base (o impulso do corpo) fica em 0**: não aprende nem herda.

## De onde vem o ganho (nada escrito por nome de ação)
1. **Nascimento** (`NatalitySystem`): `h × média dos pais` (sem ruído no padrão); o filho herda também a média do que os
   pais esperam de cada ação (`ActionMeans`). Fundadores nascem neutros.
2. **Criação** (`SkillTeachingSystem.SampleRearing`, a cada 30 dias da infância): 1% na direção dos cuidadores.
3. **Experiência** (`BrainMechanic.Learn`): ao escolher, o NPC guarda num número (`BrainState.Pending`) a ação, a
   participação de cada grupo na nota e o **valor que dava ao próprio estado** (`BrainValue`: corpo + 20 por dia de comida e
   água na despensa por morador + 10 por dia de comida que a moeda compra, ambos até 4 dias, + 100 × humor). Ao concluir,
   recompensa = valor agora − valor no começo (+ o salário que a jornada rende, se foi trabalho). Surpresa = recompensa − o
   que a ação costuma render; cada grupo muda `taxa(idade) × 0,05 × surpresa × participação`, em degraus de 20 com
   sorteio sem viés (stream do mundo por NPC e tick). A 1ª vez só forma a expectativa; a expectativa anda em degraus de 16.

## Onde o cérebro decide
- **Urgência** (`SelectByUtility(brainGains:)`): com fome ou sede ≤ 30 o corpo decide pela utilidade pura; fora disso os
  ganhos pesam. Sem ganhos que pesem, a conta é a da utilidade (sem lente) e só a vencedora é recontada para aprender.
- **Tempo livre** (`BehaviorDecisionSystem.Brain.cs`): no começo da faixa de conversar/ficar à toa, escolhe entre conversar,
  deitar mais cedo (2 h antes da cama) e ficar além do turno (sem fome nem sede), pela forma da utilidade com os ganhos de
  temperamento e hábito. Com fome ou sede < 50 segue a rotina; não troca conversar por ficar à toa (o passeio à toa levava
  para longe da água). Não guarda essa escolha para aprender (`LeisureLearns`: custo e bytes).

## Exemplo concreto
Lavrador de 30 anos, despensa com 1 dia, 2 dias de comida em moedas. Compra 3 dias: despensa +40, moeda −20, a ida custou
5 de necessidades: recompensa +15 contra uma expectativa de +5. Os grupos que o levaram a comprar (pressão da memória de
fome, antevisão) sobem um degrau com chance proporcional; o impulso da fome, não.

## Ver o cérebro
`BrainReadout.Read(world, npcId)`: termo → grupo → ação com valor e ganho (a soma das sinapses é a nota) e o valor do
estado por parcela. API: `GET /npcs/{id}/brain`. Cliente: linha do painel Cérebro (`brain_panel.gd`).

## Medir
- Sonda: `dotnet <dll> brain-trace <seed> <seeds> <anos>` (troca neural × utilidade, ganhos médios); `LW_TRACE_AWAY=1`
  mostra quem tem sede crítica longe de casa e o que faz.
- Vila com e sem: `LW_BRAIN_OUT=f LW_BRAIN_SEEDS=n LW_BRAIN_YEARS=a [LW_BRAIN_VARIANT=v] ... --filter BrainExperimentHarness`
  (variedade de dias, bytes, e `brainBytesShare`: bytes do cérebro no mesmo mundo).
- Aflição: `demography-report` com `LW_BRAIN=0` (sem) e sem a variável (com); variantes em `BrainRules.Variant`.
- Motor: `MeasureHarness` com `LW_MEASURE_BRAIN=off|1|<variante>`.

## Histórico
AD-142 (não ligar: custo, bytes, demografia), AD-160 (não ligar: corpo crítico, viés de comer; ver `verdict-ad160.md`),
AD-160b (ligado; custo +5,5% em aberto; ver `verdict-ad160b.md`).
