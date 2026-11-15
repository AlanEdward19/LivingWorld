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
86. **O critério "fome ≤ base em 20 seeds" não discrimina**: nas mesmas seeds 1–20, desligar um crime raro (sequestro, 0,02 por seed-ano) move as mortes de fome de 339 para 462; o ruído de trajetória passa de ±20%. Falta uma medida que aguente (mais seeds, pares com trajetória comum, ou taxa por pessoa-ano com intervalo) antes de aceitar ou recusar fatia por fome. AD-153 (+6% em 40 seeds) e AD-154 (+10,6%) foram aceitos sem causa isolada.

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
