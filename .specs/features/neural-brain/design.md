# Cérebro neural legível — Design (proposta para aprovação, ADR-0046)

> Pedido: `.specs/features/realism-backlog/03-cognicao-e-cerebro.md` (parte 2). Nada implementado.
> Contas de custo e bytes: [cost-estimates.md](cost-estimates.md).

## Como é hoje (visto no código)
- A nota de cada ação é uma soma de termos nomeados: 22 `UtilityTerm`
  (`src/LivingWorld.Simulation/Behavior/Decision/BehaviorDecisionSystem.Terms.cs:11-35`) sobre 10
  `ActionType` (`src/LivingWorld.Domain/Behavior/ActionType.cs:7-30`).
- Forma fixa: `(base × (1 + conforto) × peso de personalidade + contexto + vida interior) × antevisão
  + continuidade` (`BehaviorDecisionSystem.Terms.cs:43-93`). Já é uma rede pequena de arquitetura
  fixa: somas, portões multiplicativos e um "vencedor leva tudo" (`BehaviorDecisionSystem.Utility.cs:33-66`).
- Pesos de personalidade: tabela de influência ±1 escrita em código
  (`src/LivingWorld.Domain/Behavior/PersonalityWeighting.cs:15-32`); traços herdados com segregação
  (`src/LivingWorld.Domain/Population/Personality.cs:57`).
- AD-138: todo termo do rastro é motivo real, provado por intervenção (`knockout`, `Terms.cs:47-50`).
- Custo hoje: ~1,4 µs por despertar (`.specs/features/realism-backlog/03-cognicao-e-cerebro.md:36`).

## As três opções
**(a) Rede por NPC substitui a utilidade.** Entradas ≈ 28 (4 necessidades, 6 emoções, 8 de contexto,
10 traços), 16 ocultos, 10 saídas: 608 pesos + 26 vieses. Pesos herdados (média dos pais + ruído
semeado) e plásticos (recompensa = alívio de necessidade e emoção depois da ação).
- Determinismo: possível só com ponto fixo (int16, acumulador int32): soma inteira não depende da
  ordem. Em `double`, SIMD quebra o hash: `Vector<double>` tem largura 4 no AVX2 e 2 no NEON e a
  ordem da redução muda. Ativação linear por partes (sem `Exp`).
- Custo: +0,6 µs por despertar (+43%); 1,27 KB/NPC em int16 (+10% no snapshot), 5,1 KB em `double`.
- Legibilidade: ruim. Neurônio oculto não tem nome; "ler o cérebro" vira atribuição numérica e
  perde a garantia do AD-138 em termos humanos.
- Risco: alto. Sem alvo de treino, aprende por recompensa e pode cair em ciclos degenerados (não
  comer, não trabalhar); toda a calibração demográfica (AD-130, AD-133) e os goldens caem juntos.

**(b) Rede como leitura, motor intacto.** Neurônios = os 22 termos (entrada) e as 10 ações (saída);
camada intermediária = os grupos que a fórmula já tem (base, conforto, personalidade, contexto, vida
interior, antevisão, continuidade); sinapse = `UtilityContribution` (`Terms.cs:38`), com o peso que
de fato teve. Projeção volátil (ADR-0014), só para NPC observado, como o rastro de hoje.
- Determinismo, custo e bytes canônicos: inalterados (0). Legibilidade: exata.
- Risco: baixo. Limite: nada aprende; o "cérebro" não muda com a vida além do que já muda (memória,
  emoção, personalidade lenta). Pode parecer cosmético ao dono.

**(c) Híbrido: ganhos sinápticos aprendidos sobre a forma atual.** Cada NPC tem uma matriz de ganhos
g[ação, grupo] (10 ações × 8 grupos = 80 int16, milésimos) e a nota vira
Σ termo × (1 + g/1000). Ganho inicial herdado (média dos pais + segregação semeada, como `Personality.Inherit`)
e ajustado pela criação (imitação de quem criou, AD-132) e pela experiência: ao concluir uma ação, a
recompensa (alívio medido de necessidade e emoção) reforça ou enfraquece os grupos que a escolheram,
com taxa que cai com a idade.
- Determinismo: ganho inteiro; produto e soma em `double` na ordem fixa que o `ScoreOf` já tem; sem SIMD.
- Custo: +0,06 µs por despertar (+4%); 160 B/NPC (+1,3%); diffs de snapshot ≈ +4%/ano se consolidado por mês.
- Legibilidade: boa. Cada sinapse continua sendo (termo → ação) com nome; o ganho é "o que este NPC
  aprendeu a valorizar". O teste de intervenção do AD-138 continua valendo.
- Risco: médio-baixo. Limitar g a [−500, +1000] impede apagar a fome; demografia precisa ser conferida.

## Recomendação
**(c), com (b) como a visualização dele.** (b) sozinho não atende "neurônio que muda"; (a) custa dez vezes
mais, perde o AD-138 e arrisca a demografia. (c) dá herança, plasticidade e leitura nominal ao custo de um
multiplicador por termo. Fora do caminho quente, o painel do cliente (`brain_panel.gd`) desenha camadas
termo → grupo → ação com espessura = contribuição e cor = ganho aprendido.

## Experimento antes de adotar
1. Implementar (c) atrás de flag (`NeedsRules.Brain`, desligado = hash de hoje byte-idêntico).
2. Em paralelo, (a) só no `DecisionSandbox` (`src/LivingWorld.Simulation/Behavior/Decision/DecisionSandbox.cs:12`),
   para trocar a estimativa de custo de (a) por número medido; não entra no mundo.
3. Medir: `scripts/compare-simulation-cost.sh` (1.000 NPCs × 30 dias, teto +3% CPU, +2% alocação,
   `scripts/README.md:14`); B/vivo e bytes/NPC/ano; vila padrão 100 NPCs × 10 anos × 5 seeds.
4. Aprova se: custo dentro do teto; snapshot ≤ +3%; mesma seed = mesmo hash em dois processos; desligar a
   flag não muda nada; intervenção do AD-138 passa com ganhos; diversidade de dias entre NPCs do mesmo
   ofício sobe (critério de `03-cognicao-e-cerebro.md`); demografia na faixa de referência; irmãos
   criados juntos ficam mais parecidos em g do que irmãos criados separados.

## Decisões do dono (aprovadas em 2026-11-12: a opção em negrito de cada item)
1. Adotar (c) com (b) como visual? **Sim** | só (b) | (a) por inteiro.
2. Plasticidade aprende por recompensa de alívio vivido? **Sim** | só herança, sem aprender.
3. Ganho limitado (não pode apagar fome nem sede)? **Sim, [−500, +1000]** | sem limite.
4. Rodar (a) só no sandbox para medir? **Sim** | não gastar com (a).
5. Regravar goldens se o experimento aprovar? **Sim** | manter flag desligada por padrão.
