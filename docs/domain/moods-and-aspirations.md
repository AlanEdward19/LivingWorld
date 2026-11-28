# Humor, momentos, aspirações e desejos como dado (#262, épico #246)

Feliz, irritado, "Banho tomado", "Brigou com Marina", a aspiração de vida e o desejo "Abraçar Marina": **nada disto está no código**. O motor só conhece
mecanismos genéricos (gatilho, peso, duração, empilhar, efeito, condição de desejo); o que cada coisa é vem de `scenarios/catalog/moods.json`,
estendido pelo período (`CatalogExtends: [..., "moods"]`). Reaproveita o que já existe: necessidades do catálogo, interações sociais, psique e metas.
Decisão: AD-228. Sandbox: [sandbox.md](sandbox.md).

## O bloco `Moods` (`MoodCatalog`)
- `rules`: `scorePerWeight` (pontos da nota de humor por ponto de peso), `psycheWeight`, `wishSlots`/`wishHours`/`wishPoints`, `rewardMoment`, `goalUrgency`, `calm`.
- `emotions[]`: `{id, name, icon, face?, psyche?, color?}`. `face` é a emoção da psique que o rosto do boneco já sabe desenhar (joy, grief, fear, anger, shame,
  loneliness); `psyche` liga a emoção da psique a esta (a força dela entra na disputa pela emoção dominante); `icon` é o nome do rosto desenhado pelo cliente.
- `moments[]`: `{id, name, emotion, weight, durationMin, stack?, maxStack?, trigger, effects?}`; `name` pode citar `{other}` (quem causou).
- `aspirations[]`: `{id, name, text, affinity:{traço: peso}, milestones:[{points, name, moment?}]}`. `wishes[]`: `{id, text, fulfill, when?, target?, weight, points?, aspirations?, reward?}`.
- Referência a uso, interação, categoria ou necessidade que o cenário não tem fica inerte (um cenário que troca o chuveiro não perde o catálogo); emoção, momento,
  aspiração e o vocabulário do motor (ação, fato da vida) são validados com o caminho do campo.

## Gatilhos genéricos (`trigger.kind`)
| kind | quando | guarda estado? |
|---|---|---|
| `use` | uso do catálogo termina por inteiro (interromper não conta) | sim, com prazo |
| `action` | ação do motor concluída (`Sleep`, `Eat`), de NPC ou jogador | sim, com prazo |
| `social` | interação aceita ou recusada (`interaction`/`category`, `result`, `side`) | sim, com a outra pessoa como causa |
| `life` | fato da vida (`LifeEventKind`: `Hired`, `Violence`...), pela psique | sim |
| `state` | enquanto a necessidade está abaixo/acima de um nível | **não** (lido na hora) |
| `reward` | só o motor dá (desejo cumprido, marco) | sim |
Empilha (`stack`) até `maxStack`, senão renova. Os de evento ficam em `Npc.Moods` (no snapshot e no hash só se existe); expiram na leitura, sem passo por tick.

## Efeitos reais (coesão)
- **Nota de humor**: a soma dos pesos × `scorePerWeight` entra na nota (`SocialInteractions.Mood`) que os termos `target.mood`/`actor.mood` do catálogo social já
  leem: humor ruim derruba a chance e dá o motivo da recusa ("não está de bom humor").
- **Rendimento**: `effects.useRate` multiplica o que os usos de objeto sobem (`ObjectUsage.Apply`: "Bem descansado" 1,15; "Exausto" 0,85).
- **O que escolhe fazer**: `effects.socialBias` (categoria → fator) pesa na utilidade da iniciativa social do NPC (`TryInitiate`): quem brigou puxa mais o que é mau.
- **Rosto e postura**: a emoção dominante (momentos + psique) dá `dominantEmotion` do observável quando a psique não manda nenhuma; o cliente já traduz em rosto e postura.

## Aspiração e desejos
- **Aspiração** (sem estado): a que mais combina com a personalidade (soma de peso × (traço − 50)/50; empate pela ordem). Pontos de satisfação em `Moods.Points`;
  cruzar um marco dá o momento dele e entra em `recent`.
- **Desejos** (`wishSlots` por vez, `wishHours` de vida): sorteados de hora em hora (RNG semeado do mundo) entre os que o estado permite (`when`: necessidade, cônjuge),
  com peso ampliado (`aspirationBoost`) para os da aspiração; `target` `spouse|housemate` liga a uma pessoa. Cumprem-se pelo mesmo vocabulário dos gatilhos
  (`use`, `action`, `social` aceita com a pessoa certa, `state` lido de hora em hora): +pontos, momento de recompensa e `recent` (aviso do cliente).
- **Metas**: cada desejo ativo é uma meta `wish.{id}` (urgência baixa) em `PlayerGoals`; `GET /players/{id}/goals` traz `mood`.

## Contrato (só aditivo)
Ficha: `mood {score, label, emotion, emotionName, icon, face, color, moments[{id,name,emotion,weight,count,remainingMs,cause}], aspiration, aspirationName, points, done,
nextMilestone, nextMilestonePoints, wishes[{id,text,other,sinceMs,expiresMs,points}], recent[{id,text,atMs,points,milestone}]}`; omitido sem catálogo. Metas: `mood` igual.

## Cliente
`player_frame.gd`: rosto + emoção + ação na mesma linha ("Feliz · Parado") e a lista de momentos ("Bem descansado +2 · 3 h"); `goals_panel.gd` (J): aspiração com o próximo
marco, desejos com a recompensa e o último cumprido; `player_hud.gd`: aviso "Desejo cumprido: ... (+10 pontos)". Rostos em `need_icon.gd`. Passo de captura: `mood`.

## Fora desta fatia
Gatilho de ambiente (cômodo bonito ou sujo: o motor ainda não mede o cômodo); desejos de emprego e dinheiro (sem carreira); aspirações que mudam a vida (casar, filho).
