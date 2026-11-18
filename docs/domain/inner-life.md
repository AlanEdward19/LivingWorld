# Vida interior — memória da própria vida, emoção e humor (AD-138)

O NPC lembra do que viveu, sente, e isso muda o que ele faz. Tudo vem do motor: a LLM pode
narrar a lembrança ou rotular a emoção, nunca criá-las (`rules/llm-boundary.md`).

## De onde vem
Um consumidor do log de eventos (`LifeEventIntake`, chamado por `TickContext.LogEvent` no
instante do evento) transforma cada evento de vida em lembrança e emoção de quem o viveu, sem
tocar quem emite e sem varredura por tick:

| Evento do motor | Quem lembra (tipo) |
|---|---|
| morte (`NpcDeath`) | cônjuge, pais (filho), filhos (pai/mãe), irmãos, amigos com afeto ≥ 60 |
| nascimento, natimorto, aborto | mãe e pai |
| casamento, separação, abandono | os dois |
| caso descoberto (e violência) | traído, infiel |
| furto descoberto | vítima, ladrão |
| furto sofrido sem saber quem; agressão; parente assassinado; punido (AD-150) | vítima; quem apanhou; o parente; o condenado |
| ajuda mútua, esmola | quem recebeu, quem deu |
| contratação, demissão, despejo, acolhimento de órfão | a pessoa (a casa, no despejo) |
| jejum ≥ 24 h que terminou (`NeedsDecaySystem`) | quem passou fome |
| adoecer (`LifeEventIntake.OnFellIll`) | gancho para a fatia de doença, ainda sem chamador |

## Memória episódica (`PsycheState.Memories`)
- Tipo, pessoa, tick, saliência (0..100) e último reforço. A força decai exponencialmente;
  a meia-vida cresce com a saliência (viés emocional): morte de filho lembrada por décadas,
  contratação esquecida em poucos anos.
- Algo relacionado (mesma pessoa) reaviva; o mesmo tipo com a mesma pessoa se funde.
- Capacidade de 12; as fracas somem e, cheia, a mais fraca dá lugar à nova se a nova for mais forte.

## Emoção e humor
Seis emoções (luto, medo, raiva, alegria, vergonha, solidão) em milésimos, com meia-vida
própria; o valor atual é calculado na leitura (nada roda por tick ou por dia). Pouca
estabilidade emocional amplifica e prolonga as negativas; a extroversão amplifica alegria e
solidão. Valência e ativação são derivadas. Grande perda deixa marca: −2 de estabilidade
emocional para sempre; casar dá +1 (a personalidade muda devagar com a vida).

## Onde muda o que o NPC faz (consumidores)
- **Rotina**: no dia em que o luto ou a vergonha pesam, não vai trabalhar ou não sai para
  conversar (um sorteio por pessoa e dia contra o recolhimento, que cai com o luto).
- **Decisão urgente**: termos de vida interior na utilidade (luto tira de Work e Socialize e
  puxa para Idle; vergonha recolhe; alegria aproxima; medo segura Travel; solidão recente
  desconta a esperança de achar alguém; sem moeda e com a despensa baixa, `IncomeNeed` pesa em Work).
  AD-166: o luto não puxa para Idle quando comer (o que a despensa serve) ou comprar vale acima de 50
  (`GriefYieldsToBody`): quem chora come e abastece a casa. Com isso a morte por acidente e por velhice também enluta
  (`MournEveryDeath`); 40 × 15 pareado: fome crítica 1,003×, mortalidade 0,977×, sede crítica 0,981×.
- **Despensa**: quem passou fome quer meta e compra maiores (até ×2).
- **Ajuda mútua**: quem foi ajudado ajuda mais (a casa de quem ajudou, sobretudo).
- **Vínculos**: quem foi traído, roubado ou abandonado confia menos em todos e menos ainda
  em quem lhe fez mal; evita conversar com essa pessoa; com medo, evita estranhos; enlutado
  procura os seus.
- **Socialize** só satisfaz a necessidade social com alguém por perto; sozinho, a solidão sobe. AD-186 (`SeekCompany`): quem
  quer conversar, está em casa sem ninguém na janela da conversa, sente falta de gente (convívio na urgência) e já se sente só
  (solidão ≥ 0,2, `SeekCompanyLoneliness`) vai, pela rota, à casa do conhecido de quem mais gosta que esteja em casa (até 12
  células, `SeekCompanyMaxCells`) ou ao largo se há gente lá; o destino escolhido na saída fica no intento `Socialize` e vale a
  ida toda; o anfitrião que recebe a visita em casa também conversa (a falta de gente dele passa); cérebro: `procurar-companhia`.
- **Acolhimento** (AD-186): a casa que fica só com crianças é vista no dia; os irmãos vão juntos para quem acolhe, escolhido
  por laço (parente 1, quem gosta da criança 0,8 × afeto, quem gostava dos pais 0,6 × afeto, a cidade 0,15) × meios por boca
  × generosidade ÷ casa cheia (`OrphanCareSystem`; `OntogenyRules.OrphanCareLegacy` volta ao mensal). Cérebro: `acolher-crianca`.

## Pensamento = motivo
O rastro cognitivo é feito dos termos da utilidade da ação vencedora (`UtilityTerm`), com o
peso que tiveram; ver [cognition.md](cognition.md).

## Números
Padrão em `PsycheRules` (`NeedsRules.Psyche`); `PsycheRules.Disabled` desliga. Fontes e a
marca "precisa de conferência" ficam no registro AD-138 do `docs/decisions-log.md`.
