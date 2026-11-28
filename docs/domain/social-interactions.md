# Interações sociais como dado (#259, S3 do sandbox #246)

Elogiar, abraçar, beijar, discutir, convidar para ver TV: **nada disto está no código**. O motor só conhece mecanismos genéricos (requisito, aceitação por
termos, efeitos, duração, gesto); o que cada interação é vem de `scenarios/catalog/social.json`, estendido pelo período (`CatalogExtends: ["home-life", "social"]`).
Jogador→pessoa e pessoa→pessoa passam pelo mesmo `SocialInteractions.Start`. Decisão: AD-223. Sandbox: [sandbox.md](sandbox.md).

## O bloco `Social`
- `rules`: `adultYears`, `reachM` (alcance para começar), `cooldownMin` (pausa por par entre iniciativas), `initiateNeed`/`initiateBelow` (a necessidade que pede companhia),
  `checkEveryMin`, `historyHours`, `spouseAffection`/`spouseTrust` (o que um casal sente desde o começo, lido sem criar laço) e `texts` (motivos gerais com `{actor}`, `{target}`).
- `categories` (menu), `states` (estado do par, o primeiro que casa: casados, muito próximos, amigos...; olha o que a outra pessoa sente) e `mood` (nota 0–100 =
  50 + peso × emoção líquida + necessidades pesadas; o rótulo é a primeira faixa que a nota não passa).
- `interactions[]`: `{id, name, category, durationSec, pose, targetPose, requires, accept, effects, onRefuse?, joint?, initiative?, accepted, refused}`.

## Decidir (aceitação)
`requires {affectionMin, trustMin, states[], adultsOnly, reason}` decide se aparece disponível (as que não valem aparecem esmaecidas com o motivo). Valendo, o alvo decide
por `accept {base, terms[{of, weight, from, reason}], refuseReason}`: chance = base + Σ peso × (valor − from), com `of` em `relation.affection|trust|respect`,
`target.mood`, `actor.mood`, `target.need.X`, `actor.need.X`, `target.trait.X`, `recent.<categoria>` (a última interação do par foi dessa categoria, dentro do histórico).
Sorteio pelo RNG semeado do mundo (`ctx.OneShot`): 100 ou mais sempre aceita, 0 ou menos nunca. O termo que mais pesa contra dá o motivo ("está precisando de um banho").
Corpo conduzido por jogador aceita sempre (o jogador se solta com Espaço ou WASD).

## Viver e aplicar
- Recusa: sucesso `outcome: refused` com `note` (frase + motivo); aplica `onRefuse`, grava o par e não ocupa ninguém. Não vale: 409 com o motivo.
- Aceita: `BodyTask{social}` nos dois corpos; quem começou anda até ficar a 0,9 m (NPC sem condução chega na hora); ao fim aplica `effects` (laços de cada lado, necessidades
  em pontos, emoções + memória `LifeEventKind.Social`). Interromper vale a fração vivida (menos de 1/4: nada), e solta os dois.
- Atividade conjunta (`joint {use, partnerUse}`): os dois usam peças da casa por usos `hidden` do catálogo (sofá, cadeiras, cama); laços e humor valem ao começar.
- Iniciativa (`initiative {weight, toPlayer, need, below, affectionMin}`): quem ninguém conduz, com a necessidade abaixo do limite, escolhe pessoa e interação por utilidade + RNG e
  pede pelo mesmo `Start`; `NextWakeMs` o acorda a cada `checkEveryMin`.
- O par guarda a última interação (`Relationship.LastSocial*`) para o histórico, a frase da relação e o aviso curto.

## Contrato (só aditivo)
Oferta `person`: `social:[{id,name,category,categoryName,available,reason,seconds}]`, `relation:{state,stateLabel,affection,trust,mineAffection,mineTrust,mood,moodScore,last,lastMs}`;
verbo `social:<id>`. Resposta da interação: `outcome`, `note`. Pose: `busy.{with,initiator,partnerPose}` e `social` (último fato). Ficha: `socialNote`, `socialBusy`. Interior: `social` no ocupante.

## Cliente
`social_menu.gd` ("Interagir ▸" → categorias → interações; esmaecida diz o motivo), `relation_panel.gd` (afeição, confiança, estado, humor, último fato), gestos novos
(`hug`, `kiss`, `laugh`, `argue`, `plead`; pose desconhecida cai em `talk`) em `npc_actor.gd`, o par se vira um para o outro (`app.gd`, `interior_view.gd`).
Fora desta fatia: conversa por IA com o catálogo (M7), vizinhos, interação com mais de duas pessoas, gestos com animação própria (#254).

## #261: de longe, sentado e junto até o fim
- **Alcance** (`rules.approachM`, 8 m): a pessoa aparece no menu social com linha de visão (`SocialInteractions.InSight`: mesma cidade, andar e prédio, sem parede no caminho reto pela regra do chão do passo). De longe a oferta
  vem **sem verbos** (só o menu); ao escolher, quem começa anda até ela (`Walk`, busca em largura pelas portas) e o gesto só começa ao chegar; o parceiro fica parado e ocupado até o fim, nos dois corpos.
- **Com um móvel em uso**: `compatibleWithUse: true` na interação (bater papo, piada, elogio, queixa, desculpa) vale com quem começa sentado (efeitos na hora, o uso segue) e com o alvo sentado (quem começa anda até ele, o alvo segue no móvel).
  As que pedem corpo livre (abraço, beijo, briga): quem começa **levanta antes** (como no Sims); com o alvo no móvel a interação some com o motivo `texts.targetUsing`.

## #267: a briga custa
`argue`: o alvo perde 14 de afeição e 9 de confiança, quem brigou 6 e 4; `apologize` devolve só 4 e 2; o momento "Brigou com {other}" dura 3 dias (ver [moods-and-aspirations.md](moods-and-aspirations.md)).
