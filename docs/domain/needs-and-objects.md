# Necessidades e objetos da casa como dado (#255, S2 do sandbox #246)

Higiene, diversão e bexiga, o chuveiro, o vaso, a TV: **nada disto está no código**. O motor só conhece mecanismos genéricos (necessidade com
taxa e limites, objeto com usos, ação que dura, fila); o que cada coisa é vem do cenário. Decisão: AD-220. Sandbox: [sandbox.md](sandbox.md).

## Catálogo padrão e extensão
- `scenarios/catalog/default-objects.json` é o catálogo padrão (mesa, cama, bigorna, comida, água, cômodos...), **embutido e lido em tempo de execução**
  (`HomeLifeLoader.Embedded`); `DefaultObjects.AddTo` só o junta ao período. O hash do mundo grande não mudou.
- `CatalogExtends: ["home-life"]` no período junta `scenarios/catalog/home-life.json` (necessidades novas + móveis com usos). O que o cenário já declara
  vence por id (objeto, mobília por tipo de prédio, cômodo, necessidade); o resto do catálogo entra no fim.

## Necessidades (`Needs` no cenário)
`{id, name, start, decayPerHour, urgentBelow, color?, alert?, effects?:[{below, target, perHour}]}`.
- Vivem em `Npc.ExtraNeeds` (`LazyNeed`: nada escrito por tick; nulo e fora do hash sem catálogo). `HomeLife.Seed` as dá ao nascer, à chegada e ao semear.
- `effects` usa só alvos genéricos: `health` (saúde por hora), `social` (a solidão cresce mais rápido) e `bond` (cada conversa de quem está abaixo do limite
  piora o laço de quem ouve: `HomeLife.BondPenalty`, hoje em `PlayerInteractions.Talk`). `HomeLife.Hourly` roda no `NeedsDecaySystem` só para quem tem extras.
- **As quatro de sempre** (fome, sede, sono, social) continuam em `NeedsRules`/`Npc.*Need`: movê-las muda o hash e o contrato de todos os cenários. Os usos agem nelas pelo
  id (`hunger`, `thirst`, `sleep`, `social`); a ficha traz o nível de todas pelo mesmo instante em ms (`NpcInspectionDto.CatalogNeeds`, `NeedHint`).

## Objetos (`uses` no descritor)
`{id, name, durationMin, effects:[{need, perHour}], consumes?:[{resource, quantity}], produces?:[{resource, quantity}], toggle?, pose?, poseFallback?, slot?:[x,z], facing?, action?, untilFull?, occupies?}`
e `rooms?:[tipo]` no objeto (o chuveiro no banheiro). Pose nova é só um nome: sem animação própria o cliente usa `poseFallback` (o rig não muda).
`action` delega à ação do motor (dormir): o corpo chega ao lugar e `PlayerCommands.Act` assume. `untilFull` encerra quando a necessidade enche.

## Usar um objeto (`ObjectUsage`)
1. **Valida**: peça com lugar na planta, mesmo andar, livre (`occupies`), despensa tem o que gasta. Recusa é 409 e o mundo não muda.
2. **O corpo anda até o lugar**: busca em largura pela regra do chão do passo (`PlayerGround.TerrainRefusal`: parede em aresta, porta, canto), do centro de uma
   aresta ao da seguinte, na velocidade do corpo (`PlayerWalk.Approach`); `BodyTask{Use, Building, Piece, Doing:false}`. A ação só começa no lugar.
   Sem tempo fino, ou NPC que ninguém conduz, o corpo chega na hora.
3. **A ação dura** o que o catálogo diz; `BodyTaskSystem` agenda fatias (`15 s`–`5 min`) e cada uma soma `perHour × horas` às necessidades. Interromper (Espaço, WASD ou
   `POST .../interrupt`) mantém o que já subiu.
4. **Fila** (`Npc.UseQueue`, até 4, no snapshot): `interact {kind:object, verb:"queue:<uso>"}`; `{kind:queue, id:N, verb:cancel}` tira a N. A próxima começa ao fim da atual.
5. **Quem ninguém conduz** (`ObjectUsage.TryCare`, na decisão): à toa em casa, antes de passear ou de dormir, escolhe a necessidade do catálogo mais abaixo do limite e o uso que
   mais a sobe de uma vez (taxa × duração, até encher). A decisão da hora não o tira do uso; o fim do uso e o cruzamento do limite o acordam (`HomeLife.NextWakeMs`).

## Contrato (só aditivo)
Oferta `object`: `uses:[{id,name,minutes,need,busy}]`, verbos `use:<id>`. Pose: `busy.{use,label,phase(walk|do),pose,slotFx,slotFy,facing}` e `queue:[{use,label,object}]`.
Ficha: `catalogNeeds:[{id,name,value,urgent,color}]`, `needHint:{need,alert,object,use}`. Interior: `usingUse` no ocupante. Descritor: `uses`, `rooms`; catálogo: `Needs`.

## Cliente
`player_frame.gd`: uma barra por necessidade do catálogo (a mais urgente com seta), a dica ("Você está com a bexiga cheia → Vaso"), a fila (Shift+1–4 tira) e a roda com **um item
por objeto** (usos num submenu) e páginas (`Mais ▸`). `body_busy.gd`: "Indo: Tomar banho…" e a barra do progresso; a pose do catálogo escolhe o gesto que já existe.
Passos de captura: `useverb:Objeto|Uso`, `needs`, `needslog:S`, `waituse:S`, `cancelq:N`, `sleepfast`, `theother`.

## #260: leitura das barras, luz e fim do uso
`needRates` (ficha, só com catálogo de necessidades): pontos por hora de cada necessidade agora, `-queda + o que o uso em curso entrega` (`HomeLife.Rates`); o cliente desenha número, ícone
(`icon` da necessidade, `NeedIcon`) e seta de tendência (subindo/caindo, rápido). Objeto com `light` `{color,rangeM,energy,heightM}` (`ObjectLight`) dá luz; ligar e desligar é o `toggle` (#258, abaixo).
Aviso de fim: "Terminou: Tomar banho · Higiene 92%" (a necessidade que o uso mais sobe, da oferta `need`).

## #258: refeição, luz e casa mantida
`produces` põe na despensa o que o uso faz, só no fim natural (o fogão: 2 comida viram 2 refeição; interromper perde). `toggle` `light:on|light:off` liga ou desliga a luz do objeto
(`world.Lights`, `LightLedger`, canônico só se alguma está ligada; a oferta esconde o que não muda nada); o interior traz `lit` por peça e o delta `lights`. `HomeTending`: o NPC liga a luz do
cômodo ao escurecer, apaga de dia, ao dormir e ao sair, e cozinha antes de comer sem urgência. Ver [sandbox.md](sandbox.md).

## Fora desta fatia
Mover as quatro necessidades de sempre para o catálogo; entrega por telefone; animações próprias (#254).

## #261: cada um a seu modo e rotina que não para
`NeedDef.variation {jitter, traits:{traço:peso}}`: o nível de partida varia até `jitter` pontos (sorteio semeado do mundo por pessoa) e a taxa de queda é `decayPerHour × (1 + Σ peso × (traço−50)/50)` (0,5 a 1,5). `NeedStartJitter {hunger,...}` (raiz do cenário,
junto de `Needs`) varia só o nível de partida das necessidades de sempre. `seekBelow` (acima de `urgentBelow`): quem ninguém conduz e está à toa já procura um uso abaixo desse nível, escolhendo entre as duas necessidades mais baixas e os usos
mais fortes pelo sorteio semeado; procurando (sem urgência) nunca escolhe uso que gasta da despensa. O rótulo de minutos da oferta é `ObjectUsage.PlannedMinutes` (o que o uso leva agora com `untilFull`).

