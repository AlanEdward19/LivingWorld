# Fase 15 — Mapa visual VTT 2D

**Objetivo**: transformar o mundo já existente em uma visualização 2D estilo VTT com
**dois modos**: (1) espectador/admin com visão global viva do mundo e (2) jogador em
personagem com visão limitada por conhecimento e fog of war. Tudo em tempo real, sem
criar segunda fonte de verdade: o motor continua dono do estado.

## Tasks
1. **Read model espacial + eventos visuais**: projetar mundo/cidade/interior para leitura
   (`mundo → região → cidade → distrito → prédio → interior`) e incluir presença/atividade
   de NPC e eventos observáveis (conflito, trabalho, deslocamento, interação).
2. **Canal realtime**: expor stream por `WebSocket`/SSE com snapshot inicial + deltas
   ordenados por tick para mapa global, cidade e interior; reconexão recebe catch-up
   determinístico sem reescrever estado do mundo.
3. **Resolução por foco da tela (freeze de escopo visual)**: no mapa-múndi, transmitir visão
   simplificada (cidades + NPCs externos por LOD); ao entrar na cidade, subir detalhe da
   cidade focada; ao entrar em prédio/interior, subir detalhe local máximo.
4. **Modo espectador/admin**: mapa-múndi animado com cidades visíveis, NPCs fora de cidade
   como pontos coloridos e marcadores de eventos; drill-down contínuo até interior.
5. **Modo personagem (jogável)**: movimento point-and-click e W/A/S/D, limitado ao que o
   personagem conhece; fog of war por célula/ambiente visitado, com override administrativo.
6. **Interiores e atividade**: entrar/sair de casas e prédios, renderizar entidades e
   atividade em andamento (trabalho, conversa, conflito, deslocamento) com sinais visuais.
7. **Aparência inicial de NPC (token 2D)**: cada NPC usa um token circular composto
   dinamicamente (SVG/camadas equivalentes) a partir de biblioteca de partes visuais
   versionada (pele, cabelo, roupa, profissão/acessório, variações por idade/estado), com
   mapeamento determinístico derivado do estado canônico do NPC.
8. **Contrato cliente-servidor**: comandos de input do jogador viram intenção validada no
   servidor; tipos TS seguem OpenAPI gerado; scripts `build/lint/test/verify` cobrem backend
   + web no mesmo gate.

## Critérios de verificação
- **Sem escrita pelo canal visual**: para todos os endpoints/handlers de visualização e
  stream enumerados por reflexão, chamadas de leitura e subscribe/unsubscribe mantêm hash
  canônico idêntico antes/depois.
- **Realtime cobre atividade real**: para cada atividade/evento declarado no catálogo visual,
  o teste injeta cenário determinístico e exige delta emitido + renderer registrado. Item
  novo no catálogo sem emissão ou render reprova.
- **Modo espectador vê o mundo inteiro**: no mesmo tick, espectador recebe cidades, NPCs
  externos e eventos ativos sem filtro de descoberta.
- **Modo personagem respeita conhecimento**: com mesma seed e mesma posição, jogador só recebe
  células/interiores descobertos; admin override libera visão total sem alterar estado social
  ou econômico do mundo.
- **Escopo visual sobe e desce por foco**: quando o cliente muda mapa-múndi → cidade → interior,
  o stream muda de resolução exatamente nesses níveis; ao sair, rebaixa resolução sem pausar o
  avanço global do mundo.
- **Entrada em prédio e interior são navegáveis**: para todos os prédios acessíveis do cenário
  de teste, fluxo exterior → interior → exterior preserva identidade de entidades e contexto.
- **Token visual é estável e reprodutível**: com o mesmo snapshot + seed, o mesmo NPC recebe o
  mesmo token em qualquer cliente; mudança de estado relevante (ex.: faixa etária, profissão,
  condição física) altera somente as camadas previstas no catálogo visual.
- **Entrada de movimento é causal e validada**: comandos de movimento inválidos são rejeitados
  com erro explícito e hash inalterado; comandos válidos produzem mudança espacial observável
  no stream do próprio personagem.

## Fora do escopo
Cliente 3D, voz, lip sync e pipeline Unreal continuam na Fase 14 (adiada). Esta fase não
introduz mecânica social/econômica nova: só expõe visualmente o que o motor já produz.
Qualquer ausência de mecânica revelada pela UI volta como task da fase dona da mecânica.

## Ver também
[phase-08-cities.md](phase-08-cities.md) ·
[phase-14-unreal.md](phase-14-unreal.md) ·
[world-map.md](../domain/world-map.md) ·
[simulation-lod.md](../domain/simulation-lod.md) ·
[rules/simulation-determinism.md](../../rules/simulation-determinism.md) ·
[rules/eval-criteria.md](../../rules/eval-criteria.md)
