# Posse: disputa de vontade, estranhamento e reação [Estado: AD-197e/197f entregues; ritual e posse por poder em issue]

Decisões do dono (2026-11-23): pelo admin a pessoa vira personagem do jogador e não recusa (posse plena); sem admin ela recusa o
que vai contra os vínculos **e os valores** dela, "tem que ser mais forte que o controle", cansa, pode ser persuadida, pode
expulsar ou impedir a posse; quem convive estranha a conduta, descobre e reage ("nada hardcoded"). Mesma base para a
possessão dentro do jogo, inclusive de um personagem de jogador. ADR-0049.

## Estado no motor
- `Npc.ControlMode` (comum/plena), `Npc.Captive` (`CaptiveMind`, plena), `Npc.Will` (`WillContest`, comum: resistência no tick,
  sob controle ou solta, checagens seguidas, última persuasão). `WorldState.Possession` (`PossessionLedger`: estranhamento por
  observador e corpo, boato, conclusão, vigília). Nulos sem jogador: o mundo sem jogador tem o hash de antes.
- Regras em `PlayerRules.Possession` (`PossessionRules`), dado de cenário; chaves `LW_AD197F=off|nowill|nostrange|noreact`.

## Disputa de vontade (posse comum) — `PossessionContest`
- **Vontade** (0..100) = (0,45 estabilidade emocional + 0,35 conscienciosidade + 0,20 (100 − impulsividade)) × mente (medo ou
  luto tiram até 40%) × corpo (saúde: 50% a 100%).
- **Controle do jogador** = 40 + 30 × min(1, horas de posse / 168): perícia naquele corpo. **Por poder** (Fase 16, possessão
  extraordinária): mesma conta, com o controle tirado da confiabilidade do descritor do poder (issue abaixo).
- **Resistência** começa na vontade (menos o cansaço de uma posse anterior), cai 0,5/h × controle/40 sob controle, sobe 1/h
  solta; persuasão (`persuade`, 1 por hora) tira 8 × (0,5 + amabilidade/200); apoio de quem conclui a possessão devolve 15.
- **Ordem de ferir** pesa max(cuidado com o alvo, 0,8 × altruísmo): quem ajuda a todos recusa ferir qualquer um. Com peso ≥ 60
  (o limiar do dono, `RefuseHarmCare`) recusa se resistência × peso / 100 > 0,5 × controle. A recusa volta com o motivo (409),
  não muda o mundo e fica no registro; o replay confere que recusa de novo.
- **Impedir**: resistência ≥ 2,2 × 40 recusa a entrada. **Expulsar**: resistência ≥ 1,6 × controle em 3 checagens seguidas (a
  cada 6 h, evento agendado no `PossessionSystem`) devolve o corpo à pessoa, que vive a posse na psique (`PossessionExpelled`).
- **Plena** (admin): nunca recusa; a pessoa presa sofre o que vai contra ela (`CaptiveMind`) e lembra ao ser solta.

## Estranhamento, descoberta e reação — `PossessionWitness`
- **Desvio**: ação pedida igual à rotina da pessoa naquela hora (ofício, idade) não desvia — imitar bem não levanta suspeita;
  diferente desvia 15; golpe desvia max(30, peso contra os valores dela).
- **Quem repara**: quem mora com ela (peso 1) e quem tem vínculo próximo e está na mesma célula (peso do vínculo); quem vigia
  repara sempre.
- **Boato**: a 40, o observador comenta (`PossessionSuspected`) e quem mora com ele ouve pela metade.
- **Conclusão** (crença, não verdade): a 100 / crença do lugar (1 onde o extraordinário existe, 0,5 onde não; o cenário declara
  `Belief`). Lembrança "não é mais ele" (`PossessionConcluded`).
- **Reação** (`PossessionConfronted`): quem se importa (cuidado ≥ 50) apoia — a resistência da pessoa sobe e ela pode expulsar;
  quem não se importa e tem propensão à violência ≥ 1,5 ataca o corpo (`Violence.Fight`: ferimento, justiça, mágoa); o resto
  vigia. Violência contra família só como registro abstrato.

## Fora desta fatia (issues)
- **Ritual**: o mundo não tem religião nem ofício de quem trata possessão. Precisa de crença por cultura (quem acredita em
  quê), ofício de curandeiro/sacerdote e uma ação de tratar que, como o apoio, mexa na resistência e no controle; nada fixo.
- **Conhecimento que o possuidor não tem**: estranhar o que a pessoa saberia (nomes, ofício, caminho de casa) pede que o
  jogador declare o que sabe (memória acessível) e que a conversa e a ação revelem lacunas; depende da conversa (Fase 11).
- **Posse por poder** na mesma disputa: ligar `possessed-action` e `PossessionResisted` (Fase 16) ao `PossessionContest`.
- **Mente presa: conversa e pensamento no chat** (#207).
- **Outras ordens contra a vontade** (vender a casa, abandonar filho): fatia de interagir (AD-197d), mesma regra.
