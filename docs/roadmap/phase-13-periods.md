# Fase 13 — Múltiplos períodos

**Objetivo**: o motor central roda qualquer época sem fork de código. Cada período —
pré-histórico, medieval, moderno, futurista, criaturas — é um **módulo de conteúdo**
carregado como dado, não um projeto novo em `src/`.

## Tasks
1. **Formato de cenário completo**: a Fase 3 já entrega cenário-como-dado; aqui o formato
   cresce para profissões, recursos, tipos de local, tecnologias, formas de governo e
   valores culturais. Validado no carregamento — inválido falha rápido, com campo e linha.
2. **Carregador e catálogo**: o motor lê o cenário para catálogos em memória. Nenhum
   `switch (periodo)` em `src/` — o que muda são as tabelas, não os ramos.
3. **Módulo pré-histórico**: bandos, caça, coleta, migração sazonal e formação de tribo,
   expressos como profissões, recursos e regras de assentamento do próprio cenário.
4. **Módulo moderno**: industrialização, empresas como empregadoras, saúde pública e
   educação, reaproveitando os sistemas de economia e habilidades já existentes.
5. **Módulo futurista**: automação e robótica como produtores sem necessidades biológicas,
   IA e cidades inteligentes como tecnologias que alteram produtividade e migração.
6. **Módulo jurássico/criaturas**: espécies, território, cadeia alimentar e predador/presa
   usando os **mesmos** sistemas de população e reprodução dos NPCs, com evolução biológica
   como herança de atributos ao longo das gerações.
7. **Seleção de cenário na API**: iniciar um mundo escolhendo o período; o cliente descobre
   profissões e recursos pelo catálogo, sem lista fixa embutida.
8. **Baseline do horizonte evolutivo**: rodar o par com/sem pressão seletiva em vários
   horizontes e gravar em `tests/baselines/` o **menor** horizonte que ainda separa os
   braços. É o número que o critério de evolução usa — procedência, não chute.

## Critérios de verificação
- **Dois períodos, um binário, um processo**: o mesmo teste carrega dois cenários
  (`medieval` e `prehistoric`) no **mesmo processo**, roda os dois e asserta que o hash do
  assembly de `LivingWorld.Simulation` é **idêntico** antes e depois. Trocar de período é
  trocar de dado; se o binário mudou, alguém compilou conteúdo.
- **Adicionar um período novo é adicionar arquivos**: o teste cria um cenário mínimo em
  tempo de execução, roda 10 anos e passa, sem tocar em nenhum `.cs`.
- Teste de arquitetura falha se o nome de qualquer período (`medieval`, `prehistoric`, …)
  aparecer como literal em `src/LivingWorld.Domain` ou `src/LivingWorld.Simulation`.
- **Profissão vem do catálogo, e só dele**: em todos os ticks amostrados,
  `profissõesObservadas ⊆ catálogo carregado` (assert de conjunto). Mais o teste
  **negativo**, que é o que realmente pode falhar: cenário declarando profissão fora do
  catálogo é **rejeitado no load**, com o campo apontado. Sem esse par, o assert de conjunto
  é tautológico — profissão só nasce do catálogo.
- Cenário pré-histórico: 10 anos no gate com as invariantes de população rodando **a cada
  tick**; 100 anos no nightly (`Category=Scenario`), mesmo assert.
- O módulo de criaturas usa `PopulationSystem` e `ReproductionSystem` — o teste prova que
  nenhum sistema paralelo de população foi registrado no cenário jurássico.
- Cada cenário roda determinístico: `Sim(cenario, seed: 42).Run(3650)` duas vezes → mesmo
  hash canônico; cenários diferentes com a mesma seed → hashes diferentes.
- **Evolução biológica com braço de controle**: par com e sem a pressão seletiva declarada,
  **mesma seed**, 20 seeds. O sinal de (tratamento − controle) na média do atributo
  pressionado bate a direção prevista em **≥ 18/20**. Medir só o braço tratado não prova
  nada: a deriva move a média sozinha. Horizonte = o valor gravado pela task 8; ele não
  cabe no teto de 10 anos do gate, então esta verificação roda no nightly.

## Fora do escopo
**Migrar o conteúdo medieval para arquivo de cenário não é task desta fase**: a Fase 3 já
nasce com cenário-como-dado e com o cenário `test-scifi` rodando no gate, então a dívida de
conteúdo hardcoded nunca chega até aqui. Integridade referencial do cenário (profissão que
aponta recurso inexistente) também é da Fase 3, pelo sweep genérico. Arte e assets por
período são Fase 14. Nenhum sistema de simulação novo entra aqui — se um período pede
mecânica inédita, ela vira fase própria antes.

## Ver também
[society.md](../domain/society.md) ·
[economy.md](../domain/economy.md) ·
[genetics-and-family.md](../domain/genetics-and-family.md) ·
[ADR-0001](../adr/ADR-0001-monolito-modular-dotnet.md) ·
[ADR-0005](../adr/ADR-0005-simulacao-deterministica-semeada.md) ·
[rules/simulation-determinism.md](../../rules/simulation-determinism.md)
