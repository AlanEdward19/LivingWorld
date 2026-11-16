# A investigar (continuação de [08](08-a-investigar.md), mesma numeração)

> AD-161 (2026-11-16): conferência e classificação em [classificação](relatorios/classificacao-a-investigar.md); abertos ficam só
> `MEDIR` ([a-medir](relatorios/a-medir.md)) e o que não deu para conferir.

## Da sonda de terra (AD-153, [invention-land](../../../docs/domain/invention-land.md))
73. **Força humana sustentada puxando: 250 N** (lembrança de ergonomia de tração por horas, ~200–300 N; pico bem acima). Precisa
    de conferência. (AD-161: não conferido — a tabela de Snook & Ciriello 1991 não abriu; lembrança de ~150–200 N para horas.)
74. ~~**Atritos**~~ (AD-161, [invenção](relatorios/conferencia-invencao-e-fisica.md)): arrastar 0,55 confere; patim 0,10 só vale em neve (piso ~0,2 em terra, em correções).
75. ~~**Rolamento**~~ (AD-161): Crr ≈ 0,05 confere (solo médio duro 0,04–0,08); eixo 0,15 sem fonte direta, plausível.
76. ~~**Carga a granel**~~ (AD-161): 800 kg/m³ confere (trigo ~750–800); pilha, patim e tombo são escolha de modelo.
77. **Uma ida e volta inteira por carga que ficou**: o código só põe a ida e volta na frustração (`PersonalCarry.FeltCargoCut`, AD-161);
    medir se a casa de fato volta (a reposição dispara de novo, AD-149).
78. ~~**Carregador de 30 kg**~~ (AD-161): fora da faixa — água na cabeça ~20 kg (Limpopo, UEA); sugerido 20 em correções.
79. **Custo do AD-153 a 5.000 NPCs** não medido; goldens, pins e baselines feitos na junção (AD-161).

## Da violência e justiça (AD-150)
80. **AD-150, população**: 20×100 final 280 → 260, menor seed 141 → 114; parte do açoite e dos 0,43 banimentos/ano não isolada seed a seed. CPU total com a fila livre não medida (A/B crime ligado/desligado dentro do ruído). Teste do poço fraco roda sem furto e crime (com eles o sinal de preço cai de 18 para 10 de 20 seeds; causa provável, furto de água na banca, não confirmada evento a evento).

## Do crime contra a pessoa (AD-154, [crime-against-persons](../../../docs/domain/crime-against-persons.md))
81. ~~**Violência doméstica sem fonte firme**~~ (AD-161, [crime](relatorios/conferencia-crime-e-justica.md)): OMS 13%/ano confere como
    referência; a taxa do código é outra grandeza — medir a fração de esposas agredidas antes de calibrar; Given (1/5?) não conferido.
82. ~~**Sequestro sem taxa histórica**~~ (AD-161): nenhuma taxa achada; escolha de modelo (o *raptus* das fontes é mais rapto para casar, #164).
83. ~~**Violência sexual sem fonte firme**~~ (AD-161): gravidez de 5% por estupro confere (Holmes 1996); Hanawalt não conferido; resto escolha.
84. ~~**Separação a mais pela doméstica**~~ (AD-161): separação "de mesa e leito" por crueldade existia, mas rara e com prova de
    violência excessiva; deixar rara (correções). Efeito na natalidade sem fonte.
85. **Fome acima da base nos dois blocos: efeito difuso, sem chave que o concentre.** Mortes de fome 309 → 339 (seeds 1–20)
    e 325 → 362 (21–40). Com uma chave nula por vez (seeds 1–20, mesmo código do topo, `LW_CRIME_OFF`): sem doméstica 394,
    sem sequestro 462, sem violência sexual 336; todas ≥ o topo. O sequestro (0,02/seed-ano, nenhum preso morreu no
    cativeiro) não pode causar 120 mortes: é a divergência caótica das trajetórias, maior que a diferença base × topo.
    Nenhum mecanismo achado (o medo só pesa em `Travel`, não em Comprar). Falta: mais seeds por configuração para
    estreitar o ruído.

## Da medição (2026-11-15)
86. **O critério "fome ≤ base em 20 seeds" não discrimina**: nas mesmas seeds 1–20, desligar um crime raro (sequestro, 0,02 por seed-ano) move as mortes de fome de 339 para 462; o ruído de trajetória passa de ±20%. Falta uma medida que aguente (mais seeds, pares com trajetória comum, ou taxa por pessoa-ano com intervalo) antes de aceitar ou recusar fatia por fome. AD-153 (+6% em 40 seeds) e AD-154 (+10,6%) foram aceitos sem causa isolada. (AD-161: desenho proposto em [a-medir](relatorios/a-medir.md): taxa por 1.000 pessoas-ano, pares de seed, bootstrap de seeds, aceite por não inferioridade com margem 1,25, piloto de 40 pares para dimensionar.)

## Do crédito e da falência (AD-155, [credit](../../../docs/domain/credit.md))
90. ~~**Usura**~~ (AD-161, [economia](relatorios/conferencia-economia-e-credito.md)): 10%/ano e teto de 20% conferem (montes ~5%, judeus 43,3%, penhoristas 20–30%).
91. ~~**Prazos**~~ (AD-161): 12 meses confere (Briggs: um dia a um ano); 36 meses fora da faixa (sugerido ≤ 12); calote, memória e falência escolha.
92. ~~**Quanto se financia**~~ (AD-161): sem fonte para empréstimo; a *commenda* (sociedade) financiava ⅔ a 100%. Escolha de modelo.
93. ~~**Confiança**~~ (AD-161): escolha de modelo.
94. ~~**Tesouro empresta até 25%**, giro, gatilho e penhora~~ (AD-161): escolha de modelo.
95. ~~**Ordem dos credores na falência**~~ (AD-161): fora da faixa, anacrônica; 1543 trouxe o rateio *pari passu*; sugerido penhor + rateio (correções).
96. ~~**Valor do prédio tomado em pagamento**~~ (AD-161): sem fonte; escolha de modelo.

## Dos poderes por genética (AD-156, [powers-genetics](../../../docs/domain/powers-genetics.md))
100. ~~**Penetrância 0,9**~~ (AD-161, [biologia](relatorios/conferencia-biologia-e-genetica.md)): dentro da faixa real; expressividade escolha.
101. ~~**Idade de início N(13; 1,5)**~~ (AD-161): fora da faixa do próprio mundo (puberdade 15/15,5); atrelar ao `PubertyTick` (correções).
102. ~~**Mutação 1e-5 por locus por gameta**~~ (AD-161): confere (genes de doença 10⁻⁶ a 10⁻⁴; acondroplasia ~10⁻⁵).
103. ~~**Custo do poder manifesto**~~ (AD-161): poder fictício, escolha de modelo.
104. ~~**Poligênico**~~ (AD-161): forma de limiar (Falconer) confere; valores escolha de modelo.
105. ~~**Prevalência do cenário de medida 8%**~~ (AD-161): escolha de modelo.
106. **Feito no AD-156 (índice de vivos, fontes de flora por hora, salto adiado; mesmo hash). Sobra: o `PowerOpportunityProvider`
     ainda varre todos os portadores, mortos inclusive, por NPC** (`PowerOpportunityProvider.cs:34`; a fauna já não lê portadores,
     AD-161). Medir o custo com poderes ligados ([a-medir](relatorios/a-medir.md) grupo 2). Achado original: mundo com poderes ligados
     ~10× mais lento, `flora-lifecycle` 33% do tempo e 82% da alocação a 1.000 NPCs, 720 h.
107. ~~**Equivalência do conserto dos poderes ligados**~~ confirmado (AD-161): `LivingCarrierIndexTests` não compara com a varredura
     completa; o teste barato está em correções.

Itens 120 em diante: [08c-a-investigar.md](08c-a-investigar.md).
