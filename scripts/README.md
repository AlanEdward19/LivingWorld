# scripts/ — comandos repetíveis executáveis

O agente **roda o script**, não monta o comando. Economiza token, padroniza flags e
dá saída 0/1 estável para os eval gates.

| Script | Faz | Flags |
|---|---|---|
| `test.sh` | `dotnet test` Release, Unit sem `Category=Scenario` (~1 min) | `--watch`, `--filter <padrão>`, `--scenarios`, `--all`, `--project integration\|longrunning` |
| `lint.sh` | `dotnet format` | `--fix` (aplica) |
| `build.sh` | `dotnet build -c Release -warnaserror` (sem web) | — |
| `check-docs.sh` | Falha se algum `.md` passar de 100 linhas | — |
| `close-issues-from-pr.sh` | Fecha as issues citadas (`Closes #N, #M`) no corpo de um PR; o workflow `close-issues.yml` o chama ao mergear em `develop` (o GitHub só fecha sozinho em `main`) | `--list` (só imprime os números) |
| `verify.sh` | **Gate**: check-docs + build + lint + test rápido | `--full` (+ cenários pesados) |

Rode via `bash scripts/<x>.sh` (Git Bash no Windows).
Cenários pesados ficam fora do gate: `bash scripts/test.sh --scenarios` (Unit) ou `--project longrunning`.
Teste novo que passe de ~5 s ganha `[Trait("Category", "Scenario")]`.
`--project longrunning` (100 anos, golden hashes, escala): ~12 min, ver STATE.md para as falhas conhecidas.

Comando novo que você repetiria → vira script aqui.
