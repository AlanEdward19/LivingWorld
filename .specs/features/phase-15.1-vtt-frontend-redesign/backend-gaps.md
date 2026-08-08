# Fase 15.1 — gaps do motor para integração visual

Este inventário foi fechado após a aprovação do Estágio 1 em 2026-08-07. Os itens abaixo são o
bloco **E2.0**, executado antes do tick loop e das projeções já planejadas. Cada contrato novo deve
ser determinístico, persistido, reidratável e coberto contra alteração do hash canônico.

## Inventário confirmado no código

| ID | Gap atual | Evidência |
| --- | --- | --- |
| G1 | Mundo não tem nome/identidade no create, e a resposta só traz `NpcCount`. | `WorldCreateEndpoints.cs`; `PresetStart.tsx` |
| G2 | Preview do creator é uma aproximação client-side, separada de `MapGenerator`. | `creatorWorldVisuals.ts`; `MapScenarioLoader.cs` |
| G3 | Cenário aceita assentamento apenas como nome + célula; rotação, ruas e prédios autorados ficam locais. | `MapCell.cs`; `WorldEditor.tsx`; `CreatorCityEditor.tsx` |
| G4 | Cidade não tem nome canônico; prédio não tem posição, orientação, footprint, porta ou materiais. | `City.cs`; `Building.cs`; `buildingFootprint.ts` |
| G5 | Escalas entre espaços são constantes do cliente e `CellCoord` não representa andar/Z. | `space.ts`; `GeographyIds.cs` |
| G6 | Interior não fornece bounds, pisos, paredes, portas, escadas, salas ou células caminháveis. | `InteriorProjector.cs` |
| G7 | NPC não informa prédio/andar/célula interior; ocupação de interior é sempre falsa. | `Npc.cs`; `InteriorProjector.cs` |
| G8 | API só nomeia profissões e skills; demais IDs do creator/inspector não têm descritores. | `PeriodsEndpoints.cs`; `PeriodCatalog.cs` |
| G9 | Consulta de detalhe de NPC materializa estado; não existe leitura detalhada pura. | `NpcInspectionQuery.cs`; `Program.cs` |

## E2.0 — primeiras tasks do Backend

### T42 — Identidade do mundo e handshake de criação

- Aceitar e persistir nome do mundo; retornar `worldId`, revisão/tick e escopo inicial além da contagem.
- Rejeitar nome/JSON inválidos antes de trocar `WorldHost`; provar round-trip e create idempotente na borda.
- Decidir identidade persistente em ADR antes da implementação; toda aleatoriedade usa a seed do mundo.

### T43 — Preview canônico de cenário

- Expor preview read-only por seed, dimensões e cenário usando o mesmo loader/gerador do create.
- Retornar dimensões, células/terreno/água e âncoras exatamente como o create produziria.
- Testar que preview não muta/persiste mundo e que seu hash espacial coincide com o mundo criado.

### T44 — Autoria espacial persistente de assentamentos

- Estender o cenário com ID estável, nome, célula e orientação do assentamento, além de ruas autoradas.
- Aceitar prédios autorados com ID, tipo, posição e orientação; validar overlap, bounds e referências.
- Persistir/reidratar a autoria e definir nomes determinísticos para cidades fundadas pela simulação.

### T45 — Estrutura canônica de cidade e prédio

- Expor nome/bounds da cidade e posição, orientação e footprint por material de cada prédio.
- Marcar explicitamente campos autorados versus derivados; legado pode usar fallback determinístico.
- Porta/entrada deve pertencer ao footprint e permanecer estável entre snapshots, ticks e andares.

### T46 — Hierarquia espacial, escala e andares

- Modelar bounds e resolução de World/City/Building sem constantes exclusivas do cliente.
- Introduzir endereço espacial com andar e contratos de piso, paredes, portas, escadas e caminhabilidade.
- Registrar a decisão em ADR e testar transformações pai/filho e navegação vertical reversível.

### T47 — Ocupação e posição interior de NPCs

- Representar escopo atual do NPC, prédio, andar e célula local sem perder sua localização global.
- Projetar ocupantes em `InteriorSnapshot` e emitir deltas ao entrar, mover, trocar andar ou sair.
- Testar exclusividade de escopo, persistência e determinismo das transições.

### T48 — Catálogo visual legível do período

- Expor descritores estáveis para terreno, bioma, recurso, cultura, local, prédio e ação.
- Cada descritor inclui ID, nome, explicação curta e metadados de faixa/unidade usados pelo creator.
- Derivar dos catálogos do motor; UI não inventa rótulo, limite nem opção inexistente.

### T49 — Detalhe de NPC sem mutação implícita

- Criar projeção de resumo/detalhe que não materializa nem altera hash, pool, tick ou eventos.
- Se materialização for necessária, expô-la como comando explícito e nomeado, separado do GET.
- Testar leitura repetida idempotente e comando com as invariantes atuais de materialização.

## Não são gaps obrigatórios do motor

- Cor de roupa/pele/cabelo do pawn, telhado decorativo, nuvens e microtexturas podem continuar
  derivados de IDs/seed no cliente: são apresentação, não geometria ou verdade simulada.
- Camadas ainda `NotYetModeled` podem integrar como indisponíveis; modelar seu conteúdo é evolução
  de simulação, não pré-requisito para substituir os mocks honestamente.
