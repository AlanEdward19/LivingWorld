# A investigar (continuação de [08b](08b-a-investigar.md), mesma numeração)

> AD-161 (2026-11-16): todos os itens desta página foram conferidos em fonte
> ([família](relatorios/conferencia-familia-e-nupcialidade.md), [economia](relatorios/conferencia-economia-e-credito.md)).
> AD-164: 108–113 são da fatia da carga pessoal e 140–146 do cérebro (outras fatias em andamento); não tocados.

## Da natalidade e nupcialidade de época (AD-158)
120. ~~**A mulher ainda casa cedo (~21) e quase nenhuma fica solteira**~~ (AD-161): a referência confere (mulher 24–25, homem 27–28;
     10–20% de celibato na Inglaterra, 20–30% em Hajnal; diferença de idade ~3 anos); o resultado do modelo fica fora. O que falta
     (escolha do lado escasso, criadagem das moças) é desenho: issue #173 (criados fora de casa) e linha de correções.
121. ~~**TFR conjugal 20–49 ~8–9**~~ (AD-161): fora da faixa — 26 paróquias inglesas dão TMFR ≈ 6,7 (Alter, Newton & Oeppen 2020);
     intervalo médio 29–30 meses confere. Medir a decomposição por idade antes de mexer (correções). **AD-162:** amenorreia 480 d: TMFR padronizada 7,8→7,0 (a medida bruta de cima dava 9).
122. ~~**Posse para casar só vê terra da fazenda comum**~~ issue #171 (posse de casa e terra); JSON na #162.
123. ~~**Concepção pré-nupcial subiu**~~ (AD-161): 39% confere no teto da faixa (20–40%, Campop 2024); nascidos fora de união 2,2%
     confere (< 5%).

## Do crédito de abertura (AD-159, [credit](../../../docs/domain/credit.md))
130. ~~**Carência de 6 meses na abertura**~~ (AD-161): dentro do prazo de até um ano dos casos de Briggs; confere.
131. ~~**Prazo da abertura até 60 meses**~~ (AD-161): fora da faixa — crédito de aldeia de um dia a um ano (Briggs 2009, p. 71);
     sugerido ≤ 24 com garantia (correções). **AD-162:** máximo 24.
132. ~~**O salário do próprio fundador conta inteiro**~~ (AD-161): sem fonte; escolha de modelo.

## Da carga pessoal (AD-157, [household-provisioning](../../../docs/domain/household-provisioning.md))
108. **Medida da aflição** (`aflicao seed=` no `demography-report`): pareie a mesma seed, 40 × 15 (~6 min). Ruído medido com o
     cérebro ligado (sequestro desligado): fome ±2,2 de 10,3. Falta o ruído em 20 × 100; o limiar "crítico" 80 é escolha.
109. ~~fechado no AD-157b~~ (refeição de rotina; voltas para comer 24,5 → 2,3 por pessoa-ano; fome crítica igual à base).
110. **Custo da carga ligada (AD-157b)**: decisão +16–26% a 1.000 NPCs (3 pares alternados; ver o log), contra a meta de 2–3%.
     ~1/3 no código da carga (arrumar ao sair 1,6 µs × 37 mil saídas para longe, a pausa, a hora da refeição) e o resto no
     mundo que muda (menos despertares, decisões urgentes mais caras, mais refeições concluídas, relações em casa). Item 141:
     o relógio de parede oscila ±10%; um critério de 2% pede contador de instruções.
111. **Quem vai ao mercado não marca o recipiente** quando não arruma carga (perto de casa): outro pode sair com ele no meio da compra.
112. ~~**Números sem fonte do AD-157/157b**~~ (AD-166: duas refeições de época conferem o desenho; limiares sem fonte, confiança baixa, [meal-routine](../../../docs/domain/meal-routine.md)): folga de 12 h ao arrumar a carga; dependente = abaixo de 12 ou 65+ anos na medida;
     hora da refeição em 65 de déficit, comida antes da hora só contra a crítica (80), apetite 40 de quem só foi beber.
113. **Não medido no AD-157b**: custo a 5.000 NPCs; quantas saídas levam a peça com tira (só se conta a peça existir); a despensa
     vazia em casa sobe (20 → 29 h por pessoa-ano em 40 × 15): comer em hora certa gasta mais, e as casas pobres sentem.
147. **A volta pelo que ficou na banca (08b 77, AD-157b)** é por casa e não guarda quanto ficou: a ida seguinte compra pela meta.

## Do cérebro neural (AD-160, [verdict](../neural-brain/verdict-ad160.md))
140. **O corpo decide com fome ou sede ≤ 20** (`BrainRules.CriticalNeed`): reaproveita o déficit crítico 80 que já põe o corpo
     à frente do lazer (AD-147); sem fonte empírica de onde o hábito deixa de pesar diante da necessidade.
141. **Ruído das medidas do cérebro**: variedade de dias com 6 seeds oscila ±0,03 entre corridas quase iguais; o tempo por
     NPC-tick no `MeasureHarness` oscila ±10% na máquina compartilhada (mesmo código desligado: 0,889 e 0,998 µs a 5.000). Um
     critério de +2% de CPU pede contador de instruções ou máquina parada, não relógio de parede.
142. **Participação guardada com 1/127 de resolução** (byte por grupo): escolha de engenharia para bytes; o efeito no
     aprendizado (arredondamento) não foi medido além da demografia de 6 seeds.

## Do cérebro ligado (AD-160b, [verdict](../neural-brain/verdict-ad160b.md))
143. **Valor do estado**: 20 pontos por dia de comida e água na despensa, 10 por dia de comida em moedas, até 4 dias; salário
     ÷ 270 h por hora trabalhada. Escolhidos para a despensa valer mais que a moeda (é preciso ir comprar); sem fonte.
144. **Corpo decide com fome ou sede ≤ 30** (o limiar de urgência) e o tempo livre só desvia da rotina com ≥ 50: sem fonte.
145. **Degraus** (ganho 20‰, expectativa 16 pontos) e **aprendizado 0,05**: escolhidos por bytes de snapshot, não por
     dado de aprendizagem humana.
146. **Passeio à toa anda mais que conversar** (um passo por ação concluída; à toa dura 2 h, conversar 3 h): artefato do
     modelo de movimento que levava quem trocava conversar por à toa para longe da água. Investigar no movimento, não no cérebro.


## Da junção da carga ligada (AD-157b + AD-165)
148. ~~**Quebra de colheita pesa mais com a carga**~~ (AD-166): a moeda das casas é a mesma (577 mil nos dois braços no mês 8
     do ano 3); a carga come 2,7% mais por pessoa (refeição em hora certa: 20,7 contra 20,2 unidades por pessoa-mês) e a banca
     esvazia um mês antes, com a despensa a 148 contra 317 unidades. A ajuda era cega nos dois braços: 100% das casas carentes
     tinham os 4 dias de compra pelo preço-piso (8 moedas por pessoa) com o trigo a 20. Feito: moeda só conta com comida à venda
     ao alcance e comida do excedente (2 dias) de parente ou afeto. Seed 14: furtos 448 → 410 (sem carga 394), banidos 35 → 27
     (28), vivos 55 → 63 (64). O celeiro tem 15 mil unidades na quebra e a banca fica vazia: a reposição diária racionada é o
     gargalo (não tocado).
149. ~~**Mortes de fome por seed 0,80 → 1,38**~~ (AD-166): real. 80 pares × 15 anos: 1,63× (IC90 1,28–2,18), 15+ anos 1,82×;
     horas de fome crítica iguais (1,01×) e mortalidade igual (1,003×). Quem morre a mais (`starvation-trace`, 88 contra 55):
     jovem de 15–29 anos (39 contra 22), **morando só** (61 contra 27), empregado (49 contra 23), órfão de mãe, carteira e
     despensa zeradas, em casa, conversando (39 contra 14), com comida na banca. A caderneta do empregador não é a causa
     (`LW_AG=freeration`: 0,93×, IC90 0,80–1,08). **Aberto**: seguir pessoa a pessoa o jovem que fica só (salário de 90 por
     mês e esmola deviam pagar a compra: por que a carteira chega a zero e por que conversar vence comprar).
150. **Baseline `population.json` regravado** na junção: média das 20 seeds 89 → 104; só a seed 14 ficava abaixo da faixa.

## Da fatia AD-166
151. **A comida do excedente adia filhos**: natalidade 0,979× (IC90 0,966–0,992, 40 × 15; 6 × 50 0,947×). Com reserva de
     3 dias a natalidade fica igual (0,992×) mas a quebra da seed 14 piora (453 furtos). Investigar se o adiamento do primeiro
     filho (`FirstBirthMeansWeight`) deve ler a despensa de quem acabou de dar comida.
152. **O saber e a carga de toda morte** (AD-166) mudam a mortalidade 1,010× em 40 × 15 (IC90 1,002–1,020) e 1,002× em
     6 × 50: o morto deixava de esquecer designs; os desenhos agora viram lápide mais cedo. Aplicado por correção; medir de novo
     em 20 × 100.
153. **A carga do morto fica onde ele morreu** (pedido do dono, 2026-11-17): hoje `PersonalCarry.Forget` leva a carga à
     despensa da casa na hora, esteja o morto onde estiver (ou a dá por consumida se não há casa). Pedido: morreu em casa, fica
     com a casa; morreu longe, a carga e o recipiente ficam no local, para alguém da casa ir buscar ou para ser furtada por
     quem passa. Fatia AD-168, depois do bloco de desempenho (muda o mundo; a conservação tem de fechar com a carga no chão).
