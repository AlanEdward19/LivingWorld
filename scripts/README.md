# scripts/ — comandos repetíveis executáveis

O agente **roda o script**, não monta o comando. Economiza token, padroniza flags e
dá saída 0/1 estável para os eval gates.

| Script | Faz | Flags |
|---|---|---|
| `test.sh` | `dotnet test` (exclui `Category=Scenario` por padrão) | `--watch`, `--filter <padrão>` |
| `lint.sh` | `dotnet format` | `--fix` (aplica) |
| `build.sh` | `dotnet build -c Release -warnaserror` | — |
| `check-docs.sh` | Falha se algum `.md` passar de 100 linhas | — |
| `verify.sh` | **Gate**: check-docs + build + lint + test | — |

Rode via `bash scripts/<x>.sh` (Git Bash no Windows).
Cenários longos (100 anos) ficam fora do gate: `bash scripts/test.sh --filter Category=Scenario`.

Comando novo que você repetiria → vira script aqui.
