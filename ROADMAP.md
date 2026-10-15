# ROADMAP — Living World

Índice das fases. **Carregue só o arquivo da fase em que está trabalhando.**
Cada `docs/roadmap/phase-NN-*.md` tem: objetivo, tasks acionáveis, critérios de
verificação (o que o gate precisa provar) e o que fica **explicitamente fora**.

## Objetivos técnicos (a régua)
| # | Objetivo | Fecha na fase |
|---|---|---|
| 1 | 100 NPCs numa vila medieval por **100 anos sem LLM**, com famílias, profissões, economia, nascimentos, mortes e habilidades preservados | 7 |
| 2 | Selecionar qualquer NPC vivo e ver identidade, família, profissão, atributos, rotina e memórias | 8 |
| 3 | Jogador conversa com NPC via LLM, **sem** que a LLM altere o mundo | 11 |
| 4 | Cidades crescem, encolhem e dão origem a novos assentamentos | 8 |
| 5 | Selecionar um NPC detalhado e ver o motor de decisão dele — estímulo, ponderação, decisão — em dados e em visual | 28 |

## Trilhas
O número é **identidade**, não ordem. Só a trilha núcleo é sequencial.

| Trilha | Fases | Regra |
|---|---|---|
| **Núcleo** | 0–8 | Caminho crítico. Sequencial. Fecha os 4 objetivos. Nada entra na frente |
| **Escala** | 9 | Logo depois da 8, antes de qualquer trilha nova. Teto de custo por NPC-tick e por byte armazenado — toda fase seguinte adiciona sistema e estado sobre esse teto |
| Clientes | 14, 15 | Depois da 9. Independentes entre si |
| Mundo vivo | 10–13 | Depois da 9. História, LLM, narrativa, períodos |
| Extraordinário | 16–20 | Depois da 9. Potência → divindade → tempo → cosmos → trânsito |
| Realismo humano | 21–23 | Depois da 7. Ontogenia, imperfeição, intriga |
| Sistêmicas | 24–26 | Emergência exige 13; jogadores exige 11; console exige 18 |
| Cognição | 28 | Depois da 16 (reaproveita LOD/conservação/potência). Independente de 17–27 |
| Espectador | 27 | Última. Cinemática exige 10 (relato) e 12 (ancoragem) |

## Fases
| # | Fase | Entrega | Status |
|---|---|---|---|
| 0 | [Fundação](docs/roadmap/phase-00-foundation.md) | Solution, camadas, gate verde, testes de arquitetura | fechada |
| 1 | [Motor de tempo](docs/roadmap/phase-01-time.md) | Calendário, ticks, scheduler, pausa/velocidade, snapshot, hash | fechada |
| 2 | [Geografia mínima](docs/roadmap/phase-02-geography.md) | Regiões, células, terreno, bioma, recursos, custo de deslocamento — só dados | fechada |
| 3 | [População básica](docs/roadmap/phase-03-population.md) | NPC, idade, sexo, saúde, família, nascimento, morte | fechada |
| 4 | [Necessidades e rotina](docs/roadmap/phase-04-needs.md) | Fome, sono, trabalho, moradia, deslocamento, utility AI | fechada |
| 5 | [Economia](docs/roadmap/phase-05-economy.md) | Recursos, produção, estoque, consumo, emprego, salário, preço | fechada |
| 6 | [Habilidades](docs/roadmap/phase-06-skills.md) | Experiência, ensino, profissões, progressão | fechada |
| 7 | [Relações e famílias](docs/roadmap/phase-07-family.md) | Confiança, atração, casamento, reprodução, hereditariedade | fechada |
| 8 | [Cidades](docs/roadmap/phase-08-cities.md) | Crescimento, edifícios, migração, fundação de assentamentos, inspeção por CLI/API | fechada |
| 9 | [Escala e armazenamento](docs/roadmap/phase-09-scale.md) | Custo por NPC-tick, decisão por evento, decaimento preguiçoso, snapshot delta/binário, arquivo frio de mortos, sensor de escala | fechada |
| 10 | [História degradável](docs/roadmap/phase-10-history.md) | Fato, relato, distorção por transmissão, cânone limitado, verdade vs crença | fechada |
| 11 | [Interação com LLM](docs/roadmap/phase-11-llm.md) | Contexto, diálogo, validação, memória, relação | fechada |
| 12 | [Narrativa](docs/roadmap/phase-12-narrative.md) | Resumos, jornais, rumores, biografias, crônicas | fechada |
| 13 | [Múltiplos períodos](docs/roadmap/phase-13-periods.md) | Módulos de conteúdo: pré-histórico, moderno, futurista, criaturas | fechada |
| 14 | [Unreal](docs/roadmap/phase-14-unreal.md) | Cliente 3D, personagens, voz, animação | substituída pelo Godot (ADR-0025); voz e Unreal adiados |
| 15 | [Mapa visual](docs/roadmap/phase-15-map-visual.md) | Cliente React+TS VTT 2D realtime: camadas + espectador + FOW | fechada (servidor/FOW/tokens prontos; UI é o Godot, ADR-0022; telas em #58/#59) |
| 16 | [Potência](docs/roadmap/phase-16-powers.md) | Extraordinário opcional por mundo: modificador unificado, custo/rolagem/fraqueza opcionais, aquisição declarativa | parcial (efeitos e custos ainda em switch C# fechado; reabertura pendente, #64) |
| 17 | [Divindade](docs/roadmap/phase-17-divinity.md) | Deuses, culto, economia de crença, cisma, deus falso, ser cultuado ≠ ganhar poder | adiada (depois de 18/27) |
| 18 | [Linhas temporais](docs/roadmap/phase-18-timelines.md) | Ramificação, inércia histórica, âncora e coleta de branch | adiada (4ª; destrava #62 e 20) |
| 19 | [Cosmos e contato](docs/roadmap/phase-19-cosmos.md) | Sistema estelar, órbitas, aliens como cultura, colônias | adiada (por último) |
| 20 | [Trânsito interdimensional](docs/roadmap/phase-20-interdimensional.md) | Volta à linha de origem, catch-up preguiçoso de branch dormente | adiada (por último; exige 18) |
| 21 | [Ontogenia](docs/roadmap/phase-21-ontogeny.md) | Nascer sem saber nada, marcos, exposição, janelas críticas | fechada (21a–c entregues AD-115..117; dívida no plano `.specs/features/phase-21-ontogeny/plan.md`) |
| 22 | [Imperfeição](docs/roadmap/phase-22-imperfection.md) | Defeitos, doenças, moral emergente, orientação e ocultação | próxima (2ª, depois da 21) |
| 23 | [Intriga](docs/roadmap/phase-23-intrigue.md) | Segredo, persona/identidade, chantagem, traição, divulgação, rumor, investigação, conspiração | próxima (3ª, depois da 22) |
| 24 | [Emergência aberta](docs/roadmap/phase-24-emergence.md) | Raças, tecnologias, ideologias, potências e contramedidas novas por composição | parcial (tecnologia de mobilidade por composição entregue, AD-110; faltam raças, ideologias, potências, contramedidas, doença, nomeação, #61) |
| 25 | [Jogadores](docs/roadmap/phase-25-players.md) | Encarnação, offline vira desaparecimento, espectador, multiplayer | parcial (encarnar/agir/logout + procura entregues, AD-111; faltam abandono declarado, retorno tardio, espectador, multiplayer/autenticação, paridade de potência, #63) |
| 26 | [Console e modo god](docs/roadmap/phase-26-console.md) | Pausa, velocidade, busca, eventos marcantes, reescrita + rebuild | parcial (pausa com teto, velocidade, busca de NPC e auditoria entregues, AD-112; faltam índice de entidades/eventos, visão de Verdade no console, reescrita/diff — bloqueados pela Fase 18, #62) |
| 27 | [Motor cinematográfico](docs/roadmap/phase-27-cinematics.md) | Assistir a uma vida: texto → 2D → caminho para 3D | adiada (5ª; exige 10 e 12) |
| 28 | [Cognição e LOD observacional](docs/roadmap/phase-28-cognition.md) | Motor de decisão inspecionável (estímulo→decisão) com painel "ver o cérebro", compressão de estado frio, LOD por três escopos de observação (mundo/cidade/interior) | parcial (API e LOD prontos; falta o painel visual COG-11 no cliente Godot, #58) |

## Regras do roadmap
- Uma fase só fecha com `bash scripts/verify.sh` em 0 **e** os critérios da fase provados
  por teste — não por inspeção visual.
- Fase fechada → commit `feat(phase-NN): <resumo>` (ver política em `AGENTS.md`).
- Mudou o escopo de uma fase? Atualize o `.md` dela e registre em `STATE.md`. Se a mudança
  for de arquitetura ou dependência, é ADR.
- **Objetivo #1 é a régua.** Se uma task não aproxima o mundo de rodar 100 anos coerente,
  ela provavelmente pertence a uma fase posterior.
- Caminho crítico: **0–8**, incluindo a 2 — que agora é só a geografia mínima que a Fase 8
  consome. A 7 fecha o objetivo #1; a 8 fecha os objetivos #2 e #4.
- 10–15 podem deslizar sem travar objetivo nenhum. O cliente web foi para a Fase 15 e o
  objetivo #2 é atendido por CLI/API na Fase 8.
- **A Fase 9 não desliza.** Ela não fecha objetivo, mas fixa o teto de custo (tempo por
  NPC-tick e bytes por NPC vivo) que toda fase seguinte gasta. Adiar é pagar o mesmo
  trabalho depois, sobre o dobro de sistemas.
- **Ordem decidida (2026-10-14, #66): 21 → 22 → 23, depois 18, 27, 17/19/20.** Fora dessa ordem exige nova decisão.
- **Status `spec` significa bloqueada.** Fases 17–27 têm objetivo, tasks e intenção de
  critério, mas os critérios finais só são escritos sob `rules/eval-criteria.md` quando a
  fase é ativada — e nenhuma delas começa antes da Fase 8 fechar. Ativar uma fase `spec`
  fora de ordem é decisão de escopo: registre em `STATE.md` antes.
- **A performance vira fase própria (9), não um "revisitar" ao fechar a 8.** A Fase 4 já
  achou reflection no caminho quente (`PersonalityWeighting`, AD-038 em
  `docs/decisions-log.md`) e Economia/Habilidades/Família/Cidades (5–8) somam sistemas
  Hourly/Daily/Monthly que iteram população — o custo medido dessas fases é a entrada da 9.
