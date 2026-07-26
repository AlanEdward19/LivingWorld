# Fase 0 — Fundação

**Objetivo**: a solution existe, as fronteiras entre camadas são impostas pelo compilador,
o gate sabe reprovar e a infra de baselines existe. Nenhuma regra de simulação ainda.

## Tasks
1. **Criar a solution e os projetos** (`LivingWorld.sln`), com `Directory.Build.props`
   ligando `Nullable`, `TreatWarningsAsErrors` e `LangVersion` para toda a solution:
   - `src/LivingWorld.Domain` (classlib, **zero** referência de projeto e zero pacote)
   - `src/LivingWorld.Simulation` → Domain
   - `src/LivingWorld.Infrastructure` → Domain
   - `src/LivingWorld.AI` → Domain
   - `src/LivingWorld.Api` (web) → todos
   - `src/LivingWorld.Workers` (worker) → todos
   - `tests/LivingWorld.Tests` (xUnit) → todos
2. **Tipos-base do Domain** que todas as fases usam: IDs tipados (`NpcId`, `CityId`,
   `LocationId`), `Money` (inteiro, não negativo, invariante no construtor) e `Result<T>`.
3. **Primitivo único de resolução** (ADR-0011), o "d20" do projeto:
   `Resolver(dificuldade, modificadores, perfilDeVariância, rng)` devolvendo
   `falhaCrítica | falha | sucessoParcial | sucesso | sucessoCrítico`. Perfis declarados por
   domínio: `Dramático` (d20, com críticos), `Agregado` (curva estreita, sem crítico),
   `Raro` (cauda longa). Toda fase futura decide incerteza por aqui.
4. **Teste de arquitetura** (NetArchTest sobre os assemblies compilados) para a direção das
   dependências: `Domain` sem referências, `Simulation` sem `AI`, `Api` sem regra de domínio.
5. **Analyzer de API banida**: pacote `Microsoft.CodeAnalysis.BannedApiAnalyzers` mais
   `BannedSymbols.txt` proibindo `System.Random`, `Random.Shared`, `DateTime.Now`,
   `DateTime.UtcNow`, `Guid.NewGuid` e `Environment.TickCount` em `Domain` e `Simulation`.
   Severidade **error**: quem tentar usar não compila, não é teste que alguém pode pular.
6. **Contrato de LLM sem provider**: `ILlmProvider` + DTO de saída + `FakeLlmProvider`
   determinístico e **injetivo** + `NullLlmProvider` (fallback). Nada de rede. Ver ADR-0004.
7. **Infra de baselines**: helper de teste que roda N seeds de um cenário, grava/lê
   `tests/baselines/*.json` e falha com diff legível (seed, campo, esperado, obtido).
   Regravar é comando explícito, nunca efeito colateral do gate. Todas as fases seguintes
   dependem dele — é o que substitui limiar mágico por procedência (R3).
8. **Harness de mutação do gate**: helper que copia o repo para um diretório temporário,
   aplica um mutante conhecido e roda `verify.sh` exigindo saída ≠ 0.
9. **Fechar o gate**: `scripts/build.sh`, `lint.sh`, `test.sh`, `check-docs.sh` e
   `verify.sh` rodando de verdade.
10. **`git init`** e primeiro commit.

## Critérios de verificação
- **Meta-verificação do gate**: para cada um dos 3 mutantes do fixture — um arquivo com
  `new Random()` em `Domain`, um teste com assert invertido, um `.md` de 200 linhas —
  `bash scripts/verify.sh` sai **≠ 0**. Os três casos rodam automatizados; um gate que só
  sabe sair 0 não é gate (script vazio sai 0).
- Fonte com `using LivingWorld.AI;` compilado por **Roslyn em memória** contra as
  referências de `Simulation` produz erro **CS0234** (namespace `LivingWorld` existe via
  `Domain`; o sub-namespace `AI` não). NetArchTest sobre os assemblies confirma a mesma
  fronteira em tempo de teste.
- Para **cada** símbolo do `BannedSymbols.txt`, um fixture compilado em memória com o
  analyzer ligado produz **erro** de compilação — a lista do teste é lida do próprio
  `BannedSymbols.txt`, então símbolo novo sem cobertura reprova. Removendo o arquivo de
  banidos, este critério **falha** (par de mutação: sem isso ele não media nada).
- `Money`: construtor com valor negativo lança; débito além do saldo retorna `Failure` e
  deixa o valor original byte-idêntico.
- **Primitivo de resolução**: sobre 100 mil rolagens semeadas, `Dramático` produz as cinco
  faixas de resultado e `Agregado` **nunca** produz crítico — se produzir, o perfil não está
  sendo respeitado. Modificador maior desloca a distribuição na direção prevista (par
  base/tratamento, mesma seed). Mesma seed e mesma entrada devolvem o mesmo resultado em
  dois processos. Os perfis vêm do cenário: perfil não declarado **falha no load**.
- `FakeLlmProvider`: 20 entradas distintas produzem **20 saídas distintas** (zero colisão);
  alterar 1 caractere da entrada muda a saída; a mesma entrada repetida dá a mesma saída.
  Um provider constante reprova os dois primeiros.
- Infra de baselines: baseline adulterado à mão reprova apontando seed e campo; baseline
  ausente **falha** em vez de ser gerado silenciosamente pelo gate.

## Fora do escopo
Nenhuma entidade de simulação (NPC, cidade, economia), nenhum banco, nenhuma migração,
nenhum endpoint. Persistência entra na Fase 3, quando existe algo para persistir.
Teto de 100 linhas por `.md` é sensor de formatação de `check-docs.sh`, não critério
de fase — não se repete aqui.

## Ver também
[ADR-0001](../adr/ADR-0001-monolito-modular-dotnet.md) ·
[ADR-0004](../adr/ADR-0004-abstracao-de-provider-llm.md) ·
[rules/eval-criteria.md](../../rules/eval-criteria.md) ·
[rules/implementation.md](../../rules/implementation.md)
