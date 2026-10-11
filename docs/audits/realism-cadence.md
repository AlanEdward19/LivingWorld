# Realismo de decisão e cadência de despertar (B3) — 2026-10-11

Pergunta: reduzir/reorganizar os despertares dos NPCs (custo intrínseco em escala) perde realismo?
Critério do dono do projeto: **simular o que uma pessoa real faria para tomar decisões.**
Método: harness externo que roda o cenário e mede, por hora do dia, o que a população adulta faz; compara variantes.
Todas as medidas abaixo são do cenário default (60 NPCs, 20 dias após 5 de aquecimento) e do fixture de escala (400 NPCs, 15 dias), seed 1.

## O que a medição achou (e foi corrigido)

| # | Achado | Evidência | Correção |
|---|---|---|---|
| 1 | O NPC trabalha até morrer de sede | `Work` pontuava 119,5 contra `Eat` 100 com sede 0: o peso de personalidade (até ~2,3 × 50) passa o teto linear do déficit (100). Rastro de um NPC: 76 h sem comer, fome e sede em 0, ainda `Work` | `SurvivalDeficit`: fome/sede/sono viram urgência escalada acima do déficit 80 |
| 2 | Ciclo do dia em ~31 h, deslocado ~9 h | Pico de sono às 3–9 h, trabalho às 15–21 h; 60% da população em `Travel` à meia-noite | `RoutineWithCommuteLead` (sair cedo para chegar na hora) + despertar na fronteira da rotina menos o trajeto (`RoutineBoundaries`). Sono de 8 h fixo não era interrompido quando o turno começava |
| 3 | Ninguém abastece a casa | Crianças com carteira 0 morriam de fome em casas sem comida com pais com milhares de moedas | Pressão de compra por estoque/membro (55–130) + lote de ~4 dias + mercado mais próximo **com estoque** (a ferraria, sem comida, era o "mais próximo") |
| 4 | Solidão derruba o dia inteiro | 2/3 dos despertares "imediatos" vinham da necessidade social; um NPC largava o almoço para socializar com solidão 25 | Solidão só força reavaliação quando crítica (≤ 20); fora disso segue a rotina |
| 5 | Produção medida à meia-noite | `Daily` roda na hora 0, quando ninguém trabalha | `TickFrequency.Midshift` |

## Experimento de cadência (a pergunta original do B3)

Variantes de quando um NPC com necessidade urgente é reavaliado (primeira reação ao cruzar o limiar é sempre imediata):

| Variante | Trabalho 9–15 h | Sono 23–4 h | Latência média / p95 (h) | Episódios críticos (≤10) | CPU escala (ms/NPC-dia) |
|---|---|---|---|---|---|
| Base: reavaliar toda hora enquanto urgente | 70% | 65% | 3,8 / 8 | 94 | 0,37 |
| **Solidão não crítica não força despertar (adotada)** | **78%** | **75%** | 3,7 / 8 | **85** | 0,32 (−14%) |
| Reavaliar a cada 2 h | 80% | 76% | 3,7 / 8 | 86 | 0,30 |
| Reavaliar a cada 3 h | 79% | 74% | 3,8 / 8 | 97 | 0,29 (−22%) |
| Reavaliar a cada 4 h | 80% | 76% | 3,8 / 8 | 106 | – |
| Reavaliar a cada 6 h | 81% | 77% | 4,1 / 9 | **169** | – |

Nota: as colunas de comportamento das linhas "a cada K h" são do default sem a regra social; a coluna de CPU dessas linhas (escala) foi medida **com** a regra social adotada somada ao espaçamento K.

Leitura:
- **Reavaliar toda hora não é mais realista; é menos.** A reavaliação horária por carência (principalmente social) desancorava a rotina.
- A variante adotada tira o ruído onde ele não é sobrevivência e **mantém** a reavaliação horária de fome, sede e sono: nenhum atraso a mais para quem está passando mal.
- Espaçar também as necessidades de sobrevivência (K ≥ 4) começa a custar: mais episódios críticos. Rejeitado.
- Ganho de CPU é modesto em escala (−14%): lá o custo é dominado por deslocamento e economia, não por despertar. O argumento da mudança é realismo, não velocidade.

## Alternativas avaliadas e não adotadas
- **Refeições em horário fixo na rotina (almoço 12 h, jantar 19 h).** Medido: o NPC passa a comer com fome 96. `Eat` repõe fome **e** sede por completo e o decaimento (2/h e 3/h) está calibrado para ~1 refeição/dia, então refeição fixa dobra o consumo sem melhorar o comportamento. Exige recalibrar porções/decaimento: decisão de design.
- **Despertar só por evento/limiar (sem ritmo)**: deixaria o dia depender só de necessidades; perde a rotina. Descartado.

## Resíduo conhecido (não corrigido)
- **Fixture de 500 pessoas ainda colapsa a partir do ano 3** (500 → ~265 no ano 4). Não é falta de comida (o estoque da fazenda fica no teto de 50.000): é a economia do fixture. Com oferta 5× acima do consumo o preço fica no piso (1), a receita da fazenda (~24 moedas por pessoa por mês) não cobre a folha de 300 empregados (150 por mês), o caixa chega a zero e os salários passam a ser pagos por ordem de id; casas sem renda passam fome enquanto outras acumulam milhares. Reduzir o salário piora (a renda deixa de sustentar a família). Opções, todas decisão de design: pagamento proporcional quando o caixa é curto; compartilhamento de renda dentro do domicílio/parentela; lavoura de subsistência por domicílio; empregador que ajusta emprego e produção ao que vende.
- Quem come só quando uma necessidade fica urgente: latência média de 3,8 h entre cruzar 30 e comer (p95 8 h) porque `Work` ainda disputa com `Eat` abaixo do limiar crítico.
- No fixture de escala 27% da população está em `Travel` (casas espalhadas a dezenas de células de uma única fazenda); é layout, não motor.
- Os 24–29 óbitos por fome do ano 1 do default (de 100 iniciais) vêm do estado inicial sincronizado (todos começam com as mesmas necessidades).
