# Fase 25 — Jogadores

**Objetivo**: um jogador é um NPC com controlador externo, nunca um caso especial dentro do
motor. Pode ser superhumano — passando pela **mesma cadeia declarada de resolução** que a
Fase 16 define para qualquer portador, sem exceção baseada em quem controla o NPC. E o
logout **não congela o personagem**: emite um evento de **desaparecimento**. O mundo segue,
e quem convivia com ele dá falta, procura, chora, herda e ocupa a vaga que ele deixou.

> **Spec, não gate.** Os critérios abaixo são a intenção; os critérios finais são escritos
> sob `rules/eval-criteria.md` quando a fase for ativada. Não comece esta fase antes da
> Fase 11 fechar.

## Tasks
1. **Encarnação**: assumir um NPC existente **ou** entrar como personagem novo. Nos dois
   casos a identidade sai da amostragem condicionada do LOD (Fase 8) — família, origem,
   profissão e histórico coerentes com o agregado de onde a pessoa veio. Entrar não cria
   gente do nada: materializar move, nunca cria.
2. **Mesmas regras, sem exceção**: fome, sono, envelhecimento, doença, ferimento e morte
   valem igual. Invocar potência passa pela mesma cadeia declarada da Fase 16 — mesmo
   descritor, mesma confiabilidade, mesmo custo quando existe. Nenhum sistema paralelo
   "modo jogador" para potência: trocar AI por controlador externo não altera a resolução.
3. **Logout emite desaparecimento** no Tier A, não congelamento. O personagem some do lugar
   onde estava; o motor continua conduzindo os efeitos, nunca o personagem.
4. **Reação ao sumiço, por proximidade**: quem tinha vínculo procura, sofre, e depois
   segue — herança pela Fase 7, vaga de trabalho reaberta pela Fase 5, relato do
   desaparecimento entrando no cânone da Fase 10. A intensidade sai da força do vínculo.
5. **Retorno tardio**: voltar depois de anos significa que você esteve desaparecido esse
   tempo todo. A memória social registra: o luto já foi feito, a herança já foi dividida, o
   cônjuge pode ter recasado, e a reação ao reaparecido é calculada, não roteirizada.
6. **Modo espectador**: sessão somente-leitura, sem superfície de escrita — acompanha sem
   encarnar ninguém e sem poder agir.
7. **Multiplayer**: vários jogadores no **mesmo** branch compartilham um único scheduler e
   uma única ordem de aplicação; jogadores em branches diferentes vivem linhas separadas que
   nunca se fundem (ADR-0008).
8. **Política de abandono declarada no cenário**: o que acontece com o personagem cujo
   jogador nunca volta — segue desaparecido, é declarado morto após a janela declarada, ou
   volta ao pool agregado. É dado de cenário, não regra escondida em código.
9. **Cenário `test-players` pareado**: o mesmo mundo conduzido só pelo motor e conduzido com
   encarnação, para servir de braço de controle. Inclui `test-player-parity`: mesmo NPC passa
   de AI para controlador externo sem alterar a resolução mecânica de potência.
10. **Aviso de pausa na interface do jogador** (ADR-0015): pausa e velocidade são **globais
    por branch e exclusivas do administrador** — todo jogador conectado para junto e vê na
    tela quem pausou e desde quando. Branches diferentes pausam independentes, cada um com
    relógio próprio (ADR-0012). Não ameaça o hash: é estado do hospedeiro, fora do snapshot
    (Fase 1), e a reflexão sobre os campos serializados já protege isso.

## Critérios de verificação
- **Jogador offline é só um desaparecimento** — a prova de que ele não é caso especial: par
  base/tratamento na mesma seed. Base = o NPC `X` é conduzido pelo motor e recebe um evento
  de desaparecimento no tick `T`; tratamento = o mesmo `X` é encarnado e o jogador desloga
  em `T`. Os dois braços produzem hash canônico byte-idêntico por 10 anos a partir de `T`.
- **Nenhuma ação de jogador burla o que um NPC não pode burlar**: enumerar por reflexão
  todas as ações expostas ao jogador e exigir que cada uma resolva para a **mesma** cadeia de
  validação da ação equivalente de NPC — falha se alguma ação de jogador ficar sem
  equivalente ou sem cobertura. Par de mutação: remover a validação de uma ação tem de
  **fazer este critério falhar**.
- **O sumiço dói na medida do vínculo**: par base/tratamento na mesma seed variando **só** a
  força do vínculo dos próximos, com o mesmo desaparecimento no mesmo tick. O braço de
  vínculo forte produz reação maior (busca, luto, mudança de rotina) em ≥ 18/20 seeds.
  Direção, não magnitude.
- **Espectador não toca no mundo**: 1000 ticks com sessão de espectador ativa e navegando
  produzem o mesmo hash canônico que 1000 ticks sem sessão nenhuma.
- **Dois jogadores no mesmo branch não divergem**: as duas sessões emitem ações intercaladas;
  reaplicar o log resultante a partir do snapshot reproduz o mesmo hash, e as duas sessões
  observam o mesmo estado no mesmo tick. Estado divergente entre clientes reprova.
- **Branch pausado por admin congela todo jogador conectado**: com a pausa ativa, enumerar
  por reflexão **todas** as ações de jogador e exigir que cada uma falhe em alterar o mundo —
  falha se alguma ação ficar sem cobertura. Em N ticks de tempo real com sessões conectadas e
  agindo, o hash canônico não muda.
- **Encarnação entrou na conta**: desligar o subsistema de jogador muda o hash em 10 anos.

## Fora do escopo
Cliente 3D e voz: Fase 14. Mapa e drill-down: Fase 15. Conversa com NPC via LLM: Fase 11 —
aqui o jogador age, não conversa. Console, visão de verdade e modo god: Fase 26. Assistir à
vida de outro NPC: Fase 27. Ramificação em si: Fase 18 (aqui só é consumida). Autenticação e
infraestrutura de sessão não são arquitetura de motor e ficam fora desta spec.

## Questões em aberto
- Encarnar um NPC existente sobrescreve a utility AI dele. Personalidade, humor e
  necessidades continuam influindo (e podem recusar uma ordem), ou o jogador substitui a
  decisão inteira e o NPC vira casca?
- Quanto tempo de **mundo** um personagem fica desaparecido antes de ser declarado morto,
  sabendo que o jogador mede o tempo em horas reais e o mundo em décadas?
- Jogador materializa a região onde está. Dois jogadores em pontas opostas do mapa dobram o
  custo detalhado: existe teto de jogadores por mundo, ou o LOD degrada sob pressão?
- Jogadores em branches diferentes se veem de alguma forma? Se o ADR-0008 proíbe fusão,
  multiplayer entre linhas só pode ser assíncrono — objeto e relato atravessando, nunca
  presença.
- Espectador observa a **verdade** ou a **crença** de alguém? Se observa a verdade, ele é
  admin da Fase 26 com outro nome, e o critério de vazamento da Fase 10 passa a valer aqui.

## Ver também
[npc.md](../domain/npc.md) · [simulation-lod.md](../domain/simulation-lod.md) ·
[behavior.md](../domain/behavior.md) · [memory.md](../domain/memory.md) ·
[historical-memory.md](../domain/historical-memory.md) ·
[timelines.md](../domain/timelines.md) ·
[ADR-0008](../adr/ADR-0008-ramificacao-como-modelo-temporal.md) ·
[ADR-0011](../adr/ADR-0011-primitivo-unico-de-resolucao.md) ·
[ADR-0015](../adr/ADR-0015-pausa-global-e-auditoria-de-admin.md) ·
[rules/eval-criteria.md](../../rules/eval-criteria.md)
