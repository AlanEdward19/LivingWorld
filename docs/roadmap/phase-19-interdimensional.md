# Fase 19 — Trânsito interdimensional e catch-up

**Objetivo**: branch é dimensão. Quem desenvolver artefato, tecnologia ou potência de
trânsito **volta à linha de origem** — e a linha precisa ter avançado enquanto ninguém
olhava. Branch dormente fica congelado e é simulado sob demanda (ADR-0012): como o mundo é
função de `(seed, estado, ticks)`, simular tarde produz o mesmo mundo que simular na hora.
Preguiça aqui não é aproximação; é o mesmo resultado, mais barato.

> **Spec, não gate.** Os critérios abaixo são a intenção; os critérios finais são escritos
> sob `rules/eval-criteria.md` quando a fase for ativada. Não comece esta fase antes da
> Fase 17 fechar.

## Tasks
1. **`simuladoAté` por branch**, persistido junto do `BranchId` e avançado só por catch-up
   concluído. É o único ponto de verdade sobre até onde uma linha existe.
2. **Catch-up sob demanda** de `simuladoAté` até T ao voltar a um branch. Se
   `T <= simuladoAté`, o caminho é literalmente sem trabalho — não há nada a recalcular.
3. **Relógio próprio por branch**: "agora" é por linha. As linhas **não** andam juntas, e
   nenhuma consulta pode assumir um relógio global.
4. **`LOD(branch, tick)` como função pura do registro de presença** (Simulation LOD da Fase
   8): a resolução **não** é otimização do catch-up, é **definição do mundo**. Invariante que
   a resolução baixa não preserva força o degrau acima — escolha do motor, não do chamador.
5. **Cache append-only do catch-up já feito**: nunca é refeito, nunca é sobrescrito. Mesma
   disciplina do log (ADR-0006).
6. **Pré-aquecimento em background de branches ancorados**, fora do caminho crítico do tick
   e sem efeito sobre o resultado — só sobre quando ele fica pronto.
7. **Orçamento e progresso visíveis**: catch-up longo reporta progresso e respeita o teto de
   trabalho por chamada declarado no cenário. Estourar o orçamento é resultado explícito,
   não travamento silencioso.
8. **Trânsito como potência (Fase 15), não botão**: custo cobrado no uso, rolagem pelo
   primitivo único (ADR-0011, perfil `Dramático`) e modos de falha com consequência —
   chegada na linha errada, chegada fora do tick pretendido, meio de trânsito consumido.
9. **Chegada em linha onde o viajante já existe**: regra explícita de identidade e de
   coexistência, aplicada pelo motor antes de qualquer materialização.
10. **Cenário `test-catchup` pareado**: o mesmo branch simulado eager e simulado em lances,
    e uma ida-e-volta de duração declarada, para servir de controle a tudo abaixo.
11. **Registro de presença append-only por branch**: quem observou qual intervalo, nunca
    reescrito. É a única entrada de `LOD(branch, tick)` (task 4) e, sendo append-only, fixa a
    escala: simulado um tick em `L`, é `L` para sempre — não existe re-rodar mais fino.

## Critérios de verificação
- **Preguiçoso == eager, dado o mesmo registro de presença** — o critério que sustenta o
  ADR-0012 inteiro: com o registro de presença fixado pelo cenário, simular um branch direto
  até T e simular em dois lances (até T/2, depois até T) produzem hash canônico **idêntico**,
  em **dois processos** separados. Sem essa cláusula o critério é falso assim que houver
  degradação. Falhou isto, congelar branch dormente cai junto.
- **Resolução é definitiva**: simulado um tick em `L`, ele é `L` para sempre. Re-simular um
  intervalo já simulado em fidelidade maior é **transição rejeitada** — retorna `Failure` e
  deixa o mundo byte-idêntico depois da tentativa.
- **Pré-aquecimento é bit-idêntico ao catch-up sob demanda**: o mesmo branch pré-aquecido em
  background e alcançado sob demanda dão o mesmo hash canônico, porque os dois seguem a mesma
  escala de `LOD(branch, tick)`. Divergência prova fidelidade inventada pelo pré-aquecimento.
- **`T <= simuladoAté` não executa tick nenhum**: com o contador de ticks instrumentado, a
  chamada retorna com contagem **== 0** e hash canônico inalterado. Um único tick executado
  reprova — é retrabalho disfarçado de cache.
- **Catch-up custa o intervalo, não a idade do branch**: com `N` anos de atraso fixo pelo
  cenário, ticks executados e tempo de parede ficam dentro do baseline de 20 seeds em
  `tests/baselines/`, enquanto o `simuladoAté` inicial varia pelos valores declarados.
  Custo que acompanha o tempo total do branch reprova.
- **Nenhuma consulta mistura linhas**: enumeração por reflexão de **toda** a superfície de
  consulta temporal; cada handler é exercido em dois branches com `simuladoAté` diferentes e
  reprova se devolver tick, evento ou entidade de outra linha — **e** reprova se algum
  handler ficar sem cobertura. Par de mutação: remover o filtro de `BranchId` por flag de
  teste tem de fazer este critério falhar.
- **A volta encontra a casa envelhecida na medida certa**: o viajante sai de A no tick T e
  volta em `T + D` (`D` declarado no cenário). Ao voltar, `simuladoAté(A) == T + D` exato, o
  log de A contém a cadeia contínua de eventos que o catch-up gerou, sem buraco nem
  duplicata, e o hash canônico de A bate com o de um braço que simulou A eager por `D`.
- **Trânsito entrou na conta**: desligar o subsistema de trânsito por flag muda o hash
  canônico em 10 anos.

## Fora do escopo
Criação de branch, âncora, coleta e inércia histórica: Fase 17. Fusão de linhas continua não
existindo (ADR-0008). Contato e escala cósmica: Fase 18. A potência genérica que o trânsito
instancia: Fase 15. Prosa sobre o retorno: Fase 11. Custo de trânsito não tem gate (ADR-0010).

## Questões em aberto
- **Se o viajante já existe na linha de destino, o que acontece?** Dois corpos com a mesma
  identidade, substituição, chegada rejeitada, ou identidades distintas com laço explícito?
  Cada resposta muda o esquema, a inspeção da Fase 8 e o teste de conservação de população.
- Qual é o **orçamento máximo de catch-up** antes de recusar a entrada no branch? Degradar já
  é legítimo (é definição do mundo, não aproximação), então o teto é de trabalho, não de
  fidelidade — e recusar deixa a linha inacessível até alguém pagar a conta.
- **O branch de origem pode ser coletado enquanto o viajante está fora?** O viajante é
  âncora de A mesmo ausente, ou a ausência solta a âncora — e, narrativamente, o que
  significa voltar para uma linha que foi coletada: não há casa, ou não há para onde voltar?
- Orçamento estourado é `falha` ou `sucessoParcial` do primitivo, e o catch-up parcial avança
  `simuladoAté` até onde chegou ou é descartado inteiro?

## Ver também
[timelines.md](../domain/timelines.md) ·
[time-and-ticks.md](../domain/time-and-ticks.md) ·
[simulation-lod.md](../domain/simulation-lod.md) ·
[powers.md](../domain/powers.md) ·
[ADR-0012](../adr/ADR-0012-catchup-preguicoso-de-branch.md) ·
[ADR-0008](../adr/ADR-0008-ramificacao-como-modelo-temporal.md) ·
[ADR-0009](../adr/ADR-0009-branchid-no-esquema-desde-a-fase-3.md) ·
[rules/eval-criteria.md](../../rules/eval-criteria.md)
