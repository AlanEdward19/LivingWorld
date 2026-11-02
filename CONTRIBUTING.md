# Contribuindo com o Living World

Obrigado por ajudar. Este guia diz como contribuir e o que procuramos num pull request.

Ao participar, você concorda em seguir o nosso [Código de Conduta](CODE_OF_CONDUCT.md).

## Formas de contribuir

- **Reportar um bug** ou **sugerir uma melhoria** pelos [modelos de issue](https://github.com/AlanEdward19/LivingWorld/issues/new/choose).
- **Pegar uma issue.** As marcadas [`good first issue`](https://github.com/AlanEdward19/LivingWorld/labels/good%20first%20issue) são pequenas e bem delimitadas; [`help wanted`](https://github.com/AlanEdward19/LivingWorld/labels/help%20wanted) são maiores. A ordem do que vem depois do quê está no [ROADMAP.md](ROADMAP.md) e no campo "Blocked by" de cada issue.
- **Conteúdo como dados:** períodos, profissões, objetos e cenários em `scenarios/` (nada disso exige mexer no motor).
- **Melhorar a documentação** em `docs/`.

Antes de começar uma issue, confira se ela está livre (sem assignee e sem PR aberto citando `#n`) e atribua-se a ela.
Para mudanças maiores, abra uma issue primeiro para combinarmos a abordagem antes de você investir tempo.

## Ambiente

Motor e API em .NET 10 / C#, cliente em Godot 4.7 (GDScript). Arquitetura em [docs/adr/](docs/adr/).

1. Instale o [.NET SDK 10](https://dotnet.microsoft.com/download) e tenha `bash` (Git Bash no Windows).
2. Rode o gate: `bash scripts/verify.sh` (docs, build, lint e testes rápidos). Ele precisa sair 0.
3. Para o cliente, instale o [Godot 4.7](https://godotengine.org/) e rode `bash scripts/client-test.sh`.

Use sempre os scripts de `scripts/` em vez de montar `dotnet test` à mão. Agentes de código: leiam o [AGENTS.md](AGENTS.md).

## Regras do motor (o que um revisor vai checar)

- **Determinismo.** Mesma seed + mesmos ticks = mesmo mundo. Nada de `Random` sem seed, `DateTime.Now` ou GUID
  aleatório na simulação; toda aleatoriedade vem do RNG semeado do mundo ([rules/simulation-determinism.md](rules/simulation-determinism.md)).
- **Coesão.** Sistema novo entra conectado: o que ele escreve precisa ter um consumidor causal real, e desligá-lo tem
  de mudar o hash ([rules/living-world-cohesion.md](rules/living-world-cohesion.md)).
- **LLM só interpreta.** A LLM propõe linguagem; o motor valida e aplica. Ela nunca escreve no estado do mundo
  ([rules/llm-boundary.md](rules/llm-boundary.md)).
- **Conteúdo é dado.** Profissão, recurso, período, forma de governo: catálogo no cenário, nunca `enum` em C#.
- **Desempenho sem regressão.** Meça antes e depois e registre no PR: `bash scripts/compare-simulation-cost.sh`
  (motor, teto +3% CPU e +2% alocação por NPC-tick) e `bash scripts/client-stress.sh` (cliente, FPS com 500 NPCs).
- **Testes.** Comportamento novo tem teste; bug corrigido tem teste que falha sem o fix. Critérios seguem
  [rules/eval-criteria.md](rules/eval-criteria.md): sem número mágico, braço de controle para efeito causal.
- **Docs curtos.** Todo `.md` em `docs/` e `rules/` fica com no máximo 100 linhas (`bash scripts/check-docs.sh`).
- **Decisões grandes.** Dependência nova, mudança de arquitetura ou de contrato público (rota, DTO, formato de
  cenário ou snapshot) pede uma ADR em `docs/adr/` e conversa na issue antes do código.

## Branches e pull requests

1. Faça um fork (ou, se tiver acesso, crie uma branch) a partir de `main`: `feat/…`, `fix/…`, `docs/…`, `chore/…`.
2. Mudanças entram por pull request; mantenha o PR focado em uma coisa.
3. Rode `bash scripts/verify.sh` antes de enviar (e `bash scripts/client-test.sh` se tocou `client/godot/`).
4. Preencha o modelo de PR e ligue a issue (`Closes #123`).

### Mensagens de commit

Usamos [Conventional Commits](https://www.conventionalcommits.org/): `feat:`, `fix:`, `docs:`, `refactor:`, `test:`,
`chore:`, `ci:`. Quando o trabalho fecha uma fase do roadmap, cite-a: `feat(phase-24): ...`. Assunto no imperativo.

### O que nunca commitar

Segredos, chaves de API, `.env`, dados reais de pessoas, mundos salvos grandes. Não edite migração já aplicada nem
código gerado.

## Problemas de segurança

Não abra issue pública para vulnerabilidades. Veja o [SECURITY.md](SECURITY.md).

## Licença

O projeto ainda não tem licença definida. Ao contribuir, você concorda que sua contribuição seja distribuída sob a
licença que o projeto adotar.
