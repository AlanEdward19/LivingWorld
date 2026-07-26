# rules/eval-criteria.md — carregada ao escrever/revisar critério de fase

Um critério de fase é um **gate executável**. Se não roda no CI e não pode falhar, não é
critério — é opinião. As 5 regras abaixo corrigem classes inteiras de erro.

## R1 — Não teste o que o tipo já garante
Se o construtor, o enum ou a invariante da Fase 0 já impedem o estado, o assert pertence ao
unit test do tipo, não ao critério da fase. O critério testa a **transição rejeitada**:
a operação que tenta violar retorna `Failure` **e** deixa o mundo byte-idêntico.
> Ruim: "nenhum estoque negativo" (o tipo é não-negativo — passa com a compra deletada).
> Bom: "débito além do saldo retorna `Failure` e `Hash(world)` não muda".

## R2 — Invariante a cada tick em horizonte curto; horizonte longo em nightly
Conservação e faixa violam cedo. Checar só no ano 100 é lento **e** atrasa a detecção.
> Gate: 10 anos com o assert rodando **a cada tick**. Nightly: 100 anos, mesmo assert,
> `Category=Scenario`.

## R3 — Nenhum número mágico
Todo limiar é (a) derivado de parâmetro do cenário em runtime, ou (b) gravado em
`tests/baselines/*.json` a partir de **20 seeds**, atualizável só por commit explícito.
Literal no texto do critério é proibido.
> Ruim: "≥ 2× a habilidade", "variância ≥ 50%", "≤ 500 MB", "< 200 ms".
> Bom: "razão especialista/trocador maior em 20/20 seeds; a razão média vai pro baseline".

## R4 — Efeito causal exige braço de controle
Medir só o tratamento não prova causa — deriva demográfica e ruído de seed explicam
sozinhos. Rode **par base/tratamento com a mesma seed**, ≥ 10 seeds, e asserte a diferença
com contagem de acertos (10/10, 18/20). Direção, não magnitude.

## R5 — Zero ação humana
"Adicionar o `using` quebra o build", "auditoria de código", "prove chamando todos os
endpoints" não rodam no CI. Vire mutação automatizada: compilação Roslyn em memória,
`git diff --exit-code` sobre artefatos gerados, enumeração por reflexão que **falha se
algum item não estiver coberto**.

## Determinismo não se repete
"Mesma seed → mesmo hash" é **um** teste parametrizado sobre todos os cenários, não um
critério por fase. Na fase, o critério é o inverso, que hoje ninguém verifica:
**desligar o sistema novo muda o hash** (prova que ele entrou na conta).

## Teste de mutação para gate de segurança
Critério que protege uma fronteira (validação de LLM, autorização, integridade) precisa de
um par: desabilite a proteção por flag de teste e exija que **este** critério falhe.
Se não falhar, ele não media nada.

## Checklist antes de escrever "## Critérios de verificação"
`[ ]` cada item pode falhar · `[ ]` nenhum literal sem procedência · `[ ]` causal tem
controle · `[ ]` nada exige humano · `[ ]` horizonte ≤ 10 anos no gate · `[ ]` o que o tipo
garante ficou de fora
