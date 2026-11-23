# Criar o próprio personagem ou tomar a vida de um NPC [Estado: desenho para o dono, nada implementado]

Issue #63, item 8 (pedido do dono de 2026-11-12). Fatia AD-196. Não muda o `ROADMAP.md`: o escopo da Fase 25 é do dono.

## O que já existe e vale para as duas entradas
- Encarnar troca só a **escolha** (`Npc.ControllerId`, `ControlledAction`); necessidades, corpo, psique (`PsycheState`:
  lembranças episódicas, emoções com meia-vida), cérebro (`BrainState`), memória social e laços seguem os do NPC.
- Sair é desaparecer (`Disappearance`), com procura, luto na medida do vínculo e a política de abandono do cenário (AD-196).
- A possessão por poder já existe (`possessed-action`, `PossessionResisted`): o motor tem o molde de "outra vontade conduz este
  corpo, e o corpo pode resistir".

## Entrada 1 — criar o próprio personagem (#46, criador de pessoa)
Proposta: o jogador entra como **recém-chegado** pelo mesmo caminho da imigração autorada (`AuthoredImmigration`): o criador
de pessoa produz um NPC coerente com a cidade de chegada (cultura, idade, ofício, pertences), com domicílio próprio e sem
laços prévios. Nada de gente do nada: o NPC sai da amostragem do pool agregado da cidade de origem (Fase 8, "materializar
move, nunca cria"); o pool perde quem saiu. Sem vínculos, o sumiço desse personagem quase não dói, e isso é coerente.
- Opção A (recomendada): imigrante materializado do pool, ficha escolhida dentro do que o pool permite.
- Opção B: ficha livre do criador de pessoa — mais liberdade, mas cria gente fora da conta demográfica.

## Entrada 2 — tomar a vida de um NPC que já existe
O jogador assume alguém com família, ofício, dívidas, memórias e laços. A pergunta central é a **mente** do NPC.

| Opção | Mente do NPC durante o controle | Quem convive percebe | Quando o jogador sai |
| --- | --- | --- | --- |
| A. Casca | ignorada: o jogador decide tudo | nada | o NPC volta sem lembrar de nada do período |
| B. Coautoria (recomendada) | segue viva: personalidade, humor e necessidades pesam e podem **recusar** a ordem | sim, pela conduta | o NPC lembra do período como seu |
| C. Possessão | o NPC é passageiro e resiste como na possessão por poder | estranhamento, medo | trauma (lembrança de possessão) |

Recomendação: **B**, porque mantém o jogador sem regra especial e reusa o que existe.
1. **Recusa calculada.** A ordem do jogador entra como mais um termo da utilidade (`DecisionContext`), com peso alto mas
   finito; urgência forte (fome, sede, sono no limite), luto e medo podem vencer, como já vencem a rotina (AD-138). A recusa é
   registrada (`PlayerOrderRefused`, evento novo) e o jogador vê o motivo. Hoje (AD-111/196) a ordem vence sempre; mudar é
   decisão do dono.
2. **A vida que ele vive é a do NPC.** O que acontece durante o controle (casamento, briga, dívida, trabalho) vira lembrança
   e laço do próprio NPC pelos caminhos de sempre (`LifeEventIntake`, relações). Ninguém precisa "perceber" o jogador:
   percebem a conduta. Quem convive reage ao que o NPC fez, não a quem o conduziu.
3. **Mudança de conduta brusca** (o NPC calmo que passa a brigar) aparece sozinha na relação (confiança cai pelos eventos);
   não há detector de "jogador".
4. **Na saída, duas escolhas do dono**: (a) o NPC some (o de hoje, coerente com "offline = desaparecimento" e provado byte a
   byte); ou (b) o NPC **fica** e o motor reassume na hora, com a memória do período, sem procura nem luto. (b) é mais
   realista para quem "devolve" a vida, mas quebra a equivalência "logout = desaparecimento" da Fase 25. Recomendação: (a)
   por padrão e (b) como opção do cenário (`PlayerRules`), como a política de abandono.
5. **Volta do NPC tomado** depois de sumido segue a `LateReturn` da AD-196: reencontro na medida do vínculo e do tempo, a
   herança dividida não volta, o cônjuge que se uniu de novo segue unido.

## Critérios (a escrever ao ativar a fatia, `rules/eval-criteria.md`)
- Coautoria: com fome no limite, a ordem de trabalhar é recusada em ≥18/20 seeds; desligar a coautoria a faz valer sempre.
- O período controlado deixa lembranças e laços no NPC idênticos aos de um NPC do motor que fizesse as mesmas ações.
- Entrar como imigrante não muda a soma da população (pool + materializados).

## Perguntas ao dono
1. Coautoria (B), casca (A) ou possessão (C)? Recomendo B.
2. Na saída, o NPC tomado some (padrão de hoje) ou fica com o motor? Recomendo sumir por padrão e "fica" como opção do cenário.
3. Criar personagem: só imigrante do pool (A) ou ficha livre (B)? Recomendo A.
