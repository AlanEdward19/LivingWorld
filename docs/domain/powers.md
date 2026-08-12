# Potência — poderes, magia e o extraordinário

Como mutantes, magos, deuses, aliens e portadores de artefato cabem num modelo só, sem virar
cinco motores paralelos.

## Um modelo, não cinco subsistemas

Mutação genética, magia arcana, dádiva divina, implante tecnológico e artefato alienígena
**não são cinco sistemas** — são a mesma coisa com fontes diferentes. Um poder é sempre um
**modificador sobre um sistema que já existe** (mortalidade, produção, relação, aprendizado,
deslocamento, tempo), nunca um caso especial dentro do motor: cinco subsistemas paralelos
multiplicariam por cinco o trabalho de toda fase futura. Com um modelo só, poder novo é dado,
não código. **Potência** = capacidade de violar uma regra padrão do mundo, com preço opcional.

## Extraordinário é opcional por mundo

Criação de mundo decide se o fenômeno existe: `Extraordinary.Enabled = false` significa zero
portadores, zero aquisição, zero manifestação, zero scheduler de potência, zero placeholder
obrigatório no NPC — o resto do LivingWorld funciona igual. Prevalência (raro a cotidiano) é
conteúdo de cenário, não arquitetura: nunca `if realisticWorld` espalhado pelo motor.

## Os eixos de um poder

| Eixo | O que descreve | Obrigatório? |
|---|---|---|
| Fonte | genética, divina, arcana, tecnológica, alienígena, artefato — string livre de cenário, nunca enum fechado | sim |
| Efeito | qual sistema existente ele modifica, e em quanto | sim |
| Modo | `Passive` (sempre ligado) / `Active` (acionado) / `Triggered` (dispara por condição) / `Conditional` (exige objeto/estado/horário) | sim |
| Custo | debitado por uso, com ou sem sucesso | **não** — `Costs = []` é válido |
| Confiabilidade | `Guaranteed` (determinístico) ou `ResolutionCheck` (rolagem) | sim, mas pode ser `Guaranteed` |
| Modo de falha | o que acontece quando `ResolutionCheck` não dá certo | só se houver `ResolutionCheck` |
| Vulnerabilidade intrínseca | fraqueza que já vem com o fenômeno | **não** — `[]` é válido; ver contramedida abaixo |
| Assinatura observável | evidência que o uso deixa (luz, som, resíduo, alteração fisiológica) | não |
| Consequência social | como quem viu reage | sai da cultura, nunca do descritor |

A fonte quase não importa para o motor, importa para a cultura: curar é o mesmo efeito sobre
saúde seja milagre ou tecnologia, mas quem conta a história reage diferente. **Moedas de
custo, quando existem:** fadiga, saúde, longevidade, sanidade, recurso raro, dívida com uma
entidade, atenção de algo que era melhor não ter notado você. Um ser naturalmente forte não
perde energia extraordinária só para o motor balanceá-lo.

## Rolagem é opcional, custo é opcional

`Guaranteed` executa sem tocar RNG de resolução — invulnerabilidade passiva não precisa rolar
para existir. `ResolutionCheck` usa o RNG semeado do mundo (determinismo vale para potência
como vale para clima e parto):

```
custo cobrado sempre, se declarado
chance = capacidade(portador, poder) - dificuldade(efeito, alvo, contexto)
roll   = ctx.Rng(NpcId).Next()       // stream do portador
resultado: sucesso | parcial | falha -> aplica efeito e/ou modo de falha
```

Falha e consequência são coisas diferentes: um poder `Guaranteed` sem modo de falha pode ainda
ser visto, destruir algo ou revelar uma assinatura — nada disso exige rolagem. Com
`ResolutionCheck`, modos de falha que valem a pena: efeito parcial, alvo errado, custo sem
resultado, exposição pública, dano permanente ao portador, atenção hostil. Falha que não muda
o mundo é falha que não devia ter sido simulada.

## Aquisição, desenvolvimento e manifestação

Como uma pessoa passa a ter poder é regra declarativa de cenário (`PowerAcquisitionRule`),
nunca código: nascimento, predisposição, treinamento, quase-morte, trauma, exposição, item,
ritual, evento histórico — o motor não conhece "cristal que dá poderes", só o cenário declara
item e gatilho; toda aleatoriedade sai de cadeia causal, nunca de `RandomlyGivePower(npc)`.
Desenvolvimento pode ser gradual (`Dormant → Manifesting → Developing → Stable → Mastered`,
estágios de cenário, não enum universal), e **possuir não implica saber** (verdade vs. crença
da Fase 10). Transformação (`ManifestationStateDescriptor`) é **opcional**, nunca fundamento
de potência — super-humano permanente roda com `RequiredState = none`.

## Fraqueza é opcional; contramedida é outra coisa

Vulnerabilidade intrínseca (quando existe) já vem com o fenômeno desde a origem.
**Contramedida é criada, descoberta ou inventada depois** — não é fraqueza secreta esperando
ser achada. Um portador sem `IntrinsicVulnerabilities` continua válido; décadas depois alguém
pode inventar contramedida que reduz sua vantagem sem reescrever o descritor original. Mira
efeito, fonte, custo, condição, equipamento, percepção, identidade ou reputação. Conhecimento
sobre fraqueza ou contramedida é sempre crença: hipótese errada também precisa funcionar.

## Herói e vilão não são campos do NPC; escassez é design

Não existe `IsHero`/`IsVillain`/`Alignment`. Depois de adquirir potência, o NPC escolhe:
esconder, revelar, criar persona, investigar a própria origem — ações no event log. "Herói" e
"vilão" são interpretação social posterior (Fase 23); a reação de quem vê sai da religiosidade,
abertura e autoritarismo de `society.md`, nunca do poder em si. Habilidade nunca é herdada, só
predisposição; e **se todo NPC voa, voar é caminhar** — escassez é design, não balanceamento.
Um portador é candidato natural a NPC detalhado no Simulation LOD; no agregado, uma região
pode reportar "3 portadores conhecidos" sem que nenhum tenha nome.

## Ver também
- [npc.md](npc.md) — atributos e habilidades que a capacidade consulta
- [society.md](society.md) — cultura decidindo a reação ao extraordinário
- [genetics-and-family.md](genetics-and-family.md) — predisposição, nunca habilidade pronta
- [divinity-and-belief.md](divinity-and-belief.md) — potência acoplada a crença
- [simulation-lod.md](simulation-lod.md) — quando um portador vira NPC detalhado
- [history.md](history.md) — uso de poder como evento com consequência
