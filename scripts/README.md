# scripts/ — comandos repetíveis executáveis

O agente **roda o script**, não monta o comando. Economiza token, padroniza flags e
dá saída 0/1 estável para os eval gates.

| Script | Faz | Flags |
|---|---|---|
| `test.sh` | `dotnet test` Release, Unit sem `Category=Scenario` (~1 min) | `--watch`, `--filter <padrão>`, `--scenarios`, `--all`, `--project integration\|longrunning`, `--heavy`, `--no-build` |
| `lint.sh` | `dotnet format` | `--fix` (aplica) |
| `build.sh` | `dotnet build -c Release -warnaserror` (sem web) | — |
| `check-docs.sh` | Falha se algum `.md` passar de 100 linhas | — |
| `close-issues-from-pr.sh` | Fecha as issues citadas (`Closes #N, #M`) no corpo de um PR; o workflow `close-issues.yml` o chama ao mergear em `main` (o GitHub não fecha sozinho listas como `Closes #1, #2`) | `--list` (só imprime os números) |
| `client-stress.sh` | Três rodadas gráficas de 500 NPCs, API local já iniciada | `[caminho-do-cliente]` |
| `compare-simulation-cost.sh` | Três pares alternados do MeasureHarness; tetos +3% CPU / +2% alocação | `<checkout-base> [saída]` |
| `compare-creation-cost.sh` | Mede 6.000 NPCs em três pares de processos alternados; falha acima de +5% | `<checkout-base> [saída]` |
| `profile.sh` | Perfil por sistema do tick (#158): tempo e bytes por sistema, despertares sem efeito e causa de cada agendamento; não é gate | `[--pop N] [--ticks N]` |
| `demography-report.sh` | Demografia de época (AD-130): N seeds × A anos de `ScenarioRunner.Create`; natalidade, mortalidade, 1q0, e0, TFR, intervalo entre partos, razão de sexo e nascimentos por mês; não é gate | `[seed-inicial] [seeds] [anos] [aquecimento]` |
| `civ-report.sh` | Observatório de civilização (#112): N seeds × X anos em paralelo, sem persistência, série anual em JSON (`docs/domain/civilization-observatory.md`) | `[cenário] [seed-inicial] [seeds] [anos] [saída]` |
| `refresh-world-hashes.sh` | Regrava explicitamente goldens e pins após mudança de comportamento registrada em AD/ADR | — |
| `verify.sh` | **Gate**: check-docs + build (uma vez) + lint e test rápido em paralelo; `--full` roda Unit, Integration e LongRunning em paralelo (logs por suíte) e o `LongRunning.Performance` sozinho no fim | `--full`, `--nightly` (+ Heavy) |

Rode via `bash scripts/<x>.sh` (Git Bash no Windows).
Cenários pesados ficam fora do gate: `bash scripts/test.sh --scenarios` (Unit) ou `--project longrunning`.
Teste novo que passe de ~5 s ganha `[Trait("Category", "Scenario")]`.
`--project longrunning` (100 anos, golden hashes, escala): ~12 min, ver STATE.md para as falhas conhecidas.

Comando novo que você repetiria → vira script aqui.
