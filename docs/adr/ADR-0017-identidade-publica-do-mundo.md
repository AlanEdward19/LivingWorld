# ADR-0017: Identidade pública do mundo (WorldId) e nome persistido

- **Status**: aceito
- **Data**: 2026-08-12
- **Decisores**: Alan

## Contexto

`POST /worlds/create` (G1, `.specs/features/phase-15.1-vtt-frontend-redesign/backend-gaps.md`,
T42) precisa devolver uma identidade estável do mundo criado e persistir o nome que o usuário
escolhe no World Creator (`PresetStart.tsx` já coleta esse nome, sem lugar nenhum pra mandar).
Hoje `WorldState` não tem nem um nem outro: a resposta só traz `NpcCount`.

O projeto é single-tenant por processo (`WorldHost` guarda uma única instância `Current`,
substituída inteira por `Replace`) — não existe hoje o conceito de "vários mundos" coexistindo,
só "o mundo atual". `BranchId` (ADR-0009) não serve para isso: é identidade de **linha
temporal dentro de um mundo** (Fase 18, ainda não implementada — só `BranchId.Root` existe),
não identidade do mundo em si.

`rules/simulation-determinism.md` proíbe `Guid.NewGuid()` dentro de `Domain`/`Simulation` e
exige que toda aleatoriedade derive da seed do mundo — o mesmo espírito vale aqui mesmo fora
dessas camadas: um id que muda a cada request quebraria o próprio propósito de identidade.

## Decisão

### WorldId é uma função pura da seed, nunca persistido
`WorldId = Guid(SHA256(seed)[0..16])` — hash determinístico, sem consumir nenhum stream de RNG
do mundo (não é `NextCityId`: não avança estado, então rehidratar não o desloca). Vive em
`LivingWorld.Api.WorldIdentity` (não em `Domain`/`Simulation`): é apresentação de fronteira, não
estado que a simulação lê ou decide por ele. Não é campo de `WorldState` — não precisa de
`[Canonical]`/`[Volatile]`, não entra no snapshot, não pode ficar dessincronizado dele.

Consequência aceita: duas criações com a mesma seed produzem o mesmo `WorldId` — dado que só
existe um mundo ativo por vez, isso não colide identidades reais, e é a idempotência de borda
que T42 pede (mesma seed + mesmo nome ⇒ mesma resposta).

### Nome é o único dado novo que precisa de estado
Nome é escolhido pelo usuário e não é derivável de nada existente. Vira propriedade nova em
`WorldState` (`Name`, `string`, default `""`), classificada `[Volatile]` (ADR-0014: cosmético,
nenhuma decisão de sistema lê nome de mundo) — sobrevive a snapshot/reidratação como qualquer
campo volátil, sem entrar no hash canônico.

## Alternativas consideradas

- **`WorldId` como novo campo persistido (Guid gerado uma vez, guardado no snapshot)** —
  rejeitada: exigiria mutar as duas assinaturas de construtor de `WorldState` +
  `WorldSnapshot.Deserialize` só para guardar um valor 100% recomputável da seed já persistida.
  Mais estado para o mesmo resultado.
- **Reexpor `Seed` crua como identidade pública** — rejeitada: mistura um detalhe de
  determinismo/reprodutibilidade (a seed decide o mundo inteiro) com um id de apresentação;
  vaza um dado sensível à reprodução do mundo sem necessidade.
- **`BranchId` como identidade do mundo** — rejeitada: é conceito de ramificação temporal
  (Fase 18), ortogonal a "qual mundo é este"; hoje sempre `Root`, não distingue mundo nenhum.

## Consequências

- **Positivas**: zero mudança em `WorldSnapshot`/construtores para `WorldId`; idempotência de
  borda vem de graça; `Name` segue exatamente o mesmo molde de todo campo volátil existente.
- **Negativas / trade-offs**: `WorldId` não é único entre mundos com seed repetida — aceitável
  apenas enquanto o processo for single-tenant (uma instância `WorldHost.Current`); se um dia
  existir multi-mundo real simultâneo, este ADR precisa ser revisitado (provavelmente compondo
  `WorldId` com algo que distinga instâncias, não só a seed).
- **Follow-ups**: nenhum — T43-T49 (demais gaps de E2.0) não dependem desta decisão.
