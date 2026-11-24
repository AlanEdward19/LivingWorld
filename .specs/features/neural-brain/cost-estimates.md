# Cérebro neural — contas de custo, bytes, determinismo e testes

> Irmão de [design.md](design.md). Estimativas, não medidas; o experimento troca cada uma por número.

## Bases usadas (de onde vem cada número)
- Decisão: ~1,4 µs por despertar (`.specs/features/realism-backlog/03-cognicao-e-cerebro.md:36`).
- Motor inteiro: 0,844 µs/NPC-tick (`docs/decisions-log.md:70`, AD-119).
- Snapshot: 12.396 B por NPC vivo (`docs/decisions-log.md:67`); 43.863 bytes/NPC/ano a 1.000 NPCs e
  56.846 a 5.000 (`docs/decisions-log.md:43`, AD-093, diffs campo a campo).
- Despertares por NPC-tick (f): não medido. Como a decisão faz parte do total, f ≤ 0,844 / 1,4 ≈ 0,6.
  Uso f = 0,6 como teto.
- Operação escalar em C# (multiplicar e somar com checagem de limite): ~1 ns, conservador.

## Custo por despertar e por NPC-tick (f = 0,6)
| | Δ por despertar | Δ µs/NPC-tick | 1.000 NPCs, Δ por tick | 5.000 NPCs, Δ por tick |
|---|---|---|---|---|
| (a) rede 28-16-10 | 608 MAC ≈ +0,6 µs (+43%) | +0,36 (+43%) | +0,36 ms | +1,8 ms |
| (b) leitura | 0 no caminho quente | 0 | 0 | 0 |
| (c) ganhos 10×8 | ~80 MAC ≈ +0,06 µs (+4%) | +0,036 (+4,3%) | +0,036 ms | +0,18 ms |

Por NPC o custo é linear; o que muda de 1.000 para 5.000 é o cache. (a) em int16 ocupa 1,27 MB (1.000) e
6,3 MB (5.000); em `double`, 5,1 MB e 25 MB, fora do cache L2 a 5.000, e o custo real sobe além da
conta. (c) ocupa 160 KB e 0,8 MB. (b) custa só para NPC observado, onde o rastro já é montado
(`BehaviorDecisionSystem.Utility.cs:49-50`, `captureTrace`). O teto de 0,036 µs de (c) passa
dos +3% de CPU do gate se f estiver perto de 0,6: por isso medir antes.
O aprendizado de (c) roda na conclusão da ação (8 somas), não por tick: desprezível.

## Bytes por NPC
| | Canônico | B/vivo (base 12.396) | bytes/NPC/ano, plasticidade mensal (1.000 / 5.000) |
|---|---|---|---|
| (a) int16 | 634 × 2 = 1.268 B | +10% | +15,2 KB: +35% / +27% |
| (a) double | 634 × 8 = 5.072 B | +41% | +61 KB: +139% / +107% |
| (b) | 0 (projeção volátil, ADR-0014) | 0 | 0 |
| (c) | 80 × 2 = 160 B | +1,3% | +1,9 KB: +4,4% / +3,4% |

Se (c) passar de +3% em bytes/ano, consolidar os ganhos por trimestre (+1,5% / +1,1%).

## Determinismo
- Pesos e ganhos são canônicos (alimentam decisão) e entram no hash (ADR-0014).
- Inteiro em todo valor guardado. Em (a), acumulador int32 sobre pesos int16: a soma inteira é exata
  em qualquer ordem, então pode usar SIMD. Em `double`, SIMD é proibido: a largura de `Vector<double>`
  muda com a CPU (4 no AVX2, 2 no NEON) e a ordem da redução muda o último bit.
- (c) multiplica em `double` na ordem fixa que o `ScoreOf` já usa (`BehaviorDecisionSystem.Terms.cs:47-93`),
  sem SIMD e sem `Math.FusedMultiplyAdd`.
- Ativação: linear por partes (clamp). Se um dia precisar de sigmoide, `DeterministicMath.Exp`
  (`src/LivingWorld.Domain/Shared/DeterministicMath.cs:37`), nunca `Math.Exp`.
- Ruído de herança e de aprendizado só do stream do NPC (`ctx.Rng`, `src/LivingWorld.Simulation/Scheduling/TickContext.cs:39`).

## Herança e aprendizado (c)
- Nascimento: g = média dos pais + segregação semeada, com herdabilidade de cenário (mesmo padrão de
  `Personality.Inherit`, `src/LivingWorld.Domain/Population/Personality.cs:57`).
- Criação (0 a 15 anos): puxa g na direção de quem criou, com peso pelo tempo junto (AD-132).
- Vida: ao concluir a ação A, recompensa r = alívio de necessidade + Δ valência (`PsycheState`);
  g[A, grupo] += taxa(idade) × r × participação do grupo na nota de A. As participações da ação em curso
  ficam guardadas (8 × int16 = 16 B, canônico enquanto a ação dura).
- Limites: g em [−500, +1000]; base de sobrevivência nunca abaixo de metade.

## Testes
1. Mesma seed em dois processos: mesmo hash; seeds diferentes: hashes diferentes.
2. Flag desligada: mundo byte-idêntico ao de hoje.
3. Intervenção (AD-138, `ThoughtIsMotiveTests`): zerar um termo com ganho muda a nota no sentido do sinal.
4. Herança: correlação de g entre pais e filhos > 0; irmãos criados juntos mais próximos do que separados.
5. Plasticidade: NPC que passou fome no mesmo par (ação, contexto) aumenta g[Buy, base] em relação ao irmão.
6. Segurança: em 100 anos, nenhum NPC adulto com comida ao alcance morre de fome por ganho (contagem = 0).
7. Leitura: o painel mostra exatamente as contribuições somadas; soma do painel = nota da ação.
