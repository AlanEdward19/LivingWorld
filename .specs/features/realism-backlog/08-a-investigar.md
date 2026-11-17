# A investigar: pontos em aberto deixados pelas fatias já juntadas

> Coisas que não são fatia nova: dúvidas, afrouxamentos e números sem fonte que o dono pediu para não perder.
> Cada item diz o que se sabe, o que é hipótese e como investigar. Riscar quando resolvido, com o commit.
> **AD-161 (2026-11-16)**: itens classificados ([classificação](relatorios/classificacao-a-investigar.md), com os relatórios de fonte),
> código em [respostas](relatorios/respostas-de-codigo.md), fora da faixa em [correções](relatorios/correcoes-sugeridas.md), o resto
> em [a-medir](relatorios/a-medir.md). Abertos ficam só `MEDIR` e o que não deu para conferir.

## Da fatia de corpo, saúde e amamentação (AD-134)
1. ~~**Quase todas as mortes sem causa explícita**~~ resolvido: doença com causa no AD-137, fração medida no AD-140 (sobra no 34).
2. ~~**Teste de traço neutro afrouxado**~~ (AD-163: 40 seeds +0,0051 ± 0,0048, sem a saúde igual). **AD-164:** exigência do IC pareado de volta em `Unselected_trait_stays_in_the_neutral_band`; nas seeds do teste (1–20 × 10 anos, `trait-drift`) −0,0098 ± 0,0067 (1,5 EP, dentro de 1,96), sem a saúde −0,0074 ± 0,0057. Roda no `--full`.
3. ~~**Dois números sem fonte**~~ conferidos (AD-161, [família](relatorios/conferencia-familia-e-nupcialidade.md)): natimorto de 30% com a mãe morta confere (RR 9,48, Saleem 2014); desgaste 12× é escolha de modelo.
4. **Tempo por NPC-tick não comparável.** O "antes" e o "depois" da fatia foram medidos com cargas diferentes na máquina; só a alocação é confiável (141 bytes, igual). Medir pela fila de uma vaga, em pares alternados, o tick a 1.000 e 5.000 NPCs no commit anterior à fatia (`ee6f0416`) e no posterior (`bce8e56c`).
5. ~~**Demografia saiu da referência calibrada**~~ resolvido no AD-148 (e0 31,0, 1º ano 205‰, dentro dos alvos).
6. **Sobrevivência do bebê sem mãe.** Razão medida de 0,39 (30% contra 76%), abaixo do alvo de 0,5 da auditoria. Conferir em fonte (Knodel e outros sobre órfãos de mãe) e recalibrar. (AD-161: não conferido — os estudos confirmam a direção, nenhum número lido; o alvo 0,5 também não tem fonte. AD-164: Estônia e Knodel seguem ilegíveis; Espanha rural ~40% dos órfãos de mãe no 1º ano chegam aos 15 [B], sem braço com mãe.)

## Da fatia de vocação e família (AD-136)
7. ~~**População subiu 17% sem explicação**~~ superado: causa do crescimento medida no AD-149 (terra sem fim, emigração presa) e nupcialidade no AD-158.
8. ~~**Pressão dos pais pesa pouco na medida.**~~ AD-163 (40×100): seguem o ofício da casa 95,8%, e **94,3% seguiriam por acaso** (parte dos adultos com o mesmo ofício); pressão 0,7 → 0,15 (10 seeds pareadas): 96,1 → 96,3% (razão 1,002, IC90 0,996–1,009). É o mercado da vila (quase todos lavradores). Ver a pressão pede um cenário com mais ofícios pagos (não montado).
9. ~~**Irmãos criados separados seguem raros**~~ issue #173 (criados fora de casa).
10. ~~**Paternidade fora do casamento em 0,2%**~~ conferido (AD-161): fora da faixa, alvo ~1% (Larmuseau 2019); em [correções](relatorios/correcoes-sugeridas.md). **AD-162:** descoberta mais lenta: 0,4→0,6% em 20×100 (mais casos dava 0,9% mas derrubava a população).
11. ~~**Taxas sem fonte**~~ (AD-161): sem taxa histórica de caso, descoberta ou briga; escolha de modelo, calibrar pela paternidade do 10.
12. ~~**Sem "pai social"**~~ issue #173.
13. ~~**Sem penalidade de vitalidade por endogamia**~~ issue #173.
14. ~~**Piso de variância de vitalidade caiu**~~ (AD-163, 40×100): CV mínimo anual 0,298 em média, menor seed 0,257; o 0,097 não volta com as uniões de hoje.

## Da fatia de aprendizado (AD-132)
15. ~~**Números sem conferência**~~ (AD-161, [biologia](relatorios/conferencia-biologia-e-genetica.md)): aptidão, teto de 50% e desuso são escolha de modelo; peso da criação 0,3 fora da faixa (ambiente compartilhado ~0). **AD-162:** peso da criação 0,3→0,1.
16. ~~**Tolerância de nascimentos da primeira década**~~ respondido (AD-161): hoje 270%, limite inferior negativo, só o teto verifica; reescrever ([respostas](relatorios/respostas-de-codigo.md)). **AD-162:** reescrito como faixa de natalidade da década (25–60‰).
17. ~~**Teste do poço fraco**~~ respondido (AD-161): teste de sinal ≥ 15/20 (p ≈ 0,02), sem furto e crime; afirmação testada com poder razoável.

## Gerais
18. **`RuntimeHistoryCostTests`** reprovou no gate em 2 de 3 rodadas (razão 1,045, teto 1,03) e passa sozinho (1,00 a 1,02). Ver [07](07-desempenho-e-testes.md), item 10.
19. **Conferência em fonte incompleta**: três valores da demografia sem fonte e várias fontes vistas só no resumo do buscador (`relatorios/conferencia-demografia.md`). (AD-161: razão 40–44/20–24 só de lembrança, 0,36; neonatal aos 32–36 semanas e fatores de risco seguem sem fonte.)
20. ~~**Demografia de 100 anos da árvore combinada**~~ resolvido no AD-140 (e0 28,2, 1º ano 228‰); sobra teto malthusiano e seed 18 (17 vivos). Original: Com corpo/saúde, vocação/família, cognição e agricultura juntos (commit `edff9923`), 20 seeds × 100 anos: população final 151,1 (mín 86, sem extinção), natalidade 40,8‰, mortalidade 37,0‰, e0 26,7 (referência ~31 a 33), mortes no 1º ano 266‰ (referência ~212 a 234), TFR 5,50, natimortos 2.882 para 11.224 nascidos vivos (conferir se essa proporção é plausível), fome 7,3% das mortes, sede 0,3%. Cada fatia sozinha ficava na faixa; juntas não. Suspeita a confirmar: a fome sazonal da agricultura soma com a desnutrição do AD-134 (multiplicador de mortalidade e teto de saúde). Recalibrar antes de novas fatias que mexam em mortalidade. Saída bruta em `relatorios/demografia-combinada-2026-11-12.txt`.
21. ~~**Cognição (AD-138), números sem conferência**~~ (AD-161): luto de 120 d confere (Maciejewski 2007); as outras meias-vidas e a capacidade são escolha de modelo.
22. ~~**Cognição: lembranças dominadas por ajuda**~~ (AD-163, 40×100): 5,2% das lembranças dos adultos no fim (IC90 4,3–6,3%), era 54%.
23. ~~**Cognição: teste de intervenção em grade sintética**~~ respondido (AD-161): a grade fica (unidade); `CivilizationScopeTests` voltou a 3 vivos no AD-140; efeito no mundo na #146.
24. **Bytes por NPC-tick oscilam entre execuções da mesma DLL** (141 B e 174 B na mesma base): a medição só vale lado a lado na mesma janela.
25. ~~**Agricultura (AD-135), números sem conferência**~~ (AD-161, [economia](relatorios/conferencia-economia-e-credito.md)): 4 por semente, CV 20%, perda 15%, ceifa 100 conferem; 1300 graus-dia é desvio declarado (real ~2000).
26. ~~**Agricultura: população mínima 112 e fecundidade presa à despensa**~~ freio no AD-145/158, furto no AD-150; lobos e celeiro na #167.
27. ~~**Contrato `shape.world/city-subscribe` regravado**~~ respondido (AD-161): o cliente Godot não lê o campo; nada a avisar.
28. ~~**Diff de snapshot**~~ issues #155 e #156.
29. ~~**Hormônios (AD-139), números de memória**~~ (AD-161, [família](relatorios/conferencia-familia-e-nupcialidade.md)): conferem menarca, dismenorreia, pós-parto e pré-nupcial; dp da menarca baixo. **AD-162:** dp da menarca 1,2→1,8 (p10–p90 13,4–16,5 → 12,7–17,1).
30. ~~**Hormônios: efeitos colaterais**~~ fome superada no AD-148; gêmeos e tristeza pós-parto na #161; razão de sexo: AD-163, 40×100, 103,2 (IC90 101,1–105,4) contém 105; o 107,7 era ruído.
31. ~~**Invenção (AD-141), números físicos de memória**~~ (AD-161, [invenção](relatorios/conferencia-invencao-e-fisica.md)): madeira e pedra conferem; junco, margens e vau são escolha de modelo.
32. **Invenção: custo a 1000 NPCs inconclusivo** (1,98 → 2,19 µs/NPC-tick em três pares, ruído da mesma ordem, máquina ocupada); a 5000 NPCs e em alocação não houve regressão. Medir de novo com a máquina livre. ~~Mortes no 1º ano e construções~~ (AD-163, 40×100): 1q0 de coorte 203‰ (IC90 199–207); 0,48 construção falha por boa (0,37–0,61), era 0,66; plausível para quem aprende o ofício, sem fonte.
33. ~~**Cérebro (AD-142), números sem fonte**~~ (AD-161): escolha de modelo (não há grandeza empírica); o puxão da criação deve ficar pequeno.
34. ~~**Recalibração (AD-140)**~~ — AD-163, 40×100: nenhuma seed abaixo de 51 vivos; 1–4 anos 39,4‰ (doença 55%); 36,1% sem causa era a sobra da tábua sem rótulo. **AD-164:** a sobra leva o nome da idade (`DeathCauseRules.ResidualByAge`: debilidade, convulsões, dentição, lombrigas, hidropisia, apoplexia, como nas listas de óbito; combate = `combate`), no payload do `Death`. 6×50: sem causa 0,0%, sobra nomeada 39%; teste pareado prova as mesmas mortes na mesma hora. Hash canônico muda só pelo payload.
35. **AD-140, calibração sem fonte**: 160 vagas, 150 de semeadura por morador e dízimo são escolha de modelo (AD-161); **faixas da doença não conferidas** (tema próprio). Custo: em [a-medir](relatorios/a-medir.md) grupo 2.
36. ~~**Demografia de 20 × 100 da árvore final (`95e9cdc2`) não medida**~~ medida no AD-145 (`902075dd`: pop 264,9, e0 27,0, 1º ano 236‰, fome 11,4%).: os números do AD-140 são sem a invenção.

## Do freio preventivo (AD-145), tudo desligado por padrão
37. ~~**Sede com água em casa**~~ resolvido no AD-146 (commit deste item): era só o `TopUpPurchases` (seeds 1,2,7,8 × 50 anos: sede 4 → 220 com ele sozinho; as outras chaves 4–7). Sem moeda para a comida, a compra da água que completa a meta "dava certo" e o plano terminava sem beber, de 2 em 2 horas. Agora quem fecha a compra com fome ou sede crítica come e bebe da casa. Original: 13,4% das mortes por sede, adultos empregados na ação de comprar, carteira quase vazia, comida a 20.
38. ~~**Ligar por padrão** `FamilyRules.Pressure`~~ ligado no cenário de código no AD-148 (20×100: 0 extinções, fome 4,1%, e0 31,0, 1º ano 205‰, mín 421). Cenário JSON sem o bloco segue desligado (item 48). Original: `FamilyRules.Pressure` (`PopulationPressureRules.Measured`: meios para casar, primeiro filho, emigração, ajuda da família, esmola racionada, compra que completa a meta) depois do 37, e medir 20 × 100 contra os alvos (sem extinção, mín ≥ 50, e0 28–35, 1º ano 200–250‰, fome ≤ 5%).
39. ~~**Idade ao casar não chega a 23–26**~~ nicho feito no AD-158 (posse de terra, ofício ou casa própria para casar; homem 24–27, mulher ~21; sobra no 08b, item 120). Original: nem com os meios (19,0 ligado; base 17,2): falta um nicho de verdade (arrendamento, oficina, casa que só vaga com morte ou velhice) e criadagem para guardar economias; a moeda fica parada no caixa da fazenda e no tesouro (centenas de milhares), então economia de jovem não se forma.
40. ~~**Água aos milhares**~~ resolvido no AD-148: a compra completa a meta por padrão (`ProvisioningRules.TopUp`); seed 7, anos 30–50: água das casas 7 por morador (antes até 8.900).
41. ~~**Ceifa perde 22–26%**~~ resolvido no AD-149 ([land-and-harvest](../../../docs/domain/land-and-harvest.md)): com a lavoura madura a fazenda toma todo adulto do ofício sem trabalho e a parte em espécie vai à turma inteira; rendimento por semente 4,34 → 4,59 (20×100).
42. ~~**Emigração** sem saudade, sem casa inteira, `MoveTo` sem teste~~ confirmado no código (AD-161, [respostas](relatorios/respostas-de-codigo.md)); lacunas na #173, teste em correções. **AD-162:** teste de `MoveTo` com vaga do ofício em outra cidade.
43. **Custo do AD-145 não medido** (pares a 1.000 e 5.000 NPCs). Goldens e linhas novas do relatório: feitos (AD-161, [respostas](relatorios/respostas-de-codigo.md)).
44. ~~**Invenção fatia 2 (AD-143)**~~ (AD-161): 30 kg a pé fora da faixa (sugerido 20), 700 g confere, peixe e rumor escolha de modelo; contrato sem consumidor no Godot; CPU da grade em [a-medir](relatorios/a-medir.md) grupo 2.
45. ~~**Freios ligados depois do AD-146**~~ causa achada no AD-148: 55% dos adultos não podiam trabalhar (motor fino 1,0) e a casa ia ao mercado sem poder pagar; com os consertos, freios ligados: 0 extinções em 20×100.
46. ~~**Quem compra sem poder pagar**~~ resolvido no AD-148 (`ProvisioningRules.OnlyIfCanPay`): antes 70% das idas não compravam nada (seed 4, ano 30).
47. ~~**Sede residual da base**~~ ligado no AD-148 (`CriticalBodyOverridesLeisure`; 6×100: sede 0,2% → 0,0%, fome 3,7% → 3,5%; 20×100 final sede 0,04%).

## Do abastecimento das casas (AD-148)
48. ~~**Freios só no cenário de código**~~ issue #162.
49. ~~**Vila maior**~~ causa medida no AD-149 (`DecadeTally`): fronteira com terra sem fim e emigração presa pela forja; agora terra finita + emigração (pop final 280). Natalidade: item 54.
50. ~~**Motor fino não pesa no rendimento**~~ confirmado no código (AD-161): só barra o trabalho abaixo de 0,7; lacuna em correções.
51. ~~**Moeda das carteiras vai parar na cidade**~~ issue #168.
52. **Custo do AD-148 a 5.000 NPCs** e seeds acima de 20 não medidos; goldens, pins e baselines feitos na junção (AD-161).
53. ~~**Números sem fonte do AD-148**~~ (AD-161): espera, meio dia de despensa e limiar 0,7 são escolha de modelo.

## Da terra finita e da ceifa (AD-149)
54. ~~**Natalidade ~48–50‰ e TFR ~7,8**~~ resolvido no AD-158 (posse para casar; números no decisions-log; sobras 08b 120–123).
55. ~~**Terra só no cenário de código e só na fundação**~~ issues #162 (JSON) e #171 (posse de terra).
56. ~~**Horas de trabalho por adulto 2% menores**~~ issue #172 (diarista, trabalho temporário).
57. ~~**Ceifa da vila só com adultos do ofício**~~ confirmado no código (AD-161); ceifa da casa inteira e respiga em correções.
58. ~~**Ração da oficina de graça**~~ resolvido no AD-151: a refeição do empregador vai à caderneta (`Workplace.Board`) ao preço da venda.
59. ~~**Casa inteira sem trabalho não emigra**~~ confirmado no código (AD-161, `EmigrationSystem.cs:67`); issue #173.
60. ~~**Números sem fonte do AD-149**~~ (AD-161): 220/ha e 320 por ano conferem; 1,4 ha por fundador fora da faixa (sugerido 0,7–1,0); capina escolha.
61. **Custo do AD-149 a 5.000 NPCs** não medido; goldens, pins e baselines feitos na junção (AD-161).
62. ~~**Carga pessoal segue desligada**~~ decidido com medida que discrimina no AD-157 (fome crítica 3× a base com ela ligada, 38 de 40 seeds; 08c item 108–110).
63. ~~**CPU da carga ligada**~~ fonte achada no AD-157: ~1/3 no código da carga, o resto em decisões urgentes de quem tem mais fome (+14–35%); 08c item 110.
64. ~~**Números sem fonte do AD-151**~~ (AD-161): refeição ~¼–⅓ do salário de ofício (estatuto de 1446) e desconto inteiro no lavrador conferem.
65. **Custo do AD-151 a 5.000 NPCs** não medido; goldens, pins e baselines feitos na junção (AD-161).
66. ~~**Caderneta de quem morre**~~ confirmada no código (perdoada, `WagePaymentSystem.cs:94-106`); issue #171 (credor no espólio).
67. ~~**Quem tem refeição no trabalho ainda leva comida de casa**~~ resolvido no AD-157 (leva só para o caminho; vale com a carga ligada).

## Do empreendedorismo (AD-152, [venture](../../../docs/domain/venture.md))
68. ~~**Números sem fonte do AD-152**~~ (AD-161): mestre aos 21 plausível (7 anos de aprendizagem); o resto escolha de modelo.
69. ~~**Confiança mínima do sócio (60) e até dois sócios**~~ (AD-161): escolha de modelo (*commenda* ⅔/⅓, companhias com vários sócios).
70. **Vila só sustenta ~1 ferreiro**: 32 negócios em 20 seeds × 100 anos, quase todos quando o mestre parou. Conferir ferreiros por
    habitante numa aldeia inglesa (AD-161: não achado; contar nas listas da poll tax de 1379/1381). AD-164: de novo sem razão pronta; contar à mão.
71. ~~**Equipamento de fora a preço de teto**~~ confirmado no código (`VentureFounding.cs:36-54`); issue #168.
72. **Custo do AD-152 a 5.000 NPCs** e seeds 21–40 não medidos; goldens, pins e baselines feitos na junção (AD-161).

Itens 73 em diante: [08b-a-investigar.md](08b-a-investigar.md).
