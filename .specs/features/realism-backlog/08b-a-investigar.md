# A investigar (continuação de [08](08-a-investigar.md), mesma numeração)

## Da sonda de terra (AD-153, [invention-land](../../../docs/domain/invention-land.md))
73. **Força humana sustentada puxando: 250 N** (lembrança de ergonomia de tração por horas, ~200–300 N; pico bem acima). Precisa
    de conferência.
74. **Atritos**: arrastar no chão 0,55 (madeira ou couro sobre terra, faixa 0,4–0,6); patim 0,30 menos até 0,10 pelo acabamento
    liso (madeira sobre grama ou terra batida ~0,2–0,35; neve bem menos). Precisa de conferência.
75. **Rolamento**: braço de 15 mm (Crr ≈ 0,05 com roda de 0,3 m em terra batida; faixa 0,03–0,1); eixo de madeira engraxada no
    cubo 0,15 (0,1–0,2). Precisa de conferência.
76. **Carga a granel**: pilha de 0,5 m sobre o tabuleiro, 800 kg/m³ (grão ~750, água 1.000). Patim = 4× mais comprido que largo;
    tomba se o contato cobre menos da metade da largura. Escolhas sem fonte.
77. **Uma ida e volta inteira por carga que ficou** (antes a fração da viagem): muda também o recipiente (mais frustração com
    água no caminho). Conferir se a casa de fato volta (a reposição dispara de novo, AD-149).
78. **Corte por terra mais o carregador de 30 kg** (`FootCarryKg`, AD-143): a compra de água (2 kg a unidade) é quem mais corta;
    conferir quanto uma pessoa traz do mercado a pé por viagem numa aldeia (balde, odre, cesto à cabeça: 20–40 kg?).
79. **Não medido no AD-153**: goldens, pins de mundo e baselines do LongRunning; `verify --full`; custo a 5.000 NPCs.

## Da violência e justiça (AD-150)
80. **AD-150, população**: 20×100 final 280 → 260, menor seed 141 → 114; parte do açoite e dos 0,43 banimentos/ano não isolada seed a seed. CPU total com a fila livre não medida (A/B crime ligado/desligado dentro do ruído). Teste do poço fraco roda sem furto e crime (com eles o sinal de preço cai de 18 para 10 de 20 seeds; causa provável, furto de água na banca, não confirmada evento a evento).

## Do crime contra a pessoa (AD-154, [crime-against-persons](../../../docs/domain/crime-against-persons.md))
81. **Violência doméstica sem fonte firme.** `DomesticViolenceRules`: taxa 0,012/ano (propensão 1), estresse (fome 1,5,
    miséria 1,0, desemprego 0,5), ciúme 2,0, histórico 1,5, dano 0,6× o da rua, golpe mortal 0,4%, mágoa conjugal +20,
    denúncia 0,15× e condenação 0,5× da comum. Referência lembrada: OMS (2018) ~13% das mulheres com parceiro sofrem
    violência física ou sexual do parceiro em 12 meses (precisa de conferência); a norma medieval tolerava o "castigo
    moderado" da mulher e dos filhos (precisa de conferência). Conferir também a fração de homicídios dentro da família na
    Inglaterra do séc. XIII (Given 1977, ~1/5? precisa de conferência).
82. **Sequestro sem taxa histórica.** `KidnappingRules`: taxa 0,002/ano, resgate metade da moeda da família (teto 400),
    busca 2%/dia por parente (4× se desconfia), fuga 3%/dia, prazo 21 dias, morte no fim 5% (3× na vingança). Tudo
    estimado; não lembro de taxa medieval. Comparar com casos de rapto em rolos de corte inglesa e com sequestro moderno
    (poucos por 100 mil/ano, precisa de conferência).
83. **Violência sexual sem fonte firme.** `SexualViolenceRules`: taxa 0,004/ano, peso do mesmo sexo 0,1, revelação 0,3 por
    parente, denúncia 0,3× e condenação 0,5× da comum, forca 15%, banimento 40%. Lembrados: Holmes et al. (1996), gravidez
    em ~5% dos estupros de mulheres de 12 a 45 anos (base da fração 0,25 do ciclo; precisa de conferência); Hanawalt
    (1979), estupro ~1% das acusações de crime grave, quase sem condenação (precisa de conferência). A taxa medida fica
    na ordem do estupro registrado pela polícia na Europa de hoje (dezenas por 100 mil/ano, precisa de conferência).
84. **Separação a mais pela doméstica.** A primeira medição (6 seeds, taxa 0,02 e mágoa +30) teve ~36% mais separações e
    natalidade 5% menor (a casada separada para de conceber pelo ciclo). Recalibrado para 0,012 e +20; conferir se o
    efeito na natalidade tem paralelo em fonte e se a separação por violência era possível na norma da época (a igreja
    concedia separação "de mesa e leito" por crueldade, precisa de conferência).
85. **Fome acima da base nos dois blocos: efeito difuso, sem chave que o concentre.** Mortes de fome 309 → 339 (seeds 1–20)
    e 325 → 362 (21–40). Com uma chave nula por vez (seeds 1–20, mesmo código do topo, `LW_CRIME_OFF`): sem doméstica 394,
    sem sequestro 462, sem violência sexual 336; todas ≥ o topo. O sequestro (0,02/seed-ano, nenhum preso morreu no
    cativeiro) não pode causar 120 mortes: é a divergência caótica das trajetórias, maior que a diferença base × topo.
    Nenhum mecanismo achado (o medo só pesa em `Travel`, não em Comprar). Falta: mais seeds por configuração para
    estreitar o ruído.

## Da medição (2026-11-15)
86. ~~medida criada no AD-157~~ (`NeedDistressTally`, item 108). **O critério "fome ≤ base em 20 seeds" não discrimina**: nas mesmas seeds 1–20, desligar um crime raro (sequestro, 0,02 por seed-ano) move as mortes de fome de 339 para 462; o ruído de trajetória passa de ±20%. Falta uma medida que aguente (mais seeds, pares com trajetória comum, ou taxa por pessoa-ano com intervalo) antes de aceitar ou recusar fatia por fome. AD-153 (+6% em 40 seeds) e AD-154 (+10,6%) foram aceitos sem causa isolada.

## Do crédito e da falência (AD-155, [credit](../../../docs/domain/credit.md))
90. **Usura**: acréscimo de 10% ao ano do prazo, multa de 5% da parcela atrasada, teto de 20% ao ano; parente a 0%. Lembrança, **precisa
    de conferência**: Briggs, *Credit and Village Society in Fourteenth-Century England* (2009); Latrão III (1179), Lyon II (1274);
    teto dos judeus ingleses (2 d por libra por semana, ~43%/ano); montes de piedade do século XV (~5–10%).
91. **Prazos** de 36 meses (abertura) e 12 (giro, reabertura, comida); **6 meses** de atraso até o calote; **10 anos** de memória
    do calote; **6 meses** de folha que não fecha até a falência. Sem fonte.
92. **Quanto se financia**: até metade do capital (× `1,5 − Aversão/100`), parcela até metade do ganho prometido (ou do salário,
    na comida). Sem fonte; conferir prática de *commenda* e de crédito rural (penhor de colheita).
93. **Confiança**: mínimo 40, +40 parente, +40 garantia de oficina, + eixo Dívida do credor; má fama acima de 30 fecha a porta.
94. **Tesouro empresta até 25%** do que tem; **3 meses** de folha no empréstimo de giro; **7 dias** de comida por pessoa como
    gatilho do empréstimo para comer; penhora deixa **1 mês** do menor salário. Sem fonte.
95. **Ordem dos credores na falência** (salário atrasado antes dos empréstimos com garantia): é a ordem moderna; a medieval
    variava por costume local. **Precisa de conferência.**
96. **Valor do prédio tomado em pagamento** = foro + obra + equipamento da abertura (custo de reposição, sem desconto de leilão).

## Dos poderes por genética (AD-156, [powers-genetics](../../../docs/domain/powers-genetics.md))
100. **Penetrância padrão 0,9** e **expressividade** com desvio 0,15, +0,25 no homozigoto dominante e +0,25 por ponto acima do
     limiar no poligênico. Escolhas sem fonte (doenças mendelianas reais vão de ~0,3 a 1). Precisa de conferência.
101. **Idade de início N(13; 1,5) anos** (lembrança da idade da puberdade). Precisa de conferência.
102. **Mutação nova 1e-5 por locus por gameta** (o relatório citava 1e-5 a 1e-4; taxas por gene em humanos lembradas entre 1e-6
     e 1e-5). Precisa de conferência.
103. **Custo do poder manifesto**: fome e sede ×1,1, fertilidade ×0,9, vitalidade ×1,0. Escolhas sem fonte (o poder é fictício):
     calibrar pela seleção que se quer ver.
104. **Poligênico**: 4 loci, q = 0,25 nos fundadores, limiar 5 cópias, desvio do ambiente 0,5. Escolhas sem fonte.
105. **Prevalência do cenário de medida 8%** (2% por poder). Escolha sem fonte.
106. **Feito no AD-156 (índice de vivos, fontes de flora por hora, salto adiado; mesmo hash). Sobra: a fauna e o `PowerOpportunityProvider` ainda olham portadores por NPC; não medidos.** Mundo com poderes ligados ~10× mais lento (achado no AD-156, causa da base). Perfil a 1.000 NPCs, 720 h, poderes
     ligados: `flora-lifecycle` 33% do tempo e 82% da alocação (1,6 GB). Com `Extraordinary.Enabled` o sistema desliga o
     salto lazy (`TrySkip`/`Rebuild`) e chama `FloraMechanic.GrowthRateMultiplier` por planta por hora, que percorre todos os
     portadores (mortos inclusive, que nunca saem de `ExtraordinaryCarriers`) com LINQ e `OrderBy`. As varreduras horárias de
     `ExtraordinaryStateSystem`, `ExtraordinaryPowerStageSystem` e da fauna também passam pelos mortos. Conserto: índice dos
     portadores vivos com efeito de flora/fauna, recalculado só quando um portador muda, e o salto lazy mantido quando nenhum
     poder vivo mexe na flora. O AD-156 só trocou o `UpsertExtraordinaryCarrier` (antes remover + ordenar a lista inteira).
107. **Equivalência do conserto dos poderes ligados (AD-156) foi prova de uma vez só**: o teste `PowerCostEquivalenceTests` comparava o hash com o do código antigo (já apagado), nas seeds 1 e 2, e passou antes da junção com o crédito (AD-155). Como qualquer fatia que muda o mundo troca esse hash e o caminho antigo não existe mais para regravar, o teste saiu na junção (custava 8–16 min no gate). Ficam `LivingCarrierIndexTests` e o golden de poderes desligados; falta um teste barato que compare o índice de vivos com a varredura completa no mesmo mundo.

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
