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
20. **Demografia de 100 anos da árvore combinada** (corpo e saúde + vocação e família) ainda não medida; rodar `scripts/demography-report.sh 1 20 100 10` pela fila e registrar aqui.
