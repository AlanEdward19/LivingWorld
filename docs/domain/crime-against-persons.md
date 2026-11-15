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
- Chance/ano = taxa × propensão da violência (corpo³ × impulsividade × instabilidade) × (1 + fome + miséria + desemprego)
  × (1 + ciúme: mágoa conjugal) × (1 + histórico: lembrança de violência sofrida, inclusive na infância) × (1 − dissuasão).
- Alvo: o morador que a pessoa menos estima (idade mínima 3); no ciúme, o cônjuge. Ato no fim da tarde.
- Desfecho: dano menor que o da rua, golpe mortal raro (homicídio pelo caminho das mortes). Medo e mágoa na vítima,
  mágoa conjugal (+20) que leva à separação do AD-136, e lembrança de violência nas crianças da casa (o ciclo).
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
