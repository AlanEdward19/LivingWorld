# A investigar (continuação de [08b](08b-a-investigar.md), mesma numeração)

> AD-161 (2026-11-16): todos os itens desta página foram conferidos em fonte
> ([família](relatorios/conferencia-familia-e-nupcialidade.md), [economia](relatorios/conferencia-economia-e-credito.md)).

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
108. **Medida da aflição** (`aflicao seed=` no `demography-report`): pareie a mesma seed, 40 × 15 (~6 min). Ruído medido uma vez
     (sequestro desligado); falta o ruído em 20 × 100 e o limiar "crítico" 80 é escolha (a sede passa disso ~6% do tempo na base).
109. **Fome do caminho com a carga ligada**: 34 mil voltas para comer e 48 mil refeições da oficina por seed em 15 anos; a fome
     crítica triplica (10,9 → 35,3 h/pa), metade dela em casa (quem chega com fome e come no fim da refeição). Medir de onde saem
     as voltas (sair de lugar que não é a casa não arruma carga; despensa no limite da reserva) antes de ligar.
110. **Custo da carga ligada** ainda +14–35% na decisão: as decisões urgentes de quem está com fome custam mais (mesmo número);
     sair (`PackOnDeparture`) refaz o destino e dois trajetos. Fechar com 109 (menos fome, menos decisão urgente).
111. **Quem vai ao mercado não marca o recipiente** quando não arruma carga (perto de casa): outro pode sair com ele no meio da compra.
112. **Números sem fonte do AD-157**: folga de 12 h ao arrumar a carga; dependente = abaixo de 12 ou 65+ anos na medida.
113. **Não medido no AD-157**: 20 × 100 com ela ligada e a medida nova; custo a 5.000 NPCs; o ciclo do recipiente com tira levado
     (12b/13a) com os consertos (a 40 × 15 o recipiente já é levado e bebido: 36 mil bebidas e 10 mil refeições do que levavam por seed).

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

