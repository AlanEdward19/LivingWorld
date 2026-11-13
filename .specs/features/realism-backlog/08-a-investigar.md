# A investigar: pontos em aberto deixados pelas fatias já juntadas

> Coisas que não são fatia nova: dúvidas, afrouxamentos e números sem fonte que o dono pediu para não perder.
> Cada item diz o que se sabe, o que é hipótese e como investigar. Riscar quando resolvido, com o commit.

## Da fatia de corpo, saúde e amamentação (AD-134)

1. **Quase todas as mortes sem causa explícita.** Cerca de 98% das mortes ainda são "idade, pela tábua"; só ~2% têm causa nomeada (parto 1,3%, fome 0,5%, sede 0,3%). A doença ficou só com ganchos. Em andamento como fatia própria (AD-137, ver [01](01-em-andamento.md)); depois dela, medir de novo a fração com causa.
2. **Teste de traço neutro afrouxado.** `Unselected_trait_stays_in_the_neutral_band` (calibração de família) perdeu a exigência de diferença média zero no intervalo de confiança pareado; ficou só a faixa calibrada. A diferença medida foi −0,009 (2,8 erros-padrão). Hipótese do subagente, não provada: o braço real escolhe parceiro pela saúde, que agora varia, e o braço neutro escolhe ao acaso; com sucesso reprodutivo mais desigual um traço neutro perde mais variância. Investigar: (a) rodar com a escolha de parceiro sem o fator saúde e ver se a diferença some; (b) rodar mais seeds para saber se −0,009 é efeito ou acaso; (c) se for efeito real, decidir se é seleção indireta legítima (carona genética) e reescrever o teste para medi-la, em vez de deixá-lo afrouxado; (d) se for defeito, corrigir e devolver a exigência.
3. **Dois números sem fonte.** 30% de natimorto quando a mãe morre no parto, e o desgaste de saúde do parto (12 × o risco obstétrico). Procurar em literatura de obstetrícia histórica; se não houver fonte, declarar como suposição do modelo no doc de domínio.
4. **Tempo por NPC-tick não comparável.** O "antes" e o "depois" da fatia foram medidos com cargas diferentes na máquina; só a alocação é confiável (141 bytes, igual). Medir pela fila de uma vaga, em pares alternados, o tick a 1.000 e 5.000 NPCs no commit anterior à fatia (`ee6f0416`) e no posterior (`bce8e56c`).
5. **Demografia saiu da referência calibrada.** e0 caiu de 32,8 para 30,3 e a mortalidade no 1º ano subiu de 214‰ para 244‰ (bebê sem leite morre mais; nascem os filhos de mães que morrem no parto). Ainda na faixa pré-industrial, mas a tábua tinha sido conferida em fonte para dar ~33 e ~212‰. Decidir se a mortalidade-base do 1º ano deve cair na mesma medida, para a amamentação explicar mortes que já existiam em vez de somar.
6. **Sobrevivência do bebê sem mãe.** Razão medida de 0,39 (30% contra 76%), abaixo do alvo de 0,5 da auditoria. Conferir em fonte (Knodel e outros sobre órfãos de mãe) e recalibrar.

## Da fatia de vocação e família (AD-136)

7. **População subiu 17% sem explicação** (177 → 207 de média final), com natalidade e mortalidade quase iguais. Achar a causa: mais uniões por causa do cortejo com preferência individual? recasamento? menos mulheres sem par? Comparar número de uniões, idade ao casar e mulheres-ano casadas antes e depois.
8. **Pressão dos pais pesa pouco na medida.** Seguem o ofício de quem criou 78% com pressão alta e 69% com baixa; e numa medição de 8 seeds × 60 anos o código antigo (sorteio ao nascer) já dava 78%. Entender quanto do número vem do mercado de trabalho da vila (poucas oficinas, quase todo mundo lavrador) e medir num cenário com mais ofícios.
9. **Irmãos criados separados seguem raros** (1,7%). Falta criadagem e aprendizagem fora de casa, que na época separavam muitos irmãos.
10. **Paternidade fora do casamento em 0,2%**, contra ~1% por geração que o subagente lembra (Larmuseau). Conferir a fonte e recalibrar.
11. **Taxas sem fonte**: caso, descoberta, violência e mágoa são calibração pura. Conferir o que houver (registros de tribunais eclesiásticos, estudos de história da família).
12. **Sem "pai social"**: o filho de outro pai herda do biológico. Decidir o modelo (o marido cria como seu se não sabe).
13. **Sem penalidade de vitalidade por endogamia**, embora o tabu vá até primos.
14. **Piso de variância de vitalidade caiu** ao regravar o baseline (seed 4: 0,097 contra piso anterior de 0,129). Confirmar que não é perda de diversidade genética por causa das uniões.

## Da fatia de aprendizado (AD-132)

15. **Números sem conferência**: aptidão por idade, teto de 50% do que a casa ensina, peso da criação (0,3), perda por desuso (4% ao ano).
16. **Tolerância de nascimentos da primeira década** foi alargada duas vezes (150% → 250% na demografia; o aprendizado propôs 175%). Ficou 250%. Rever se o teste ainda verifica algo.
17. **Teste do poço fraco** (`A_weak_well_raises_the_share...`): efeito fraco e não monótono; a água por trabalhador já foi trocada de 0 para 3 e depois para 2, e o limiar de 18 para 15 de 20 seeds. Decidir se a afirmação que ele testa é verdadeira ou se o teste deve ser reescrito ou removido.

## Gerais

18. **`RuntimeHistoryCostTests`** reprovou no gate em 2 de 3 rodadas (razão 1,045, teto 1,03) e passa sozinho (1,00 a 1,02). Ver [07](07-desempenho-e-testes.md), item 10.
19. **Conferência em fonte incompleta**: três valores da demografia sem fonte e várias fontes vistas só no resumo do buscador (`relatorios/conferencia-demografia.md`).
20. ~~**Demografia de 100 anos da árvore combinada**~~ resolvido no AD-140 (e0 28,2, 1º ano 228‰); sobra teto malthusiano e seed 18 (17 vivos). Original: Com corpo/saúde, vocação/família, cognição e agricultura juntos (commit `edff9923`), 20 seeds × 100 anos: população final 151,1 (mín 86, sem extinção), natalidade 40,8‰, mortalidade 37,0‰, e0 26,7 (referência ~31 a 33), mortes no 1º ano 266‰ (referência ~212 a 234), TFR 5,50, natimortos 2.882 para 11.224 nascidos vivos (conferir se essa proporção é plausível), fome 7,3% das mortes, sede 0,3%. Cada fatia sozinha ficava na faixa; juntas não. Suspeita a confirmar: a fome sazonal da agricultura soma com a desnutrição do AD-134 (multiplicador de mortalidade e teto de saúde). Recalibrar antes de novas fatias que mexam em mortalidade. Saída bruta em `relatorios/demografia-combinada-2026-11-12.txt`.
21. **Cognição (AD-138), números sem conferência em fonte**: meias-vidas de luto (120 d), memória (1 ano), medo, raiva, alegria, vergonha e solidão; capacidade de 12 lembranças é escolha de modelo. As referências citadas no relatório vieram de memória do subagente.
22. **Cognição: lembranças dominadas por ajuda** (54% das vagas): avaliar baixar a saliência de ajuda dada e recebida.
23. **Cognição: teste de intervenção roda em grade sintética**, não em mundo simulado; `CivilizationScopeTests` passou a exigir 2 vivos por faixa em vez de 3.
24. **Bytes por NPC-tick oscilam entre execuções da mesma DLL** (141 B e 174 B na mesma base): a medição só vale lado a lado na mesma janela.
25. **Agricultura (AD-135), números sem conferência**: ~4 grãos por semente, perda de 15% ao ano, 1300 graus-dia (trigo real ~2000, calibrado ao mapa), CV de safra 20%, ceifa de 100 por pessoa-dia, coelho (gestação 30 d, 60%/ano de mortalidade), lobo com uma ninhada por ano.
26. **Agricultura: população mínima 112 e fecundidade presa à despensa** (piso de concepção de 5 de comida); mais malthusiano que a base. Lobos extintos; celeiro com sobra de anos; fome sem furto de comida.
27. **Contrato `shape.world/city-subscribe` regravado** (lista `processes` agora traz o campo da lavoura): o cliente Godot precisa ser avisado.
28. **Diff de snapshot**: entidade com forma diferente da primeira linha vai inteira em todo delta; medir quanto isso pesa no disco agora que mais campos são omitidos quando valem o padrão (sensor de escala subiu 0,1% a 0,4%).
29. **Hormônios (AD-139), números todos de memória**: menarca ~15, dismenorreia 15%, TPM ~20%, depressão pós-parto 12%, concepção pré-nupcial 20 a 35%, janela fértil de 6 dias, portador de doença recessiva ~1/25. Chances de sexo fora da união são calibração do subagente.
30. **Hormônios: mortes por fome triplicaram na base dele** (76 → 244), atribuídas a pais jovens que descontam consequências; +3 a 4% de tempo e bytes por NPC-tick a 5.000 NPCs; razão de sexo ao nascer 107,7 (alvo 105); gêmeos idênticos sem o mesmo genótipo; tristeza pós-parto não afeta o cuidado do bebê.
31. **Invenção (AD-141), números físicos todos de memória**: madeira 600 kg/m³ e 40 MPa, junco 200 kg/m³ com 8 meses de vida, couro, resina, pedra, 75 W remando, coeficientes de arrasto, margens (GM 30 mm, borda 80 mm). Sem fonte nenhuma: 4 células de vau por célula d'água e os minutos de coleta por kg.
32. **Invenção: custo a 1000 NPCs inconclusivo** (1,98 → 2,19 µs/NPC-tick em três pares, ruído da mesma ordem, máquina ocupada); a 5000 NPCs e em alocação não houve regressão. Medir de novo com a máquina livre. Mortes no 1º ano 266 → 281‰ sem causa investigada. 49.446 construções falhas para 74.625 boas: conferir se a taxa é plausível.
33. **Cérebro (AD-142), números sem fonte**: taxa de aprendizado, queda da plasticidade com a idade (1 até 20 anos, 0,25 aos 60), segregação de ±100‰, puxão de 1% da criação; herança lamarckiana (o filho recebe parte do que os pais aprenderam) é escolha do desenho. Não separado: quanto do +7% de CPU é mecanismo e quanto é comportamento diferente. Disco medido só na vila de 100.
34. **Recalibração (AD-140), o que não fechou**: teto malthusiano sem freio preventivo (vila cresce ~1%/ano e só para pela fome); com hormônios e0 26,8 (alvo 28 a 35) e fome 15,3% das mortes; seed 18 cai a 17 vivos (região quente, janela de colheita de 40 dias perde metade da safra); casas com milhares de unidades de água de origem não achada; 1 a 4 anos com doença abaixo da tábua; 34,7% das mortes ainda sem causa.
35. **AD-140, calibração sem fonte**: 160 vagas na fazenda, 150 de semeadura por morador, dízimo de 5%/mês com reserva de 12 folhas; faixas da doença de memória. Custo: +2,5% a 5.000 e +3,4% a 1.000 NPCs com a doença; não medido depois dos hormônios nem na árvore com invenção e cérebro.
36. ~~**Demografia de 20 × 100 da árvore final (`95e9cdc2`) não medida**~~ medida no AD-145 (`902075dd`: pop 264,9, e0 27,0, 1º ano 236‰, fome 11,4%).: os números do AD-140 são sem a invenção.

## Do freio preventivo (AD-145), tudo desligado por padrão

37. **Sede com água em casa** quando os freios ligam juntos: 13,4% das mortes (base 0,1%), adultos empregados na ação de comprar, carteira quase vazia, comida a 20. Isolar qual chave causa (rodar cada uma sozinha: `RationedAlms`, `TopUpPurchases`, `KinFoodAid`, meios, emigração) e achar por que quem tem água em casa não bebe (laço Comprar/Viajar?).
38. **Ligar por padrão** `FamilyRules.Pressure` (`PopulationPressureRules.Measured`: meios para casar, primeiro filho, emigração, ajuda da família, esmola racionada, compra que completa a meta) depois do 37, e medir 20 × 100 contra os alvos (sem extinção, mín ≥ 50, e0 28–35, 1º ano 200–250‰, fome ≤ 5%).
39. **Idade ao casar não chega a 23–26** nem com os meios (19,0 ligado; base 17,2): falta um nicho de verdade (arrendamento, oficina, casa que só vaga com morte ou velhice) e criadagem para guardar economias; a moeda fica parada no caixa da fazenda e no tesouro (centenas de milhares), então economia de jovem não se forma.
40. **Água aos milhares**: correção pronta atrás de `TopUpPurchases`; não vale enquanto o 37 não for resolvido.
41. **Ceifa perde 22–26% da lavoura madura** em todas as seeds: só conta quem está no campo na passada diária e a janela de 40 dias corta o resto. Trazer a casa inteira para a ceifa (mulheres, jovens, quem não é empregado) antes de alongar a janela (AD-140 viu explosão com 90 dias). Época de plantio pelo clima local não feita.
42. **Emigração**: sem saudade nem luto dos pais pela partida; casa inteira sem trabalho não emigra para fora do mundo; ida individual para outra cidade (`MoveTo`) sem teste.
43. **Custo e goldens do AD-145 não medidos**: pares de custo a 1.000 e 5.000 NPCs não rodados (mudança de processo: gate e goldens na junção em lote); `pop_min_min` e as linhas `primeiro_casamento` e `emigrantes` novas no relatório.
