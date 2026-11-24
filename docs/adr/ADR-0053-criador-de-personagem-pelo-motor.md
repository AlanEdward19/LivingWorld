# ADR-0053: criador de personagem pelo motor (amostra do agregado, registrada; entrar = materializar + encarnar)

- **Status**: aceito pelo dono em 2026-11-24 ("Sim, pelo motor")
- **Data**: 2026-11-24
- **Decisores**: dono do projeto (decisão de produto); rascunho por agente (AD-197q)

## Contexto
Até aqui o jogador só entrava encarnando um NPC já detalhado (`POST /players/{npcId}/incarnate`). A Fase 25 (task 1) pede também
entrar como **personagem novo**, sem criar gente do nada: a pessoa sai da população que o motor já conta no agregado da cidade
(`City.AggregatePool` + `PoolNpcIds`), com origem, ofício e histórico coerentes com ele (`docs/domain/simulation-lod.md`:
"materializar move, nunca cria"). O agregado só guarda contagem, riqueza e saúde somadas e os ids reservados. Faltava decidir como
sortear sem viciar a semente, como o replay reproduz o que o jogador viu e o que "coerente" quer dizer com esse agregado.

## Decisão
1. **Perfil único de materialização** (`MaterializationPrior`/`MaterializationProfile`): a mesma conta serve a prévia do criador e
   a `MaterializationSystem.MaterializeOne` de sempre, então o candidato mostrado é exatamente a pessoa que entra. Sorteios por
   chave fixa por id (`OneShot`: sexo, idade, aniversário, personalidade, ofício), sem stream guardado.
2. **Amostragem condicionada pelo agregado** (vale para toda materialização): riqueza e saúde por cabeça do pool (como antes);
   idade pela pirâmide dos adultos detalhados da cidade (a de um deles, sorteado; uniforme só sem nenhum); ofício pela distribuição dos adultos detalhados da cidade (catálogo só se não há nenhum); cultura predominante deles; quem é mais
   velho que a cidade fundada por outra nasceu na cidade-mãe (`BirthLocation`). Família: **nenhum parente inventado** — mãe, pai,
   cônjuge e casa ficam nulos e os parentes, se existem, seguem no agregado (ele não modela famílias).
3. **Amostra**: `CharacterCreation.Candidates(cidade, número, filtro, N)` filtra o pool inteiro (sexo, faixa de idade, ofício)
   e tira até N (1 a 12, padrão 6) por Fisher–Yates parcial com `OneShot("character-sample|cidade|número")`. Só lê o mundo.
   Cada candidato traz nome, sexo, idade, ofício, cultura, bens, saúde, personalidade, onde nasceu, anos na cidade e se a
   **vontade impede a posse** (`PossessionContest.Prevents`, a mesma regra de encarnar).
4. **Registro**: pedir amostra é o comando `Sample` (número = posição no registro; o replay passa o gravado, com filtro e N);
   entrar é `Create` (cidade, id, número da amostra). Os dois vão ao `players.log` pela fila única (ADR-0048); o replay refaz as
   amostras dadas e reaplica a entrada, com o mesmo hash. Amostras dadas ficam no `PlayerService` (hospedeiro, fora do hash).
5. **Entrar**: refaz a amostra agora; o id tem de estar nela e a vontade não pode impedir; só então `MaterializeOne` (move do pool
   para `WorldState.Npcs`) e `PlayerCommands.Incarnate` em **posse comum** — sem exceção por ser personagem novo. Toda recusa vem
   antes de mexer no mundo: cidade ausente 404; agregado vazio, ninguém no filtro, amostra desconhecida ou desatualizada,
   jogador já encarnado 409; a recusa da vontade é 409 e vai ao registro (como a de encarnar).
6. **Contrato** (só acréscimos, v2): `GET /players/origins`, `POST /players/{id}/candidates`, `POST /players/{id}/create`;
   no canal, `candidates` e `create`. **Cliente**: tela "Pessoa nova" (cidade, filtro, outra amostra, cartões do motor, entrar);
   encaixada na área central abaixo da barra; entrou, assina a cidade e vai à 3ª pessoa (HUD da AD-197p). Nenhum atributo montado no cliente.

## Alternativas consideradas
- Montar atributos no cliente (estilo RPG) — cria gente do nada e viola a conservação; o dono decidiu "pelo motor".
- Sortear a amostra do stream do mundo — pedir outra amostra deslocaria todos os sorteios seguintes (vicia a semente).
- Criar casa e parentes para a pessoa — inventaria gente e casa que o agregado não tem; fica para quando o agregado modelar famílias.
- Posse plena para personagem novo — seria exceção pelo controlador; posse plena segue só do admin.

## Consequências
- **Positivas**: conservação provada (população e riqueza da cidade iguais ao entrar); mesma semente e mesmo registro dão os
  mesmos candidatos e o mesmo hash; a materialização de sempre fica mais coerente (ofício e cultura da cidade).
- **Negativas / trade-offs**: a prévia percorre o pool inteiro por pedido (sob demanda, fora do tick); a pessoa nova chega sem
  parentes detalhados; com a vontade forte, alguns candidatos não deixam entrar.
- **Follow-ups**: agregado com famílias e histórico (fome, guerra) para materializar parentes e memórias (issue aberta pela
  coordenação).
