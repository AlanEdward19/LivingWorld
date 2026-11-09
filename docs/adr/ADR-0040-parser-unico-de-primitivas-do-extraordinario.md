# ADR-0040: Parser único por primitiva do extraordinário (carga e invocação)

- **Status**: aceito
- **Data**: 2026-11-09
- **Decisores**: Alan (issue #64; decisão do dono após 8 rodadas de divergência carga x invocação)

## Contexto
A carga do cenário valida efeitos e custos por uma regra (`Syntax`) e a invocação os lê por outra (cada `Mechanic`, `AreaTargetResolver`).
Oito rodadas de verificação acharam a mesma classe de bug: a carga aceita o que toda invocação recusa (`area:radius:fire:3`) ou recusa o que a invocação aplica
(`matter.transmute :1:2:3`, `foresight.preview: `, `movement.flight:Infinity`). Cada correção ajustava um dos dois caminhos e abria outra fresta.

## Decisão
- **Um parser por primitiva**, tipado e sem efeito: `ExtraordinaryPrimitive<T>.Parse(token) : Result<T>`. A carga (`Matches`/`Accepts`) e a invocação
  (`PrepareEffect`/`PrepareCost`) chamam esse mesmo método. Leitores de efeito dos sistemas (`GravityMechanic`, `AttributeMechanic`, `SkillMechanic`, `LuckMechanic`,
  `FloraMechanic`, `AreaTargetResolver`) também. A invocação fica só com o que depende do mundo (NPC, Fact, saldo, habilidade, alcance, mapa).
- **A carga aceita exatamente o que a invocação aceita sintaticamente.** A única redução fora do parser é a forma tipada `chave:tipo:n`
  (`TrySplitTypedMagnitude`, a mesma do motor), e só onde o motor a aplica: `AllowTyped = false` para seletor de área, que o motor lê cru.
- **Paridade de invocação**: nenhum token aceito hoje muda de comportamento (exceto a emenda de valores não finitos abaixo); nenhum golden/baseline muda. Mensagens de erro da invocação foram mantidas.
- **Catálogo**: `Listing` lista o que um editor deve oferecer. Um parser sem listagem é um token **tolerado**.

## Valores não finitos (emenda: exceção à paridade)
Regra: **nenhum valor não finito entra em estado canônico**. JSON não representa infinito nem NaN e `WorldSnapshot.CanonicalHash` lançava `ArgumentException` depois que um entrava.
- **Recusados no parser** (carga e invocação), porque o valor chega ao estado: `gravity.target` (`GravityTargetMultiplier`), `environment.temperature` (delta, inclusive `NaN` e estouro de `float` como `1e39`)
  e `skill.learn-rate` (no teto o ganho é 0 e `0 × infinito` grava `NaN` na habilidade). É mudança do que a carga aceita, feita de propósito: esses tokens quebravam o hash do mundo, então nenhum cenário funcional dependia deles.
- **Saturado no leitor**: o produto de `skill.learn-rate` para em `double.MaxValue` a cada passo, porque fatores finitos (`1e308 × 1e308`) também estouram. Não há token a recusar nesse caso.
- **Seguem aceitos** porque só alimentam valor volátil ou inteiro, nunca estado canônico: `movement.flight|speed-multiplier:Infinity`, `gravity.self:Infinity`, `flora.growth-rate:Infinity`, `attribute.*:Infinity`.
- Recusar foi preferido a saturar no parser: saturar inventaria um significado para "infinito" (`1,8e308` guardado) em vez de avisar o autor na carga.

`ExtraordinaryLocomotion` paga custos de movimento pelos mesmos parsers de custo e aplica a mesma redução tipada do motor (`carrier.hunger:fire:5` debita `5 × vulnerabilidade`).

## Lixo tolerado (paridade com ressalva)
A invocação aceita sem efeito, e portanto a carga também aceita:
- `movement.<qualquer coisa>` (inclui `movement.flight` sem valor e `movement.flight:Infinity`): `MovementEffectMechanic` devolve sucesso sem ler o argumento
  e `ExtraordinaryLocomotion` usa qualquer `movement.*` como ativador de custo. Os valores de `flight`/`speed-multiplier` seguem lidos pelo parser tipado (positivo, infinito aceito).
- `attribute.fertility|strength|perception|reaction-speed` com valor ausente ou inválido (`attribute.strength:abc`): só a chave antes do último `:` conta; o leitor ignora o valor.
- `area:<outra coisa>` sem forma de seletor (`area:radius`, `area:foo:3`): sem efeito. Com forma de seletor (`area:radius:`, `area:region:`) exige `n` inteiro >= 0, cru.
- `dimension.pocket-store:<qualquer>`: argumento ausente, inválido ou não positivo move 1 unidade.
- `npc.teleportx:5`, `combat.strike:8:1` e similares: mecânicas que só leem a magnitude e não conferem a chave.

## Alternativas consideradas
- Fazer a invocação recusar o lixo — muda comportamento de token aceito hoje; fora da regra de paridade.
- Manter duas regras e testá-las por diferencial — foi o que gerou as 8 rodadas.

## Consequências
- **Positivas**: divergência carga x invocação deixa de ser possível por construção; o catálogo de #46 vem dos mesmos parsers.
- **Negativas**: o lixo tolerado acima passa na carga; recusá-lo é decisão de produto futura (mudaria a invocação).
- **Follow-ups**: rota `GET /authoring/extraordinary/primitives` (#46) e eventual endurecimento do lixo tolerado.
