# Fase 10 — História degradável

**Objetivo**: história não é log comprimido, é **relato transmitido e degradado**. Morta a
última testemunha, o fato deixa de existir como registro fiel e passa a existir como
relatos — tradição, livro, crônica, canção, monumento —, cada um com fidelidade,
proveniência e distorção acumulada. O NPC age sobre a **crença**; o motor guarda um
esqueleto compacto do fato só para poder comparar.

## Tasks
1. **Esqueleto imutável do fato**: tipo fechado, campos mínimos (quem, o quê, onde, quando,
   significância). Append-only imposto pelo armazenamento, sem `UPDATE`/`DELETE` (ADR-0006).
2. **Significância calculada na escrita**: escopo do impacto, entidades afetadas, papel dos
   envolvidos. É o que decide o que sobrevive no esqueleto e o que colapsa.
3. **Janela de memória viva**: enquanto existe testemunha viva, o fato é consultável com
   fidelidade alta (já enviesada pela testemunha), sem virar relato.
4. **Conversão fato → relato** disparada pela morte da **última** testemunha. É um evento
   agendado no scheduler (Fase 1), não uma varredura por tick.
5. **Operadores de distorção determinísticos** aplicados **por salto de transmissão**:
   troca de atribuição, inflação de magnitude, compressão temporal, perda de causa,
   moralização, anacronismo, omissão conveniente, fusão de personagens. RNG semeado pelo
   par (relatoId, salto). **O motor distorce; a LLM só narra o que já veio distorcido.**
6. **Meios de transmissão com fidelidade própria**: memória viva, tradição oral familiar,
   livro/crônica, monumento/inscrição, canção/ditado. O meio define a taxa de distorção por
   salto, o alcance e como o relato morre.
7. **Cânone limitado por comunidade**: no máximo `N` relatos vivos por comunidade (`N` do
   cenário). Relato novo entra despejando o de menor peso (importância × transmissibilidade
   × recência). É o que torna o custo **independente do número de anos**.
8. **Livros como objetos do mundo**: podem ser copiados (com erro de copista), perdidos,
   queimados e **redescobertos** — a redescoberta é um evento declarado, não um acaso.
9. **Duas consultas separadas na API**: `Verdade` (visão de motor, debug, ferramenta de
   autor) e `Crença` (o que este NPC, esta família ou esta cultura acredita). Nunca
   misturadas, nunca no mesmo handler.
10. **Índices de consulta** por ano, entidade e tipo, no esqueleto e no cânone. Consulta de
    linha do tempo não varre a base.
11. **Dinastias e linhagens** derivadas do esqueleto, nunca tabela paralela. **Correção do
    passado só por evento compensatório** — linha nova, jamais reescrita da original.

## Critérios de verificação
- **O cânone não cresce com o tempo** (a propriedade que faz o modelo caber em disco):
  rodar 50, 100 e 200 anos e assertar que o total de relatos vivos por comunidade fica no
  teto declarado no cenário nos três horizontes, **sem tendência de crescimento**.
- **Orçamento por relato**: bytes por relato retido medidos em 10 anos, comparados ao
  orçamento em `tests/baselines/` (20 seeds). Sem "≤ 500 MB" chutado e sem esperar 100 anos.
- **Colapso é seletivo, não deleção**: **100%** dos eventos com significância ≥ limiar do
  cenário continuam íntegros no esqueleto **e** ≥ `X%` dos abaixo do limiar foram
  colapsados, com `X` vindo do cenário. Só "bruto > retido" passa deletando uma linha.
- **Consulta é indexada, provado por complexidade**: contar linhas lidas (contador de I/O
  ou plano de query) e falhar se ler mais que `k × tamanho do resultado`. Sem milissegundos
  — tempo de parede mede a máquina de CI, não o índice.
- **Append-only por tentativa real**: o teste executa `UPDATE` e `DELETE` diretos na tabela
  de eventos e exige que **ambos** falhem no armazenamento.
- **Distorção é determinística**: mesma seed → relato distorcido byte-idêntico entre dois
  processos. E o provider de LLM **fake** falha o teste se for chamado durante a distorção:
  a LLM não participa da geração do relato.
- **Distância relato↔fato é não decrescente** ao longo dos saltos de transmissão: para toda
  cadeia do cenário, `d(salto n+1) >= d(salto n)`.
- **Nenhum relato rejuvenesce sozinho**: `d` só pode cair num salto precedido por um evento
  de redescoberta declarado. Queda sem redescoberta é falha.
- **Verdade e crença divergem**: em pelo menos um caso do cenário de teste as duas consultas
  retornam versões diferentes. Se nunca divergem, o sistema de distorção não está ligado.
- **Nenhum caminho de jogo alcança a verdade**: enumerar por reflexão **todos** os handlers
  da API de jogo e exigir que nenhum resolva para a consulta de verdade — falha se algum
  handler ficar sem cobertura. Par de mutação: desligar a checagem por flag de teste tem de
  **fazer este critério falhar**.
- **Crenças incompatíveis coexistem**: dois NPCs de comunidades diferentes acreditam em
  versões contraditórias do mesmo fato **e** cada um decide de forma coerente com a própria
  crença — a decisão observada bate com a crença dele, não com o fato.
- Toda morte no esqueleto tem nascimento do mesmo `NpcId` em tick anterior; zero eventos
  após a morte; linhagem reconstruída chega a um fundador sem buraco e sem ciclo.
- Evento compensatório aparece na consulta com a linha original ainda legível, marcada.
- Reidratar um snapshot e reaplicar o log a partir dele reproduz o mesmo `Hash(world)`.

## Escopo herdado de outras fases
Guerra entre cidades, tratados e política externa (apontado por Fase 8 e Fase 23 como "fora
do roadmap atual") pousa aqui — ainda sem tasks/critérios próprios; entra quando a Fase 10 for
ativada (status `pendente`, não `spec`, mas o detalhe de guerra/diplomacia precisa de
levantamento próprio antes de virar task).

## Fora do escopo
Prosa narrativa, jornais e biografias geradas: Fase 12. LLM lendo o passado para narrar:
Fase 11 em diante — aqui relato e crença são **dado estruturado**, produzido pelo motor.

## Ver também
[historical-memory.md](../domain/historical-memory.md) ·
[history.md](../domain/history.md) · [memory.md](../domain/memory.md) ·
[society.md](../domain/society.md) ·
[ADR-0007](../adr/ADR-0007-memoria-historica-degradavel.md) ·
[ADR-0006](../adr/ADR-0006-snapshot-mais-event-log.md) ·
[rules/eval-criteria.md](../../rules/eval-criteria.md) ·
[rules/database-entities.md](../../rules/database-entities.md)
