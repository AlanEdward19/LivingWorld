# ADR-0046: Cérebro legível com ganhos sinápticos herdados e aprendidos

- **Status**: proposto
- **Data**: 2026-11-12
- **Decisores**: dono do projeto (Alan), pendente; rascunho por agente

## Contexto
O dono quer o cérebro do NPC "tipo os neurônios e rede neural (ao ler o cérebro)". A decisão hoje é uma
utilidade com 22 termos nomeados sobre 10 ações, numa fórmula de forma fixa
(`src/LivingWorld.Simulation/Behavior/Decision/BehaviorDecisionSystem.Terms.cs:11-93`), e o AD-138 garante
que todo termo do rastro é motivo real. Pesos de personalidade são uma tabela ±1 em código
(`src/LivingWorld.Domain/Behavior/PersonalityWeighting.cs:15-32`). A decisão custa ~1,4 µs por despertar e
a meta do dono é desempenho alto. Desenho e contas: `.specs/features/neural-brain/design.md` e `cost-estimates.md`.

## Decisão
Vamos manter a forma da utilidade e dar a cada NPC uma matriz de ganhos sinápticos inteiros g[ação, grupo de
termos] (10 × 8, int16 em milésimos), multiplicando cada grupo por (1 + g/1000). O ganho nasce da média dos
pais com segregação semeada, é puxado pela criação e ajustado pela experiência (recompensa = alívio vivido,
taxa que cai com a idade), limitado a [−500, +1000]. O painel "ver o cérebro" passa a desenhar a rede termo →
grupo → ação com as contribuições reais e os ganhos aprendidos, só para NPC observado. Tudo atrás de flag
(desligada = hash de hoje) até o experimento aprovar: teto +3% CPU e +2% alocação no
`scripts/compare-simulation-cost.sh`, snapshot ≤ +3%, demografia na faixa e diversidade de dias maior.

## Alternativas consideradas
- Rede por NPC (28-16-10) substituindo a utilidade: +43% por despertar, +10% a +41% de snapshot, neurônios
  ocultos sem nome (perde o AD-138 em termos humanos) e risco alto de comportamento degenerado e de quebrar
  toda a calibração demográfica. Fica só no `DecisionSandbox`, para medir.
- Rede só como leitura sobre os termos atuais: custo zero e exata, mas nada aprende; vira a visualização desta decisão.

## Consequências
- **Positivas**: herança e plasticidade de verdade; NPCs do mesmo ofício passam a diferir; leitura nominal mantida;
  custo estimado +4% por despertar e +1,3% de snapshot.
- **Negativas / trade-offs**: ganhos canônicos aumentam diffs de snapshot (~+3 a +4% bytes/NPC/ano se mensais);
  o +4% pode passar do teto de CPU se os despertares por NPC-tick estiverem perto de 0,6; nova fonte de deriva
  demográfica a conferir; goldens regravados se ligado por padrão.
- **Follow-ups**: medir despertares por NPC-tick; consolidação trimestral se os bytes passarem; aprender também
  os pesos de personalidade (hoje tabela ±1) só depois, em ADR próprio.
