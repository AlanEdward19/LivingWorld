# Regras comuns para as fatias de realismo do LivingWorld

Diretriz do dono (palavras dele): "toda a existência de um npc e tudo que ele faz deve mirar na realidade da vida real, em todos os aspectos, até na tomada de decisão"; "quero o mundo totalmente simulado"; tudo determinístico e dirigido por simulação, nada escrito à mão. A única coisa que pode fugir da realidade são os poderes. O cenário padrão é uma vila medieval/pré-industrial.

## Antes de mexer
- Leia AGENTS.md, rules/simulation-determinism.md, rules/tests.md, rules/eval-criteria.md e rules/living-world-cohesion.md.
- Confira a base: `git log --oneline -1` deve mostrar o commit indicado no seu pedido; se não, reposicione o seu branch para ele antes de começar.
- Leia na auditoria (relatorio-realismo.md, na mesma pasta deste arquivo) a tabela-resumo e o detalhe dos achados da sua fatia. Confirme cada achado no código antes de agir: a auditoria foi feita antes de várias mudanças.

## Engenharia
- Aleatoriedade só do RNG semeado do mundo: chave que nunca se repete usa `ctx.OneShot(chave)`; sorteio por entidade e tick usa `ctx.StreamFor(propósito, id)`; `ctx.Rng(chave)` guarda o stream no snapshot para sempre e só serve para chave que se repete.
- Proibido em `LivingWorld.Simulation`: `Random`, `DateTime.Now`, GUID aleatório, e `Math.Pow/Exp/Log/Sin/Cos` da plataforma (use `DeterministicMath` em src/LivingWorld.Domain/Shared ou multiplicação; um teste reprova o uso direto).
- Nunca itere `Dictionary`/`HashSet` para produzir efeito no mundo: ordene por id. O mundo reaberto de um snapshot tem de se comportar igual ao que seguiu rodando.
- Todo número é dado de cenário, com padrão na própria classe de regras em `src/LivingWorld.Domain`. `src/LivingWorld.Simulation/Scenarios/ScenarioRunner.cs` é ponto de conflito entre agentes: mexa o mínimo (uma linha por regra) e liste no relatório exatamente o que mudou lá. Cenários JSON herdam o padrão sem declarar campo novo. Estado novo precisa de valor padrão para snapshot antigo abrir.
- Custo: nada de laço novo por NPC por tick. Use o agendador de eventos ou cadência diária/mensal. Meça `bash scripts/profile.sh --pop 1000` e `--pop 5000` antes e depois (µs e bytes por NPC-tick); a alocação é exata, o tempo é ruidoso se outro agente estiver rodando: repita.
- Todo sistema novo entra conectado a um consumidor causal real (rules/living-world-cohesion.md): diga no relatório onde o efeito é consumido.
- Números do mundo real: use o meio das faixas mais aceitas, deixe parametrizado e liste no relatório final cada número com a fonte que você lembra e a marca "precisa de conferência". Não invente referência.

## Testes e gate
- Cada comportamento novo tem teste que falha no comportamento antigo.
- O mundo vai mudar: regrave de propósito os goldens (`bash scripts/refresh-world-hashes.sh`) e os baselines que divergirem. Procure `LW_RECORD_` e `ZZZ_record` em tests/. Família: `LW_RECORD_FAMILY_DIVERSITY=1 bash scripts/test.sh --project longrunning --scenarios --filter "FullyQualifiedName~Record_explicit_family_diversity_baseline"`. Civilização: `bash scripts/civ-report.sh` e copiar `summary.diversity` para `Gate` em tests/baselines/civilization-diversity.json.
- Teste existente que passe a falhar: leia o que ele verificava. Se dependia do comportamento irreal, reescreva mantendo a intenção e explique no relatório. Nunca apague nem afrouxe calado; todo afrouxamento vai listado no relatório com o motivo.
- A vila tem de continuar viável: `bash scripts/demography-report.sh 1 20 100 10` (20 seeds × 100 anos, leva dezenas de minutos) antes e depois, sem extinção. Referência atual: população final ~168 (mín 126), natalidade ~34‰, mortalidade ~29‰, e0 ~33, 1º ano ~212‰.
- Rode tudo pelos scripts (`bash scripts/build.sh`, `bash scripts/test.sh [--filter ...]`, `bash scripts/lint.sh --fix`, `bash scripts/verify.sh`, `bash scripts/verify.sh --full`, que leva 15 a 40 minutos: rode em segundo plano e espere).
- Docs: todo doc/rule ≤100 linhas. Registre a decisão numa linha nova no fim de docs/decisions-log.md com o número AD indicado no seu pedido e atualize o doc de domínio que descrever o comportamento antigo. Não edite STATE.md.
- Não faça push, não abra PR, não escreva em issues.
- Commit no branch do seu worktree só com `verify.sh --full` saindo 0, mensagem `feat(realism): ...` terminando com a linha `Co-Authored-By: Claude <noreply@anthropic.com>`.

## Relatório final (até 30 linhas, em português)
Branch e hash; o que mudou por arquivo; os números adotados e quais precisam de conferência; as medições pedidas, antes e depois; onde o efeito é consumido; demografia de 20 seeds × 100 anos antes e depois; custo por NPC-tick antes e depois; testes novos, reescritos e afrouxados; o que não fechou. Se o gate completo não ficou verde, diga isso primeiro, com a saída.

## Um comando pesado por vez (regra do dono, 2026-11-12)
- Gate, testes, relatórios de 20 seeds, perfil e regravação de goldens rodam por uma fila de uma vaga, entre todos os agentes: `bash <pasta>/lw-um-por-vez.sh <comando> [args...]` (cópia do script nesta pasta). `build.sh` e `lint.sh` ficam fora.
- Nunca dois comandos pesados em paralelo, nem do mesmo agente. Rodar em segundo plano e esperar a notificação.
