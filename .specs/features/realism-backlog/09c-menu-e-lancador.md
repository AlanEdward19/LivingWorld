# Menu de videogame e lançador (AD-174, AD-175)

> Parte do [09](09-cliente-visual-e-jogador.md).

## Pedido do dono em 2026-11-18: jogar com um comando e menu de videogame (AD-174)
"Eu preciso de uma maneira fácil de executar a simulação gamificada (que rode o projeto e o Godot) e o Godot precisa ter o
menu interativo estilo videogame, onde crio meu mundo etc. Fluxo completo, descomplicado, que um jogador de RPG, Dwarf
Fortress, [...] até uma criança consiga entender e amar."

## 3. Menu de videogame e lançador (AD-174): lacunas da API — fechadas no AD-175

O dono liberou acréscimos na API (2026-11-18); tudo só por acréscimo (contrato só cresceu):
- [x] `/periods` e `/periods/{id}` com `card` (nome, época, descrição, pessoas, vilas, ofícios, tecnologia de partida) do bloco
      `Presentation` do período. Os 3 presets são a mesma época: o passo diz isso. Aberto: os períodos de `scenarios/periods/`
      (medieval, pré-histórico…) continuam fora do catálogo do servidor (são cenários de teste, sem `Presentation`).
- [x] `GET /worlds` com `periodId`, `periodName`, `year`, `isCurrent`, `lastPlayedAtUtc` e `origin`; o cliente não adivinha.
- [x] Renomear: `POST /worlds/{id}/rename` (vazio, mais de 40 letras, controle e repetido viram erro em frase).
- [x] "Salvar agora": `POST /world/save` (o mundo é instantâneo a cada 24 ticks); a pausa diz quando salvou.
- [x] Mundo inicial automático marcado (`origin: automatic`); Continuar só o oferece a quem já jogou.
- [x] Criação com etapas reais: `POST /worlds/create/async` + `GET /worlds/create/jobs/{id}`; `/worlds/create` igual.
- [x] `?startRunning=true` em criar e carregar (com o token de admin); sem ele, pausado como antes.
- [x] Sons de interface gerados por código (`Scenes/menu/ui_sounds.gd`), sem asset nem dependência.
