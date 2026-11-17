# Crime contra a pessoa: violência doméstica, sequestro, violência sexual (AD-154)

Continuação de [crime-and-justice.md](crime-and-justice.md). Tudo sai do estado dos NPCs (temperamento, corpo, estresse,
mágoa, pobreza, quem conhece quem, medo da punição) e do RNG semeado; nada é escrito à mão. Cada crime tem chave própria
(`CrimeRules.Domestic`, `.Kidnapping`, `.SexualViolence`); nulo (snapshot do AD-150) ou desligado = não existe, e o resto
do dia não muda (stream próprio `crime-person-day-N`).

## Limite de apresentação (firme)
- Doméstica e sequestro aparecem como acontecem: autor, vítima, vínculo ou motivo, desfecho.
- Violência sexual é um **evento abstrato do motor**: autor, vítima, categoria (adulto/criança), caso, concepção. Nenhum
  campo descreve o ato; o cliente mostra uma frase factual curta que nomeia o crime. Contra criança, só o fato e as
  consequências; a criança nunca engravida (a concepção exige mulher adulta fértil).

## Violência doméstica (`DomesticViolenceRules`)
- Chance/ano = taxa (0,012) × propensão da violência (corpo³ × impulsividade × instabilidade) × (1 + fome + miséria + desemprego)
  × (1 + ciúme: mágoa conjugal) × (1 + histórico: lembrança de violência sofrida, inclusive na infância) × (1 − dissuasão).
- Alvo: o morador que a pessoa menos estima (idade mínima 3); no ciúme, o cônjuge. Ato no fim da tarde.
- Desfecho: dano menor que o da rua, golpe mortal raro (homicídio pelo caminho das mortes). Medo e mágoa na vítima,
  mágoa conjugal que leva à separação do AD-136, e lembrança de violência nas crianças da casa (o ciclo). AD-162: a norma
  tolerava o "castigo moderado" e a separação por crueldade era rara (exigia violência excessiva provada): a agressão leve e
  isolada soma só 1/4 dos +20 (`ToleratedGrievanceShare`); a grave (dano ≥ 25) ou repetida (a vítima ainda guarda mágoa ≥ 10 do
  mesmo agressor) soma os +20.
- AD-163, medida pela grandeza da fonte: **esposas agredidas no ano ÷ anos de esposa** (`ReferenceTally`): 3,0% (40×100),
  abaixo de 10–20% (OMS 2018 / Sardinha 2022: 13% no último ano, moderno [B]). 0,036 dá 9,5–10,2% com separações 12 → 16 e
  natalidade igual, mas (20×100, pareado) horas de fome crítica 1,28× (IC90 1,01–1,67), fome dos menores de 5 2,0× (1,25–3,6),
  homicídios 1,28×: **não aplicado**.
- AD-164, por que leva à fome (sonda `DomesticHungerProbe`, 12 pares × 50 anos, ×3): horas de fome crítica 1,73× (IC90
  1,11–2,48). **Não é o dano**: a vítima adulta trabalha 28,5% → 28,8% das horas (igual à média). **Nem a perda do provedor**:
  12 mortos ou agressores enforcados/banidos em 2.046 agressões. A casa não pula de fome depois da agressão (90 dias depois ÷
  antes 1,12). O caminho local mais forte é a **separação depois da agressão**: quem sai leva os filhos pequenos e uma parte da
  despensa, sem sustento depois — fome crítica 15,8 h/pa (aldeia 5,2) e 17,6 h/pa nos menores de 5 (aldeia 1,7). A vítima no
  ano seguinte tem 1,75× a aldeia sem trabalhar menos (hipótese não provada: o medo corta `Travel`). ~60% do excesso cai em quem não teve agressão
  em casa em 5 anos: difuso, pede ~160 pares. Correção proposta: pensão do separado (a separação canônica obrigava o marido a
  sustentar a mulher) e o medo pesar só no caminho onde está o agressor.
- AD-165, com a pensão aplicada (sustento do separado, `UnionRules.SupportChildUntilAge` 12, evento `SupportPaid`): 20×50
  pareado, com a doméstica ×3 o sustento baixa a fome crítica 0,81× (IC90 0,60–1,03) e a da vítima 0,59× (0,38–0,91). De novo
  0,012 → 0,036 sobre o sustento (20×50): esposas agredidas 3,3 → 9,9%/ano, fome crítica 1,08× (0,84–1,37: passa da margem
  1,25), homicídios 1,82× (1,33–2,56), natalidade 0,955×, fome dos <5 separados 12,6× (1,02–79), população 179 → 166:
  **segue não aplicado** (inconclusivo pela margem e o custo em homicídios e natalidade). O medo contra sair
  (`FearTravelPenalty` 0) **não** explica a fome da vítima: sem ele ela sobe 2,1× (1,09–3,23); hipótese refutada, o medo fica.
- Justiça: a vítima sabe quem foi, mas a denúncia é rara (fração 0,15, uma decisão só) e a corte condena menos (0,5).

## Sequestro (`KidnappingRules`)
- Motivo do estado: **vingança** (mágoa ≥ 60: leva o filho pequeno do inimigo, ou ele), **resgate** (sequestrador pobre;
  criança de família com ≥ 60 moedas; pede metade, até 400), **posse** (solteiro que gosta muito de quem não gosta dele).
- Cativeiro: a vítima vai para a casa do sequestrador (célula real), fora da rotina (`Kidnapping.Hold` na decisão), dorme
  no chão e come e bebe do que aquela casa tem; sem comida, morre de fome ou sede (causa explícita).
- Cada dia: fuga (adulto ousado; criança 0,3×), busca de cada parente (4× se desconfia do sequestrador), resgate pago pela
  família (moeda passa, nunca é criada), e no prazo (21 dias) soltura ou morte (mais na vingança). Sequestrador banido ou
  morto solta a vítima.
- Ao voltar, a vítima e a família sabem quem foi: caso aberto (flagrante quando achada), mágoa de vingança dos parentes.

## Violência sexual (`SexualViolenceRules`)
- Chance/ano = taxa × propensão da violência × insensibilidade (pouca amabilidade e consciência) × (1 − dissuasão).
- Oportunidade: entre conhecidos e moradores da casa na cidade, sorteio pela fraqueza relativa (músculo); o do mesmo sexo
  pesa 0,1. Na hora, qualquer pessoa acordada no lugar faz o autor desistir.
- Consequências: trauma (lembrança de saliência 95: medo, vergonha; marca a estabilidade para sempre), confiança no
  autor desfeita, gravidez possível pelas regras de concepção (fração 0,25 do ciclo, sem casamento forçado), revelação a
  cada parente (0,3): raiva, mágoa de vingança, e quem sabe pode denunciar pela criança.
- Justiça: má fama só quando alguém de fora viu ou quando é denunciado; denúncia rara (0,3, uma decisão, cortada pelo
  medo); condenação 0,5× a comum; punição forca 15%, banimento 40%, senão multa e açoite; contra criança, nunca só multa.

## Justiça comum para os três
`Justice.OnCrime(victimKnows)`: a vítima viva entra como quem sabe e aponta o autor (sem contar como testemunha); a palavra
dela soma meia testemunha na corte. Criança não denuncia: denuncia quem sabe por ela, salvo parente do acusado (silêncio).
Caso de dentro de casa ou às escondidas não vira boato enquanto ninguém denuncia. Sem prisão (V6).

## Eventos, cliente e custo
`DomesticViolence`, `Kidnapped`, `KidnappingEnded`, `SexualAssault` (payloads em `WorldEventKind`), donos no `CRIME`,
narrados em `CrimeEventNarrator`. Custo: um sorteio por adulto, crime e dia no stream próprio; a conta só abaixo do teto;
o preso é pulado na decisão por uma checagem nula quando não há cativeiro.
