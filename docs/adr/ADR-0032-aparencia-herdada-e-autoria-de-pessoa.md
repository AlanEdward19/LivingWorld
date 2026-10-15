# ADR-0032: Aparência herdada opt-in por período e habilidades iniciais autoradas

- **Status**: aceito
- **Data**: 2026-10-15
- **Decisores**: Alan (issue #46, fatia 2)

## Contexto
A aparência do NPC era só do cliente (`npc_actor.gd` sorteava pele e cabelo por `npc_id`); o motor não a conhecia, os filhos não se
pareciam com os pais e o autor não podia escolhê-la. O ADR-0014 classifica traços de aparência como **canônicos** quando alimentam atração.
Tornar isso permanente para todo NPC mudaria o hash de todos os mundos e golden hashes.

## Decisão
- **Genes** (`Appearance`, 4 inteiros em `[0,100]`): `SkinTone`, `HairTone`, `HairCurl`, `Build`. Altura e peso seguem em `Npc.Height/Weight`.
- **Herança** pelo mesmo modelo infinitesimal da personalidade (`Appearance.Inherit`, AD-099): média dos pais + segregação, misturada pela
  `Heritability` do período; com 0 vale o sorteio independente. Fundadores sorteiam por stream próprio `appearance-{id}`.
- **Opt-in por período**: bloco `Appearance { Heritability, AttractionWeight? }`. Sem o bloco, `WorldState.AppearanceRules` é nulo, nenhum
  stream é aberto, `Npc.Appearance` é nulo e some do snapshot, do hash e das rotas (`WhenWritingNull`): hash, goldens e contratos iguais aos de antes.
  Com o bloco os genes são **canônicos** (ADR-0014) e o consumidor é o cortejo: `AttractionFactor.Appearance` (semelhança, peso `AttractionWeight`,
  só quando o cenário já declara pesos de atração). Desligar o bloco muda o hash. Os 3 presets do menu ligam o bloco (herdabilidade 0,6).
- **Autoria**: `AuthoredHouseholds[].Members[]` aceita `Appearance` (4 genes) e `Skills` (`{ "idHabilidade": nível 0–100 }`), validados na borda
  com o campo no erro; ausentes = sorteio (ADR-0024). `Appearance` autorada sem o bloco no período é recusada.
- **Exposição**: `GET /npcs/{id}` (`appearance`), `NpcVisual`, `CityResidentMarker` e `GlobalNpcMarker`; o cliente cai no sorteio atual sem o campo.

## Alternativas consideradas
- Sempre ligada e canônica — quebra a restrição "sem o bloco, hash igual" e todos os baselines.
- Sempre ligada e volátil — sem consumidor causal e contra o ADR-0014.
- Categorias discretas em vez de genes contínuos — herança menos graduada.

## Consequências
- **Positivas**: filhos parecem com os pais (z ≥ 3 em 200 pares); o autor escolhe a aparência; o cortejo passa a ler a aparência.
- **Negativas / trade-offs**: mundo antigo não ganha aparência; o peso de atração só vale com pesos declarados no cenário.
- **Follow-ups**: roupa e acessórios (fatia 3 e #47); aparência no pool agregado ao materializar.
