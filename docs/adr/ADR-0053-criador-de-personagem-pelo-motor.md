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

## Emenda do dono em 2026-11-24: criador, não escolhedor (AD-197t, #219)
O dono corrigiu: "é um criador e não um escolhedor, deveria dar para escolher idade, atributos (caso queira), nome, cidade, famílias
(...) ou ser recém chegado, dinheiro inicial, aparência completa". A amostra do agregado **continua** ("sortear alguém da cidade"); a tela
passa a **montar** a pessoa. Vale o que está acima, mais:
- **Escolhas** (`CharacterSpec`): nome, sexo, idade (18 até a longevidade − 1), cidade, ofício, saúde, os 10 traços, dinheiro inicial e
  aparência (altura, pele, cabelo, ondulação, corpo, olhos, rosto, traje). O que ficar em branco o motor sorteia por `OneShot`
  (`character|jogador|semente`: nenhum stream guardado mexe; a semente é escolha registrada, "sortear de novo" a troca).
- **Aparência é estado do corpo**: `Appearance` ganha `EyeTone`, `FaceShape`, `Outfit` e `BodyFrame` (nulos nos demais NPCs, omitidos do
  snapshot); a altura vira estatura adulta (`SetStature`) e o arcabouço escolhido fixa o peso no crescimento mensal.
- **Dinheiro** entra de fora (`Mint` `player-character`, contado em `MoneyMinted`): de 0 a 5× o morador típico da cidade (mín. 500).
- **Lugar na cidade**: recém-chegado (migrante: `ArrivedAtTick`, sem casa, evento `CharacterArrived`; vindo de fora ou de outra cidade)
  ou família: filho (pais = o parente e o cônjuge vivo), irmão (os mesmos pais; sem pais no registro não há irmandade), cônjuge (livre,
  adulto e que aceita pela regra do cortejo; `MarriageSystem.Marry` de verdade) ou parente agregado à casa (sem laço genealógico).
  Filho e irmão exigem pais que podiam ter gerado a pessoa (janela fértil do mundo, vivos ao nascimento): `CharacterJoinedFamily`.
- **Validação antes de tudo**: idade fora da longevidade, laço impossível, cônjuge ocupado ou que não aceita, cidade inexistente, dinheiro
  ou altura fora da faixa → 409 com o motivo, sem registro e sem efeito. A vontade que impede a posse segue sendo recusa registrada.
- **Registro**: comando `Character` no `players.log` com a escolha inteira; o replay refaz a pessoa e dá o mesmo hash.
- **Contrato** (v2, só acréscimos): `GET /players/origins/{cityId}`, `POST /players/{id}/character/preview` (só lê) e
  `POST /players/{id}/character`; canal `character`. **Cliente**: campos, famílias vindas do motor e prévia do corpo pelo estado devolvido.

## Emenda t2 (AD-197t2, #219): parentes genealógicos, irmão de fundador e menor de idade
- **Neto, sobrinho, tio, primo** (laço de sangue pelos pais registrados, `KinIndex`): o jogador escolhe o parente e por quem o laço passa
  (`ViaId`). Neto = filho de um filho do parente; sobrinho = filho de um irmão dele; primo = filho de um irmão do pai ou da mãe dele;
  tio = irmão do pai ou da mãe dele (divide os pais dessa pessoa). Quem faz de pai/mãe tem de viver na cidade e ter podido gerar a pessoa
  (janela fértil, vivo ao nascimento); a pessoa entra na casa dele e herda nome de família, aparência e estatura dos pais.
- **Irmão ou tio de quem não tem pais registrados: recusa com motivo**, a opção mais realista. Criar pais mortos exigiria Npcs fantasma
  (herança, linhagem, nomes, hash) e editar o registro do fundador: seria inventar antepassados, contra "materializar move, nunca cria". A
  tela mostra o motivo; com gerações no mundo o laço passa a existir.
- **Menor de idade** (de 6 a 17 anos) como filho, neto, sobrinho, primo ou irmão, só em casa com adulto vivo que cuide; sem ofício nem
  dinheiro próprios; altura na faixa de quem cresce. Vive pelas regras de qualquer criança (ontogenia, crescimento, dependência da casa).
  Fora: menor de 6 (lactente), tio, cônjuge e recém-chegado menores.
- **Custo** (`CharacterCreatorCostTests`, cidade de 10.000): `GET /players/origins/{cityId}` 10 ms e prévia 2 ms (medianas de 3); teto
  declarado 250 ms e 50 ms; a lista tem no máximo 120 famílias e 8 parentes de passagem por laço.

