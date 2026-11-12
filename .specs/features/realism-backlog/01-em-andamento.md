# Fatias em andamento (worktrees, 2026-11-12)

> Cada uma roda num subagente Opus, em worktree próprio, sobre o commit `ee6f0416`. Nenhuma foi juntada ainda.
> Se a sessão cair, o trabalho está no branch do worktree: `git worktree list` e `git log <branch>` mostram o que existe.

## 1. Corpo, crescimento e saúde (AD-134): JUNTADO em 2026-11-12, itens 1 a 4

Ficou pendente o item de doença (só ganchos em `DiseaseProfile`), o contágio por mercado e turma, e o doente acamado. Medido depois: e0 30,3 e 1º ano 244‰ (abaixo da referência anterior, ainda na faixa pré-industrial); só ~2% das mortes têm causa explícita. Um teste de traço neutro foi afrouxado (ver relatório do AD-134 no log de decisões). Números sem fonte: 30% de natimorto quando a mãe morre no parto; desgaste do parto.

Branch: `worktree-agent-af9846256807e40c5`. Achados da auditoria: 8, 12, 14, 3, 5, 15, 16, 20.

- **Corpo que cresce.** Hoje o bebê nasce com 1,70 m e 68 kg e o corpo não muda. Pedido: tamanho de recém-nascido (menor se prematuro), curva de crescimento por idade e sexo, estirão na puberdade (mais cedo nas meninas), perda de estatura e massa na velhice, dimorfismo sexual, estatura herdada dos dois pais com regressão à média, e adulto mais baixo se passou fome enquanto crescia. Músculo sobe com trabalho e cai com desuso e idade.
- **Genética quantitativa.** Traço herdado por média simples perde metade da variância a cada geração; corrigir com o termo de segregação e medir a variância de vitalidade, gene de taxa e estatura por geração.
- **Saúde dinâmica.** Hoje quase nada reduz a saúde e nada a recupera. Pedido: cai com fome, sede, doença, ferimento, parto difícil e velhice; recupera com descanso e comida, mais devagar nos velhos; fraco trabalha menos e anda mais devagar; entra no risco mensal de morte pelo gancho `StateMultiplier`.
- **Amamentação.** O recém-nascido hoje come trigo. Pedido: mama na mãe ou em outra lactante da casa, com custo de comida para ela; sem lactante corre risco alto; desmame gradual; a morte do bebê encurta a amenorreia; se a mãe morre no parto o bebê pode nascer e sobreviver com risco maior.
- **Doença de época ligada por padrão**, com contágio por convivência, mais grave em crianças, velhos e desnutridos, imunidade e surtos. Tem de ser causa das mortes que a tábua já prevê, não mortalidade a mais. Se não couber, fica pendente.

## 2. Agricultura, estações e água (AD-135): JUNTADO em 2026-11-12

Ficou de fora: população mínima 112 em 20 seeds × 100 anos (a meta era 126; a concepção depende da despensa); lobos se extinguem na primeira década; furto de comida não existe (furto é só de moeda e vem desligado), então fome não gera furto; o celeiro carrega sobra de anos; preço de mercado só reage com a loja quase vazia; sem caça, sem frio de inverno, rotina não muda por estação; cenário JSON só tem clima se declarar. Na junção apareceu um defeito latente do diff de snapshot (campo que some não era apagado), corrigido em `SnapshotTreeDiff.cs`.

Branch: `worktree-agent-a9907ed034b4f0ede`. Achados: 6, 19, 21, 22, mais dois conhecidos fora da auditoria.

- **Ano agrícola.** Hoje o trigo amadurece em 24 h e é colhido todo dia, o ano inteiro. Pedido: plantio na estação, crescimento por meses dependente de temperatura e água, colheita em janela curta, nada no inverno, rendimento pré-industrial, anos bons e ruins, quebra de safra ocasional, e a habilidade do lavrador no rendimento (hoje a colheita não lê habilidade).
- **Estoque e perda.** Celeiro, estrago e pragas, reserva de semente. Fome sazonal e de quebra de safra passam a existir, e é aí que esmola, ajuda mútua e furto devem aparecer.
- **Estações e clima com efeito**, ligando o que já existe.
- **Água de beber.** Hoje só existe como mercadoria de mercado, e quem mora a mais de um dia do mercado morre de sede. Pedido: quem mora perto de rio, lago ou poço bebe dali.
- **Fauna e flora.** Animal precisa comer e se reproduz em meses, não de hora em hora.
- Risco principal: a vila continuar se alimentando com colheita anual. Calibrar rendimento e área, não fisiologia.

## 3. Vocação e família (AD-136): JUNTADO em 2026-11-12

Pendências deixadas: irmãos criados separados seguem raros (1,7%; falta criadagem e aprendizagem fora de casa); a pressão da casa pesa pouco na medida (78% contra 69% seguem o ofício de quem criou); população subiu 17% sem explicação; paternidade fora do casamento em 0,2% (lembrança da literatura: ~1% por geração); taxas de caso, descoberta, violência e mágoa sem fonte; não existe "pai social" (o filho de outro pai herda do biológico); sem penalidade de vitalidade por endogamia; o abandono só leva a outro povoado se houver mais de uma cidade.

Branch: `worktree-agent-a19180fe4c086826f`. Achados: 13, 18, 24, 27. Veio de duas reclamações do dono:

- "A profissão nem sempre vai para a pessoa. Meu pai é farmacêutico, minha mãe é enfermeira, minha irmã é designer e tráfego, e eu sou arquiteto de software, e desde pequeno gostei de tecnologia; nem sempre a pessoa pega dos pais, claro que alguns pais podem forçar o filho e isso influencia."
- "'Irmãos criados em casas diferentes não acontecem na simulação', isso também fere a simulação, e divórcio deveria poder ocorrer. Também traição que causa divórcio, ou talvez falta de amor."

Pedido:
- **Ninguém nasce com ofício** (hoje é sorteado no nascimento). Emerge na juventude de: inclinação própria (temperamento, corpo e um componente do indivíduo, em parte herdável), exposição em casa e com o mestre, oportunidade (vaga, salário pago) e pressão de quem criou. A força da pressão é dado de cenário e cultura: alta na vila medieval, baixa em cenário moderno. Quem trabalha no que combina rende e aprende mais; quem foi forçado rende menos e larga quando pode.
- **Falta de amor**: o vínculo do casal cai com ausência, brigas de temperamento, penúria, morte de filho.
- **Traição**: depende de impulsividade, lealdade, vínculo fraco e oportunidade; pode gerar filho de outro pai; pode ser descoberta ou não; leva a separação, violência ou perdão.
- **Separação conforme a época**: mecanismo geral (casa, filhos, bens, quem sai). Medieval: abandono e separação de fato, divórcio formal raro. Moderno: divórcio comum.
- **Depois**: novas uniões, padrastos, meios-irmãos, filhos em casas diferentes; medir irmãos criados separados no mundo rodando.
- Tabu de parentesco além do primeiro grau e o casamento que cria comida do nada, se couber.

## 4. Doença de época como causa de morte (AD-137): PRONTA NO BRANCH, NÃO JUNTADA

Commit `e4351b38` em `worktree-agent-a748c4f038095be74` (sobre `c497e57c`), gate verde lá. A junção sobre a árvore combinada (com agricultura e cognição) foi tentada em 2026-11-12 e **reprovou no gate**, então foi desfeita:
- `VillageSurvivesGenerationsTests` seed 42: só 57 vivos no ano 60.
- `FamilyDiversityCalibrationTests.Vitality_does_not_collapse_at_century_checkpoints`: 19 de 20 seeds abaixo do piso, com vilas de 16 a 17 vivos.
- `OntogenyModelTests.No_npc_acts_above_its_development_across_ten_years`: 1 violação (suspeita: criança escolhida como cuidadora ou ação de doente; conferir `SickBehavior`).

Causa provável: a árvore combinada já mata demais (item 20 de [08](08-a-investigar.md)) e a doença soma. **Recalibrar a mortalidade da árvore combinada antes de juntar.** Conflitos da junção são simples: `DemographyReport.cs` (unir os tallies), `ScenarioRunner.cs` (`diseaseRules ?? DiseasePresets.PreIndustrial` na linha do HEAD), pins e baselines `--ours`. Ao juntar, ligar o gancho da cognição: em `OutbreakSystem`, depois do `ReplaceInfection` do sintoma, `if (bedridden) LifeEventIntake.OnFellIll(world, npc, now);`. Em aberto na própria fatia: fome subiu de 1,0% para 6,8% das mortes sem causa achada; números de época sem fonte; +6% de tempo por NPC-tick numa medição só.

Branch: `worktree-agent-a748c4f038095be74`, sobre `c497e57c`. Pedido em [06](06-violencia-poderes-e-restantes.md), seção "Doença de época": perfis de doença como dado de cenário, contágio pela convivência real, doente acamado que precisa de cuidado, e a doença como causa das mortes que a tábua já prevê (recalibrar a mortalidade-base).

## 5. Memória, emoção e pensamento real (AD-138): JUNTADO em 2026-11-12

Ficou de fora: necessidades de segurança, pertencimento e descanso; maturação da personalidade com a idade; raiva só leva a evitar, não a confrontar; memórias de jogador, LLM e crença ainda entram no rastro por busca de texto; gancho `OnFellIll` sem chamador (ligar na junção da doença). Ajuda dada e recebida ocupam 54% das 12 vagas de lembrança.

Branch: `worktree-agent-a79d82224d8995144`, sobre `c497e57c`. Pedido em [03](03-cognicao-e-cerebro.md), parte 1: memória episódica, emoção e luto, emoção e memória mudando a ação, rastro cognitivo como motivo real (teste de intervenção), `Socialize` só com alguém. Rede neural fora do escopo.

## Pausa de 2026-11-12 (o dono desligou o computador)

As fatias 2, 4 e 5 foram interrompidas no meio e retomadas no mesmo dia; a 5 já foi juntada. Na hora da pausa nenhuma tinha commit. O trabalho ficou em dois lugares:

- nos arquivos do worktree (`.claude/worktrees/agent-<id>`), sem commit;
- numa cópia de segurança em `refs/wip/<id>` (inclui arquivos novos). Recuperar num worktree limpo do commit-base: `git stash apply refs/wip/<id>`.

| Fatia | id | Base | Tamanho na pausa | Onde parou |
|---|---|---|---|---|
| 2 Agricultura (AD-135) | `a9907ed034b4f0ede` | `ee6f0416` | 49 arquivos | esperando comando pesado na fila (validação) |
| 4 Doença (AD-137) | `a748c4f038095be74` | `c497e57c` | 34 arquivos | esperando comando pesado na fila (validação) |
| 5 Cognição (AD-138) | `a79d82224d8995144` | `c497e57c` | 31 arquivos | esperando a fila pela primeira vez |

Para retomar: continuar cada fatia a partir do worktree (ou do `refs/wip`), rodar o gate dela pela fila, e só então juntar. A fatia 2 parte de um commit mais antigo: vai conflitar com AD-134 e AD-136. Os scripts de junção que estavam em pasta temporária foram copiados para `relatorios/` (`junta-e-valida.sh`, `record-and-verify.sh`); os caminhos dentro deles apontam para a pasta temporária antiga e precisam ser ajustados.

## Segunda pausa de 2026-11-12 (o dono desligou o computador de novo)

Estado do branch `perf/decision-scan-costs` na pausa: juntadas e com gate verde as fatias 1, 2, 3 e 5; a 4 (doença) pronta no branch dela, não juntada (ver acima).

**Hormônios, ciclo menstrual, adolescência, doença genética e contracepção (AD-139)**: interrompida no meio da validação. Worktree `.claude/worktrees/agent-ae04b3952ab6b2914`, branch `worktree-agent-ae04b3952ab6b2914`, sobre `70adcd30` (antes da agricultura): 37 arquivos alterados, **sem commit, sem gate**. Cópia de segurança em `refs/wip/ae04b3952ab6b2914` (`git stash apply` num worktree limpo de `70adcd30`). Pedido em [02](02-biologia-e-hormonios.md).

Ordem sugerida na volta: (1) recalibrar a mortalidade da árvore combinada (item 20 de [08](08-a-investigar.md)); (2) juntar a doença; (3) terminar e juntar hormônios; depois seguir a tabela do [README](README.md). O dono pediu pausa depois dessas: não disparar fatia nova sem ele dizer.

## Ao juntar

Conflitos esperados: `Npc.cs` e `NatalitySystem.cs` (fatias 1 e 3), `ScenarioRunner.cs`, goldens e baselines (todas). As três mudam o equilíbrio da vila: juntar uma de cada vez e medir a demografia de 100 anos na árvore combinada.
