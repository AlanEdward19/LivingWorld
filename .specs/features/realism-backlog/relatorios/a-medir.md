# O que só se resolve rodando (AD-161)

> Lista enxuta dos itens `MEDIR` de [08](../08-a-investigar.md)/[08b](../08b-a-investigar.md)/[08c](../08c-a-investigar.md),
> agrupados pela medição que responde vários de uma vez. Ordem: valor para as próximas decisões. Rodar pela fila
> (`lw-um-por-vez.sh`), nunca em paralelo com outra rodada pesada.

## 1. Rodada de referência com medida de fome que aguenta o ruído (itens 86, 85, 62, 80, 34, 30, 32, 14, 22)

Uma rodada 40 seeds × 100 anos da árvore de hoje, **em dois braços pareados** quando há uma chave em julgamento, com estes
contadores por seed e por década: mortes por causa e por faixa de idade; **pessoa-anos** por faixa de idade; nascidos vivos por
sexo; natimortos; construções boas e falhas; CV mínimo de vitalidade; lembranças por tipo. Responde:

- **86, 85, 62, 80**: se fome sobe com a doméstica/sequestro/violência sexual (85), com a carga pessoal ligada (62) e com o açoite e o
  banimento (80), pelo desenho estatístico abaixo, em vez da contagem bruta em 20 seeds.
- **34**: fração de mortes ainda sem causa (era 34,7%), doença de 1 a 4 anos contra a tábua, e se alguma seed ainda cai a dezenas de
  vivos (a seed 18 do AD-140).
- **30**: razão de sexo ao nascer com intervalo (o 107,7 do AD-139 é ruído do sorteio 105:100? com ~11 mil nascidos o erro-padrão
  da razão é ~2 pontos [C], então 107,7 fica a ~1,3 erro-padrão de 105).
- **32**: mortes no 1º ano e a taxa de construções falhas/boas (49 mil/75 mil no AD-141).
- **14**: piso do CV de vitalidade (0,097 contra 0,129) com as uniões de hoje.
- **22**: fração das vagas de lembrança ocupadas por ajuda (era 54%).

### Desenho da medida de fome (item 86)

1. **Unidade: taxa, não contagem.** Mortes de fome por 1.000 pessoas-ano, por braço, também separada em menores de 5 anos e adultos
   e por década. A contagem bruta mistura "mais fome" com "mais gente": a demografia histórica sempre divide pelo tempo em risco
   (pessoas-ano), e a taxa tem intervalo de Poisson por seed.
2. **Pares de seed com números aleatórios comuns.** O mesmo seed nos dois braços (já é assim). O motor sorteia por chave
   (`ctx.OneShot(...)`, `ctx.StreamFor(...)` com id da pessoa e do evento), o que mantém os sorteios alinhados enquanto as
   trajetórias não divergem; depois de uma ou duas décadas o caos desfaz o par. Por isso a análise é **no nível da seed** (a
   diferença ou a razão de taxas de cada par), não no nível do evento.
3. **Intervalo por bootstrap de seeds.** Reamostrar seeds com reposição (o par inteiro, 10.000 reamostras), recalcular a razão das
   taxas somadas (braço B / braço A) e ler o intervalo de 90% por percentis (ou BCa). A seed é o aglomerado: as mortes dentro de
   uma seed não são independentes, então o bootstrap por morte daria intervalo estreito demais.
4. **Critério de aceite por equivalência, não por "≤ base".** Aceitar a fatia se o limite superior do IC 90% da razão ficar abaixo
   de **1,25** (não inferioridade com a margem usual de equivalência 0,80–1,25); recusar se o limite inferior passar de 1,0 com causa
   achada; senão, "inconclusivo" e mais seeds.
5. **Quantas seeds.** Fórmula de pares: n ≈ 2(1 − ρ)(z₀,₉₅ + z₀,₈)² · CV² / δ², com CV a variação entre seeds da taxa de fome, ρ a
   correlação entre os braços do mesmo seed e δ o efeito relativo a detectar. Os números de hoje sugerem CV alto (blocos de 20 seeds
   variando ~±20% em direções opostas): **hipótese** a confirmar, CV ≈ 0,9 e ρ ≈ 0,3 dariam ~140 pares para δ = 25% e ~900 para
   δ = 10%. Portanto: (a) rodar um piloto de 40 pares para estimar CV e ρ; (b) seguir em blocos de 20 até a meia-largura do IC
   ficar abaixo de 0,25 ou chegar a 200 pares; (c) aceitar que 20–40 seeds **não** detectam mudanças de 10% na fome, e usar a
   medida por década e por idade (onde a fome se concentra) para ganhar poder. Referências do método [M]: Law & Kelton,
   *Simulation Modeling and Analysis* (números aleatórios comuns, réplicas); Lorscheid, Heine & Meyer 2012, *Comput Math Organ
   Theory* (número de réplicas pela estabilização do CV); Lee et al. 2015, *JASSS* 18(4):4 (análise de saída de modelos de agentes).

## 2. Custo por NPC-tick em pares alternados, fila de uma vaga (itens 4, 24, 18, 32, 43, 44, 52, 61, 63, 65, 72, 79, 106)

Uma sessão de custo: árvore de hoje contra a base antes do AD-145, e cada chave suspeita ligada/desligada, a **1.000 e 5.000 NPCs**,
em pares alternados (A, B, A, B…), mínimo de 3 pares, mesma janela de máquina. Responde o custo nunca medido a 5.000 dos AD-145,
148, 149, 151, 152, 153 (43, 52, 61, 65, 72, 79), a grade de água do AD-143 depois do conserto (44), a variação da carga ligada
(63), o par do AD-134 (`ee6f0416` × `bce8e56c`, item 4), a oscilação de bytes da mesma DLL (24: medir a mesma DLL 5× seguidas),
o custo da invenção a 1.000 (32) e o `PowerOpportunityProvider` com poderes ligados (106: ele varre todos os portadores por NPC).
O **18** (`RuntimeHistoryCostTests` 1,045 no gate, 1,00–1,02 sozinho) sai da mesma sessão: rodar o teste 10× sozinho e 10× com
o gate em paralelo e ver se o teto de 1,03 mede o código ou a carga da máquina.

## 3. Experimentos de causa, cada um com uma chave (itens 2, 8, 77)

- **2** (traço neutro): braço real com a escolha de parceiro **sem** o fator saúde; se a diferença −0,009 some, é carona genética
  legítima e o teste deve medi-la; se não some, rodar mais seeds (pelo bootstrap do grupo 1) antes de decidir.
- **8** (pressão dos pais): cenário com mais ofícios com vaga paga, pressão alta × baixa; o 78% × 69% de hoje pode ser só o mercado
  de trabalho da vila (quase todos lavradores).
- **77** (casa volta pela carga que ficou): traço `provision-trace` contando compras cortadas e a compra seguinte da mesma casa; o
  código só garante a frustração (`PersonalCarry.FeltCargoCut`), não a volta.
