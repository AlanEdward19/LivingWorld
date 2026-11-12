# Violência, furto, poderes por genética e achados restantes (não começado)

> Números de achado referem-se à tabela de `relatorios/relatorio-realismo.md` (30 achados, feita no commit `baea58fd`).

## O que o dono pediu (palavras dele)

- "Preciso que todos os sistemas sejam ajustados para realismo. Incluindo nascimentos, sexo (masculino e feminino quanto à reprodução), relacionamentos, crescimento, saúde, doença, roubo, violência, etc."
- "Única coisa que deve fugir da realidade são os poderes, mas neles iremos adotar uma abordagem de genética (como seria na vida real se fossem existir)."

## Estado de cada achado da auditoria

| # | Achado | Estado |
|---|---|---|
| 1, 2, 4, 29 | Nascimento e morte no mesmo dia; tábua otimista; fertilidade plana; razão de sexo e gêmeos | feito (AD-130, AD-133) |
| 3, 5, 8, 12, 14, 15, 16, 20 | Saúde estática, amamentação, corpo, variância genética, músculo, sono, fome, doença | em andamento (01); sono e prazo da fome já corrigidos (AD-133) |
| 6, 19, 21, 22 | Trigo em 24 h, estrago, clima, fauna | em andamento (01) |
| 7, 25 | Aprendizado desde o nascimento, habilidade que não se esquece | feito (AD-132) |
| 13, 18, 23, 24, 27 | Tabu de parentesco, casamento que cria comida, profissão sorteada, atração como média do casal, viuvez e divórcio | em andamento (01) |
| 9, 11, 28 | Memória, rastro de pensamento, personalidade fixa e uniforme | não começado (03) |
| 10 | Herança de poder não é genética | não começado (abaixo) |
| 17 | Preço preso no piso, demanda fixa, salário sem ligação com habilidade | não começado (04) |
| 26 | Testemunha lembra com fidelidade total; sem boato de pessoa a pessoa | não começado (03 e abaixo) |
| 30 | Cônjuge herda 100%; furto desligado e sem justiça | não começado (abaixo) |

## Violência e crime (a fazer)

Hoje: `TheftResolver` furta por desespero (carteira vazia, fome ou sede, temperamento, vítima com moeda ao lado), com guardas reduzindo o sucesso; é desligado por padrão. Combate só existe na pasta de poderes. Não há briga, agressão doméstica, homicídio, punição nem justiça.

1. **Furto ligado por padrão e realista**: por necessidade e por oportunidade e temperamento (não só desespero); furto de comida, não só de moeda; o que foi roubado faz falta à vítima.
2. **Violência entre pessoas**: briga por ofensa, ciúme, dívida, traição descoberta (liga com 01) e bebida; mais provável em homens jovens (liga com 02), com impulsividade alta e estabilidade emocional baixa; resultado em ferimento (saúde dinâmica), morte ocasional, inimizade duradoura e vingança de parentes.
3. **Violência doméstica** e seu efeito em separação, em fuga e nas crianças criadas na casa.
4. **Justiça e punição conforme a época**: denúncia, testemunha, julgamento local, multa, castigo, banimento; guarda e senhor como dados de cenário; impunidade quando ninguém viu. A reputação corre por boato de pessoa a pessoa (achado 26), com distorção.
5. **Herança realista** (achado 30): partilha entre cônjuge e filhos conforme a norma do cenário; disputa de herança como fonte de conflito.
6. Medir em 20 seeds × 100 anos: taxa de homicídio (ordem de dezenas por 100 mil por ano na Europa medieval; conferir em fonte), furtos por ano e o que os causou, ferimentos, punições, vinganças.

## Poderes por genética (a fazer; desenho proposto na seção 3 do relatório)

Hoje: o filho só herda se os dois pais forem portadores, com chance 1,0, e num dos caminhos recebe todos os poderes dos dois, que se acumulam a cada geração.

1. **Genótipo**: um locus por poder, dois alelos por NPC (materno e paterno); cada pai transmite um dos dois com 50%; loci independentes ou ligados.
2. **Modo de herança por poder**: dominante, recessivo, ligado ao X ou poligênico com limiar. Portador heterozigoto de traço recessivo transmite sem manifestar.
3. **Penetrância e expressividade**: chance de manifestar dado o genótipo e intensidade contínua.
4. **Idade e gatilho de manifestação**: curva de início (puberdade) combinada com o gatilho ambiental que já existe.
5. **Mutação nova**: taxa baixa por locus por gameta, no lugar do sorteio de prevalência; prevalência inicial pelo equilíbrio de Hardy-Weinberg.
6. **Custo metabólico**: poder manifesto aumenta o gasto de fome e sede mesmo sem uso; o mesmo alelo pode reduzir fertilidade ou vitalidade, para a seleção natural operar.
7. **Fim do acúmulo**: o filho só tem o que os alelos herdados dão.
8. Mesmo motor para as doenças genéticas (02).
9. Testes: filhos de heterozigoto dominante com não portador manifestam em 50% ± 5%; filhos de dois heterozigotos recessivos, em 25% ± 5%; o número médio de poderes por portador não cresce entre gerações (20 seeds × 150 anos).

## Doença de época (a fazer; sobrou da fatia de corpo e saúde)

Ligar a doença por padrão: contágio por convivência (casa, turma de trabalho, mercado), mais grave em crianças, velhos e desnutridos, imunidade, surtos, incubação, doente acamado que não trabalha. Tem de ser causa das mortes que a tábua já prevê: descontar da mortalidade-base a parte atribuída à doença e recalibrar para manter e0 e a mortalidade infantil. Ganchos prontos: `DiseaseProfile.HealthPenalty`, `LethalityByAge`, `MalnutritionLethalityFactor`, `MortalitySystem.StateMultiplier`. Meta: a maioria das mortes com causa explícita (hoje ~2%).

## Lacunas conhecidas fora da auditoria

- Cidades-filhas fundadas com 1 ou 2 moradores (`SpatialSettlementFoundingSystem`); contornado na esmola, não corrigido.
- Os cenários JSON declaram a tábua de vida otimista antiga e os de época moderna herdam fecundidade natural, sem contracepção.
- `ReferenceCivilizationTests` (#141) continua com `Skip`: 20 seeds × 300 anos do cenário de referência não foram rodados depois das correções.
- A morte materna no parto ainda impede o bebê de nascer (entra na fatia de corpo e saúde).
- `Socialize` enche a necessidade social mesmo sem ninguém por perto (03).
- Idosos: cuidado por parentes, aposentadoria do trabalho pesado e dependência não existem.
- Religião, festas, lazer e costume como fonte de encontro e de norma não existem.
