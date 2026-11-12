# Doença de época (AD-137)

O motor não conhece nenhuma doença: cada uma é um **perfil de cenário** (`DiseaseProfile`), e o cenário de código usa
`DiseasePresets.PreIndustrial` (vila medieval). Os cenários JSON continuam sem doença se não declaram o bloco `Disease`.

## Perfis da vila medieval

| Perfil | Via | Padrão | Quem morre |
|---|---|---|---|
| diarreia | água e comida | endêmica, pico no verão | desmame (6–24 meses), velhos, desnutridos |
| catarro | respiratória | endêmica, pico no inverno | bebês e velhos |
| febre (tifo, tifoide) | vetor/casa | endêmica, fim do verão | adultos; três semanas de cama |
| tísica | respiratória | latência longa, anos de doença | adulto jovem; trabalha menos por meses |
| bexigas | contato | chega de fora a cada poucos anos | crianças; imunidade de vida, marca |
| sarampo | respiratória | chega de fora, contagia no pródromo | crianças desnutridas |
| peste | vetor | rara, verão | mais da metade de quem pega |

Cada perfil declara incubação (com sorteio), dias contagiosos (antes e depois do sintoma), duração, parte dos casos de
cama, letalidade por idade, peso da desnutrição, imunidade (dias; de vida = 100 anos), sequela, sazonalidade por mês,
casos ambientais por idade e chance anual de chegar de fora. Quem existia no começo do mundo já teve as doenças de
infância pela chance da idade (`FounderImmunityByAge`).

## Ciclo de uma infecção (`OutbreakSystem`, diário)

1. **Pega**: do ambiente (sorteio de Poisson sobre a população, aceito pela idade), de fora (abre surto: evento
   `OutbreakStarted`) ou de alguém contagioso.
2. **Incuba**: sem sintoma, sem contágio (salvo o pródromo).
3. **Adoece**: decide-se no sintoma se fica de cama, quem cuida (a mãe, o cônjuge, a primeira mulher da casa, quem tiver
   idade) e se morre; a morte é agendada numa hora da doença (evento `Disease` com o perfil). Doente sem ninguém para
   cuidar morre mais.
4. **Sara**: imunidade pelo perfil e, às vezes, sequela (o corpo envelhece alguns anos). Surto sem caso: `OutbreakEnded`.

## Contágio pela convivência real

Só quem está contagioso espalha, e só para quem convive com ele: cada morador da casa (quem cuida, em dobro), os
vizinhos de turma na oficina (se ainda trabalha) e alguns encontros na cidade por dia (mercado, conversa; de cama, só
visitas). Com o surto conhecido na cidade, o medo corta os encontros fora de casa. Custo por doente e por caso.

## O doente

De cama não trabalha nem sai (dorme, come, compra se precisa); quem cuida falta ao trabalho em metade dos dias. A saúde
cai até o teto que a doença tira e volta na convalescença (`PhysiologySystem`); o caso leve trabalha mais fraco
(`BodyMechanic`). Criança doente pode regredir marco de desenvolvimento.

## Causa das mortes

A doença **explica** parte da tábua: o risco-base mensal (`MortalitySystem`) é multiplicado por
1 − `BaselineShareByAge` (metade no bebê, 70% de 1 a 4 anos, metade no adulto, 20% no velho). A morte que sobra da
tábua é nomeada por `DeathCauseRules`: acidente (taxa anual pela idade, mais no homem) e velhice (a partir dos 60);
o resto fica sem causa (`Death`). Órfãos, viuvez e herança seguem de `NpcDeath`, como em qualquer morte.

## Gancho para a medicina

`DiseaseRules.CareLevel` (0 na vila) multiplica as reduções de letalidade e duração que cada perfil declara.

## Medir

`bash scripts/demography-report.sh` imprime mortes por causa e idade, a fração com causa, a variação de ano a ano
(anos de surto) e os dias de trabalho perdidos por doença.
