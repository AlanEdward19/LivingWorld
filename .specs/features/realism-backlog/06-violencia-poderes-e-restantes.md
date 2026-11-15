# Violência, furto, poderes por genética e achados restantes (furto, violência e justiça: AD-150)

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
| 10 | Herança de poder não é genética | feito (AD-156); sobras P1–P7 abaixo |
| 17 | Preço preso no piso, demanda fixa, salário sem ligação com habilidade | não começado (04) |
| 26 | Testemunha lembra com fidelidade total; sem boato de pessoa a pessoa | feito para o crime (AD-150); a história (`HistoryRules`) segue igual: item V8 |
| 30 | Cônjuge herda 100%; furto desligado e sem justiça | feito (AD-150) |

## Violência e crime (AD-150: itens 1, 2, 4, 5 e 6; AD-154: item 3; ver `docs/domain/crime-and-justice.md`)

1. Furto ligado por padrão, por fome, miséria e oportunidade, de moeda, comida, banca, celeiro, caixa e ferramenta. **Feito.**
2. Violência entre pessoas (ofensa, ciúme, traição, herança, denúncia, vingança, flagrante), ferimento, homicídio, mágoa. **Feito.**
3. **Violência doméstica** e seu efeito em separação, em fuga e nas crianças criadas na casa. **Feito (AD-154)**, fora a fuga de quem não é cônjuge: V11 em [06b](06b-crime-contra-pessoas.md).
4. Justiça: testemunha imperfeita, boato com distorção, denúncia ou silêncio, corte, multa, castigo, banimento, forca, dissuasão. **Feito.**
5. Herança pela norma (terça da viúva, filhos, oficina ao primogênito) e disputa. **Feito.**
6. Medição de 20 seeds × 100 anos: no registro AD-150. **Feito.**

O que ficou de fora do AD-150 (nada some; cada item é fatia ou linha futura):
- V1. Bebida (não existe no mundo) como motivo de briga; entra quando houver taverna e bebida.
- V2. Dívida em dinheiro (o mundo não tem empréstimo; a `Debt` da relação é favor) como motivo de briga.
- V3. Furto de animal de criação (não há rebanho) e de peça inventada (fatia da invenção, outro agente).
- V4. Guarda profissional e senhor como pessoas do mundo (hoje dado de cenário: profissão de guarda e `Authority`).
- V5. Má fama pesando em contratação, casamento e ajuda (hoje pesa em suspeita, condenação, medo e confiança).
- V6. Prisão, fiança, apelação, perdão do senhor, asilo em igreja; outlaw que volta.
- V7. Furto planejado com deslocamento real do ladrão até o alvo (hoje o ato é abstrato, na hora sorteada).
- V8. Boato e testemunha imperfeita também para a história (`HistoryRules`, `WitnessedAccount`).
- V9. Luto por morte de `Accident` e `OldAge` (o `LifeEventIntake` não as consome; achado ao ligar o homicídio).
- V10. Sede: resolvida (o crime deixava gente viva com saúde ≤ 1, e a percepção faz a vizinhança fugir). A regra da percepção (ferido = perigo) segue: vale rever fora do crime.

## Crime contra bens e contra pessoas (pedido do dono em 2026-11-12)

Furto (AD-150), sequestro, violência sexual e consequência social (AD-154), com estado e sobras V11 em diante: [06b](06b-crime-contra-pessoas.md).

## Poderes por genética (AD-156: itens 1–7 e 9 feitos; detalhe e sobras P1–P7 em [06b](06b-poderes-por-genetica.md))

## Doença de época (a fazer; sobrou da fatia de corpo e saúde)

Ligar a doença por padrão: contágio por convivência (casa, turma de trabalho, mercado), mais grave em crianças, velhos e desnutridos, imunidade, surtos, incubação, doente acamado que não trabalha. Tem de ser causa das mortes que a tábua já prevê: descontar da mortalidade-base a parte atribuída à doença e recalibrar para manter e0 e a mortalidade infantil. Ganchos prontos: `DiseaseProfile.HealthPenalty`, `LethalityByAge`, `MalnutritionLethalityFactor`, `MortalitySystem.StateMultiplier`. Meta: a maioria das mortes com causa explícita (hoje ~2%).

## Doença dinâmica: surgir sozinha e ser criada à mão (pedido do dono em 2026-11-12; a fazer)

Palavras dele: "sobre doenças, ok você criar algumas de preset, mas elas devem ser dinâmicas como todo o restante. Uma nova doença deve poder aparecer, como também eu poder criar uma manualmente e simular no mundo."

1. **Preset é só ponto de partida.** Os sete perfis do AD-137 continuam como dado de cenário, mas a lista de doenças passa a ser estado do mundo, que cresce e muda.
2. **Doença nova emerge**: por mutação de uma existente (via, letalidade, incubação, duração e imunidade cruzada derivam do perfil de origem com variação semeada) e por salto de animal para gente onde há convívio com fauna e rebanho. Nome, sintomas e gravidade saem dos parâmetros, nada escrito à mão. Pode se extinguir, virar endêmica ou voltar em ondas.
3. **Evolução depois de surgir**: pressão por transmissão e por letalidade (o que mata rápido demais se espalha menos); imunidade da população muda o que circula.
4. **Criar à mão e soltar no mundo**: comando de console/modo deus (fase 26) e bloco de cenário JSON para definir um perfil e introduzir em uma pessoa, cidade ou região; entrada registrada como evento, reproduzível (mesma seed + mesma intervenção = mesmo mundo). Poder rodar "e se" numa cópia do mundo.
5. **Cenário JSON lê todos os campos do perfil** (pendência já anotada no AD-137).
6. **Resposta do mundo emerge também**: medo, fuga, quarentena, cuidado e, quando inventada, medicina (liga com invenção emergente, arquivo 05, e com `CareLevel`).
7. Medir: quantas doenças surgem e somem em 20 seeds × 100 anos, sem a vila se extinguir; custo por NPC-tick sem regressão.

Depende de a doença (AD-137) estar juntada, o que hoje espera a recalibração da mortalidade ([01](01-em-andamento.md), fatia 4).

## Lacunas conhecidas fora da auditoria

- Cidades-filhas fundadas com 1 ou 2 moradores (`SpatialSettlementFoundingSystem`); contornado na esmola, não corrigido.
- Os cenários JSON declaram a tábua de vida otimista antiga e os de época moderna herdam fecundidade natural, sem contracepção.
- `ReferenceCivilizationTests` (#141) continua com `Skip`: 20 seeds × 300 anos do cenário de referência não foram rodados depois das correções.
- A morte materna no parto ainda impede o bebê de nascer (entra na fatia de corpo e saúde).
- `Socialize` enche a necessidade social mesmo sem ninguém por perto (03).
- Idosos: cuidado por parentes, aposentadoria do trabalho pesado e dependência não existem.
- Religião, festas, lazer e costume como fonte de encontro e de norma não existem.
