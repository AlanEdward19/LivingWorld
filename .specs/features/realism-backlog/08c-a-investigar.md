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
108. ~~**Medida da aflição**~~ (AD-182): ruído em 20 × 100 pareado (braços que quase não diferem): fome crítica 3,36 × 2,83 h por
     pessoa-ano, razão 0,84 (IC90 0,60–1,13), sede 0,989 (0,975–1,003). Diferença de fome abaixo de ±30% em 20 seeds é ruído.
109. ~~fechado no AD-157b~~ (refeição de rotina; voltas para comer 24,5 → 2,3 por pessoa-ano; fome crítica igual à base).
110. **Custo da carga ligada (AD-157b)**: decisão +16–26% a 1.000 NPCs (3 pares alternados; ver o log), contra a meta de 2–3%.
     ~1/3 no código da carga (arrumar ao sair 1,6 µs × 37 mil saídas para longe, a pausa, a hora da refeição) e o resto no
     mundo que muda (menos despertares, decisões urgentes mais caras, mais refeições concluídas, relações em casa). Item 141:
     o relógio de parede oscila ±10%; um critério de 2% pede contador de instruções.
111. ~~**Quem vai ao mercado não marca o recipiente**~~ (AD-182): leva o da casa mesmo sem lanche (`MarketTripRules.HoldsCarrier`).
112. ~~**Números sem fonte do AD-157/157b**~~ (AD-166: duas refeições de época conferem o desenho; limiares sem fonte, confiança baixa, [meal-routine](../../../docs/domain/meal-routine.md)): folga de 12 h ao arrumar a carga; dependente = abaixo de 12 ou 65+ anos na medida;
     hora da refeição em 65 de déficit, comida antes da hora só contra a crítica (80), apetite 40 de quem só foi beber.
113. ~~**Não medido no AD-157b**~~ (AD-182): 5.000 NPCs 1,079 → 1,069 µs, 145 B por NPC-tick; 16,8% das saídas com lanche levam
     peça (740 mil de 4,4 mi); com tira, 50 mil saídas (lanche ou compra; 80 × 15); despensa vazia em casa 24,7 → 20,3 h por pessoa-ano com as correções.
147. ~~**A volta pelo que ficou não guarda quanto ficou**~~ (AD-182): guarda por recurso; só antecipa a ida com um dia da casa.

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
148. ~~**Quebra de colheita pesa mais com a carga**~~ (AD-166). Fim (AD-182, [land-and-harvest](../../../docs/domain/land-and-harvest.md)):
     a quebra da seed 14 não ocorre na base nova; na seed 26 o celeiro racionava certo (acabou 1 mês antes da colheita) e a
     falha era a venda à primeira casa. Venda repartida (2 dias por morador) e semente guardada: 80 × 15 nas carestias, furtos
     0,93× (ns), mortes de fome 0,94× (ns). Socorro em grão da cidade tentado e retirado (2,5× as mortes de fome).
149. ~~**Mortes de fome por seed 0,80 → 1,38**~~ (AD-166; causa e correção no AD-182). Sonda `person-trace`: a carteira não
     zerava (era a herança repartida antes do registro da morte); dois mecanismos: o jovem só com 85–154 moedas conversava
     sozinho (solidão no zero, ~190) contra a compra (160) por 25 dias — o teto do lazer agora vale com a compra que serve; e o
     adolescente lento que regredia na doença abaixo do marco de comprar (0,76 → 0,65) — o marco se consolida pelos anos
     vividos, e moeda de quem não sabe comprar não tira a casa da ajuda. Com × sem carga: 1,41× (IC90 1,04–1,94) → 1,02×
     (0,66–1,45); base → final: mortes de fome 0,54× (0,35–0,76), 15+ 0,43×, mortalidade 0,98× (ns), natalidade 1,01× (ns).
150. **Baseline `population.json` regravado** na junção: média das 20 seeds 89 → 104; só a seed 14 ficava abaixo da faixa.

## Da fatia AD-166
151. ~~**A comida do excedente adia filhos**~~ (AD-182): não era `FirstBirthMeansWeight` (segurou zero concepções); era o piso
     de 5 de comida em casa para conceber (`ConceptionResourceFloor`), que a casa que doava furava. Com o ciclo hormonal o corpo
     decide (amenorreia): excedente ligado × desligado, natalidade 0,988× (0,978–0,998) → 1,001× (0,991–1,012), 80 × 15.
152. ~~**O saber e a carga de toda morte**~~ (AD-182, 20 × 100 pareado com `LW_DEATH_FORGETS=old`): mortalidade 0,981× (IC90
     0,952–1,006), natalidade 0,986×, mortes de fome 0,75×: o +1% dos 40 × 15 não se sustenta. Saber que morre com a pessoa é realista.
153. ~~**A carga do morto fica onde ele morreu**~~ **Fechado no AD-168** ([leavings](../../../docs/domain/leavings.md)): em
     casa fica com a casa; longe, fica no chão para a casa (ou o herdeiro) buscar numa viagem ou para quem passa furtar.

> Ordem do dono (2026-11-17): os itens 149, 151, 152 e a reposição racionada do celeiro na quebra (fim do 148) se resolvem
> nesta trilha, depois da refatoração do cliente Godot.

## Da fatia AD-168
154. **A casa sabe da morte na hora** (o luto já chegava assim): a notícia devia vir de quem estava junto, de quem passa
     pelo lugar ou da vila; hoje a casa sabe do lugar sem ninguém contar (dívida de realismo).
155. ~~**O espólio quase não acontece**~~ (AD-182, sonda `leavings-exercise`): todo espólio da vila cai a < 6 h de casa (24 h
     nunca limita); 35% era furtado por quem passa; com o laço com o morto (`KnownDeadBarrier`), 31%. Longe: issue.
156. ~~**No chão estraga igual à despensa**~~ (AD-182): 3× a despensa, água 2%/dia, Q10 2 com o calor, só os dias no chão.
157. **Quem busca leva um recipiente só**: com o seu já em uso, o do morto vai para casa sem servir (o modelo só segura uma
     peça por pessoa); o veículo de terra da casa não entra na busca. O furtador faminto também não procura espólio como alvo
     (só o acha de passagem).

> Continua em [08d](08d-a-investigar.md) (itens 158–173, traçado da cidade, AD-173).
