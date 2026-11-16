# A investigar (continuação de [08b](08b-a-investigar.md), mesma numeração)

## Da natalidade e nupcialidade de época (AD-158)
120. **A mulher ainda casa cedo (~21) e quase nenhuma fica solteira** com a posse para casar ligada (20×100); o homem casa aos
     24–27 e o celibato cai nele (1–3% aos 45). Causa medida: o cortejo aceita o primeiro par aceitável (limiar), não é mercado; quem tem
     posse aceita a menina de 18 que o procura antes da de 23 que ele preferiria. Falta: escolha do lado escasso (quem tem
     posse escolhe entre as que o aceitariam), criadagem/serviço das moças (15–25 anos fora de casa, juntando enxoval) e a
     norma de diferença de idade pequena do noroeste europeu. Referência: mulher 23–26, 10–20% nunca casam (Hajnal 1965;
     Wrigley e Schofield) — precisa de conferência.
121. **TFR conjugal 20–49 ~8–9** (estimado na linha `por_decada_nupcialidade`; Inglaterra pré-industrial ~7,5 de lembrança,
     precisa de conferência). Intervalo entre partos média 29–30, mediana ~26 meses: a mediana curta vem da morte do bebê, que
     encurta a amenorreia (real). Conferir com a fonte antes de mexer na amenorreia (365 d) ou na fecundabilidade (0,20).
122. **Posse para casar só vê terra da fazenda comum** (`Holdings`): não há arrendamento, herança da posse ao filho que fica
     (o herdeiro casa saindo de casa; só a casa que ele chefia sozinho com os filhos conta), nem lote de casebre. Os 3,5 ha
     por posse e 0,7 ha por pessoa são calibração do modelo. Cenário JSON sem o bloco `Pressure` segue sem a regra (08, item 48).
123. **Concepção pré-nupcial subiu** (primeiros filhos concebidos fora da união 27% → 39% em 20×100; nascidos fora de união
     0,9% → 2,2%): a gravidez que obriga a casar passa por cima da posse. Conferir contra registros paroquiais (lembrança:
     20–35% de noivas grávidas no séc. XVIII inglês) — precisa de conferência.
## Do crédito de abertura (AD-159, [credit](../../../docs/domain/credit.md))
130. **Carência de 6 meses na abertura** (`FoundingGraceMonths`): lembrança de contratos de aldeia com o 1º pagamento na
     colheita ou feira seguinte (Briggs, *Credit and Village Society*, 2009). Precisa de conferência.
131. **Prazo da abertura até 60 meses** (`MaxFoundingTermMonths`): crédito rural medieval era curto (meses a poucos anos); 5
     anos é o teto que lembro para dívida de equipamento com garantia. Precisa de conferência.
132. **O salário do próprio fundador conta inteiro no ganho que paga a parcela** (`OwnWageInCapacityShare` = 1) e a parcela
     segue até a metade dele (`MaxInstallmentShare`): sem fonte; escolhido porque a sonda mostrou 4 de 5 negativas pela parcela.
