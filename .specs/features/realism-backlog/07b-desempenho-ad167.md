# Bloco de desempenho e tempo dos testes (AD-167, 2026-11-17)

> Continuação de [07](07-desempenho-e-testes.md). Base `e7eaefe1`. Nenhuma otimização mudou o mundo: hash canônico das seeds 1, 7
> e 42 nos anos 1, 5, 10 e 20 igual à base em cada passo; goldens, pins de 90 dias e `family-diversity.json` (regravado dentro do
> `--full`) sem diferença. Medidas em `.medicao/` do worktree, pela fila, uma por vez.

## O que entrou (todas sem mudar o que se decide)

- **Fauna** (`FaunaLifecycleSystem`): vivos sem reordenar a lista inteira (mortos inclusive) cinco vezes por dia, pasto numa grade
  densa do mapa em vez de grupos e dicionário por dia, aptidão de par calculada uma vez por animal. Vila padrão (ano 20): 154 →
  117 ms e 96 → 42 MB alocados por ano simulado.
- **Bônus de passo por cidade** (`Mobility.BonusPct`) guardado até uma tecnologia nova ou adotada (`TechnologiesVersion`): antes
  varria todas as tecnologias em toda viagem, ida ao mercado e pergunta de alcance da despensa.
- **Humor lido uma vez por NPC e tick** (`PsycheMechanic.AffectOf`, estado imutável como chave): decisão, valor do estado do
  cérebro e fim da ação liam os mesmos seis decaimentos.
- **Cérebro**: os grupos da vencedora só são recontados quando a escolha vai ser guardada (quem segue no que faz não guarda).
- **Carga**: "leva alguma coisa?" numa consulta (`InventionLedger.CarriesOrHolds`) em vez de duas buscas binárias por decisão.
- **Sorteio único sem objeto** (`DrawFor`): 20 lugares que criavam um stream só para um `NextDouble`.
- **Conversa**: com poucos vivos, a amostra da vizinhança varre a lista deles em vez de 49 consultas ao índice por célula (a amostra
  é a dos menores (chave, id), não depende da ordem).
- **Testes**: no `--full`, `SkillByAge` (seeds 1–5 no ano 35) e `VillageSurvives` seed 7 (ano 60) leem o século compartilhado em vez
  de simular o mesmo prefixo (no lote, cada um roda o seu, como antes); a conservação de moeda roda uma vez (até o ano 10, depois
  até o 100) para os quatro horizontes. Coletor de servidor (`DOTNET_gcServer=1`) nas suítes do `verify.sh`.
- **Ferramenta**: `profile.sh --village ANOS` (a vila padrão, o mundo dos cenários) e `LW_MEASURE_BRAIN` no perfil.

## Tabela (antes → depois)

| Item | Antes | Depois | Aplicado? | Comando |
|---|---|---|---|---|
| Decisão com a carga (AD-157b), 1.000 NPCs, 2.160 ticks, 6 pares | +16,2% (total +16,1%) | +7,4% (total +13,6%) | parcial | `LW_MEASURE_CARRY=0` vs 1, `profile.sh --pop 1000 --ticks 2160` |
| Cérebro ligado × `LW_BRAIN=0`, mesma medida | −4,5% (ruído 0,88–1,04) | −0,7% (0,96–1,05) | dentro do teto de 2% | `LW_MEASURE_BRAIN=0` vs padrão |
| Vila padrão no ano 20 (µs e B por NPC-tick), 6 pares | 0,876 µs, 245 B | 0,825 µs (−4,8%), 192 B (−22%) | sim | `profile.sh --village 20 --ticks 8640` base × novo |
| 5.000 NPCs, mundo de hoje (cérebro e carga), 3 rodadas | 1,109 µs, 152 B | 1,093 µs, 151 B | sim (1º número a 5.000) | `profile.sh --pop 5000` |
| 1.000 NPCs, mundo de hoje (2.160 ticks) | 1,86 µs, 160 B | 1,80 µs, 159 B; base×novo em 720 ticks +2% (0,95–1,09, ruído) | sem ganho firme | `profile.sh --pop 1000 [--ticks 2160]` |
| 4: par do AD-134 (`ee6f0416` × `bce8e56c`) | — | não medido | não | fora da cadeia; a pergunta (comparar a fatia antiga) não guia mais decisão |
| 24: bytes da mesma DLL | 194/202 B em dois degraus | iguais nas 3 rodadas de cada uma das 10 DLLs; tempo ±1–3% | fechado | cadeia abaixo |
| 18: `RuntimeHistoryCost` | 1,045 no gate | 10/10 sozinho; passa na fase quieta dos dois `--full` | fechado | `test.sh --project longrunning --filter RuntimeHistoryCost` ×10 |
| 32: invenção a 1.000 (inclui a carga) | inconclusivo | +31% tempo (1,11–1,50), −3% bytes; sem a carga ~+15% | medido, aberto | `LW_MEASURE_NO_INVENTION=1` |
| 44: grade d'água (AD-143) | — | dentro do +31% da invenção; sem chave própria | medido junto | idem |
| 63: carga ligada | +14–35% | ver 1ª linha | parcial | idem |
| 106: poderes ligados | 1,9× tempo, 2,1× bytes | 1,13× tempo, 2,35× bytes (4 pares) | medido | `LW_PROFILE_POWERS=1` |
| 43 (AD-143+145) a 5.000 | não medido | 0,986× tempo, 1,00× bytes | medido | cadeia: `profile.sh --pop 5000` de cada merge, em rodízio, 3 rodadas |
| AD-144/146/147 a 5.000 | — | 1,099× tempo, 1,031× bytes | medido | idem |
| 52 (AD-148) | não medido | 1,058×, 1,036× | medido | idem |
| 61 (AD-149) | não medido | 0,991×, 1,000× | medido | idem |
| 65 (AD-151) | não medido | 0,978×, 1,006× | medido | idem |
| 72 (AD-152) | não medido | 1,004×, 1,000× | medido | idem |
| 79 (AD-150+153) | não medido | 1,013×, 1,006× | medido | idem |
| AD-154 a 166 juntos | — | 0,981×, 0,874× | medido | idem |
| Gate completo | 20 min 34 s | 15 min 02 s | sim | `LW_TEST_TRX_DIR=… bash scripts/verify.sh --full` |

## Tempo do gate completo (`verify.sh --full` com a regravação de `family-diversity`, como o diário)

| Suíte (s) | LongRunning | Unit | Integration | lint | Timing | parede | CPU (user+sys) |
|---|---|---|---|---|---|---|---|
| antes (`e7eaefe1`, 2026-11-17) | 1.200 | 585 | 143 | 93 | 33 | 20 min 34 s | — |
| depois, coletor padrão | 1.029 | 524 | 175 | 102 | 33 | 17 min 44 s | 6.806 s |
| depois, coletor de servidor | 868 | 495 | 279 | 145 | 31 | **15 min 02 s** | 6.483 s |

O caminho crítico é o século compartilhado (20 seeds × 100 anos em 20 threads): 18,0 → 15,2 → 12,6 min. Uma rodada de cada; o
relógio desta máquina oscila ±5–10%.

## Por que 5 minutos não fecham

- O `--full` gasta ~6.500 s de CPU; em 8 núcleos o piso é ~13,5 min com empacotamento perfeito. O século sozinho são ~2.000
  vilas-ano (~1,5 s de CPU cada, 128–180 vivos): ~6 min de 8 núcleos só para ele.
- Sem cortar seed, ano ou asserção, 5 min pedem um motor ~2,7× mais rápido. A decisão é 70% do tick e está espalhada em ~25
  trechos de 2–10% cada (perfil por trecho em `.medicao/hotprof-*.txt`): não há um ponto quente que dê 2×.
- O que daria mais, em ordem: paralelismo do tick por cidade (ADR), despertares sem efeito (22% na vila, 42% a 1.000; muda quando
  o NPC reavalia: precisa de comparação demográfica), relações numéricas no snapshot (ADR de formato).

## O que ficou aberto

- **Carga**: a decisão ainda custa +7,4% com ela (meta da parte de código ≤ +2% não fechada; pelo perfil por trecho, ~3% é código:
  `PackForTrip` ~1,7 µs por saída, a refeição de rotina e o refino de destino); o resto do +13,6% é o mundo que muda (relações e
  `needs-decay` dobram no fixture com a carga). Próximo passo: memória da arrumação por casa e por dia.
- **Bytes do snapshot com o cérebro** (+2,1–3,1%, AD-160b): não medidos de novo nesta fatia.
- **20 seeds × 100 anos** (topo demográfico): não rodado; nada mudou o mundo (hashes e goldens iguais), então não há o que comparar.
- **Item 4** (par do AD-134): não medido. **Invenção** +31% a 1.000 e **poderes** 1,13×: medidos, sem otimização nesta fatia.
- **5 minutos**: ver acima; o melhor número é 15 min 02 s.
