# Fase 3 — População básica

**Objetivo**: o mundo tem gente que nasce, envelhece e morre sozinha ao longo de séculos,
esse estado sobrevive ao processo (EF Core + SQLite) e o conteúdo é cenário desde o
primeiro NPC. **Fase mais pesada do roadmap** — pode fechar em dois commits: **3A** (tasks
1–7, população) e **3B** (tasks 8–14, persistência e sensores).

## Tasks
1. **Entidade NPC**: identidade (nome, sexo, data de nascimento, cultura, local de
   nascimento, residência), saúde e localização atual. Sem necessidades, sem profissão.
2. **Idade derivada**: `idade = WorldDate.Hoje - DataDeNascimento`. Idade **nunca** é
   coluna que um sistema incrementa — não existe "sistema de aniversário".
3. **Household**: família como unidade com residência, membros e chefe. Nascimento entra
   num household; morte remove dele; household sem membros é dissolvido.
4. **Nascimento e morte como eventos agendados** no scheduler da Fase 1. Concepção agenda
   o parto; a morte por idade agenda o tick de óbito. Nenhuma varredura por tick.
5. **Tabela de vida**: probabilidade de morte por faixa etária e saúde, vinda do cenário.
   Mortalidade infantil alta e longevidade máxima explícitas.
6. **Gerador de população inicial**: 100 NPCs com pirâmide etária coerente — crianças,
   adultos, idosos, sexos distribuídos, famílias formadas. Nada de 100 adultos de 30 anos.
7. **Cenário como dado desde o primeiro NPC**: profissões, recursos e tipos de local vêm do
   arquivo de cenário, nunca de `enum` em código. Entra um segundo cenário deliberadamente
   alienígena, `test-scifi` (profissões piloto/técnico, recursos plasma/liga), que roda no
   gate. Se um cenário de ficção científica roda na Fase 3, conteúdo medieval não calcifica
   em `src/` — e a Fase 12 deixa de precisar da task de migração.
8. **Persistência**: EF Core + SQLite, primeira migração, mapeamento das entidades. Nenhum
   recurso exclusivo do SQLite — o esquema atravessa para Postgres (ADR-0002).
9. **`BranchId` no esquema desde a primeira migração** (ADR-0009): toda entidade persistida
   e toda linha de event log carregam a coluna; chave composta e índices a contemplam;
   branch é **parâmetro explícito** de todo repositório e de toda consulta, nunca implícito.
   Até a fase temporal existe um branch só, o raiz — nada ramifica ainda.
10. **Snapshot + event log persistidos** (ADR-0006): salvar, encerrar o processo, recarregar
    e continuar do mesmo ponto. Replay do log a partir de qualquer snapshot.
11. **Zero round-trips de banco durante o tick**: `DbCommandInterceptor` de teste conta
    comandos executados durante um run. Banco só é tocado nas fronteiras de snapshot.
12. **Sweep de integridade referencial genérico**: dirigido por reflexão sobre **todos** os
    tipos de ID, rodado ao fim de cada cenário de teste. Ganha cobertura sozinho a cada
    fase nova (emprego em local demolido, evento citando NPC inexistente, memória órfã).
13. **Sensor de bytes/NPC/ano**: mede o crescimento do estado + log e reprova acima do teto
    declarado no cenário. Aprende a taxa real seis fases antes de virar problema.
14. **Lista de campos monotônicos** com assert genérico sobre ela: idade só cresce,
    contadores do log só crescem, massa monetária só muda por cunhagem/destruição
    registrada. A lista cresce a cada fase; campo declarado que regride reprova.

## Critérios de verificação
- Gate: **10 anos** com 100 NPCs iniciais e as invariantes checadas **a cada tick**.
  Nightly (`Category=Scenario`): os mesmos asserts em 100 anos.
- População final dentro do baseline de **20 seeds** em `tests/baselines/population.json`;
  reprova fora de `[min × 0.8, max × 1.2]`. Faixa com procedência, não chute.
- O mesmo gate de 10 anos passa com o cenário `test-scifi`, com as mesmas invariantes; e o
  teste de arquitetura reprova se profissão, recurso ou tipo de local aparecer como literal
  em `src/LivingWorld.Domain` ou `src/LivingWorld.Simulation`.
- **Nenhum evento com tick > tickDeMorte referencia o NPC.**
- **A tabela de vida não trunca cedo**: em 100 anos (nightly), pelo menos 1 NPC atinge mais
  de 90% da longevidade máxima do cenário. Só "ninguém passa da longevidade" passa por
  construção quando a morte é agendada na longevidade.
- **Idade responde ao relógio**: avançar o clock sem executar nenhum sistema muda a idade
  de todos os NPCs vivos — prova que ela é derivada, não coluna.
- **Zero round-trips**: o interceptor conta **0** comandos de banco durante 3650 ticks fora
  das fronteiras de snapshot. Este teste sozinho garante que SQLite → Postgres é trocar
  string de conexão.
- **Sweep referencial** limpo ao fim de todo cenário, e o teste **falha se algum tipo de ID
  do assembly não estiver coberto** pelo sweep.
- **Extremos populacionais**: mundo com 0 NPCs avança 1000 ticks sem exceção e com hash
  canônico estável; mundo com 1 NPC não gera filho; extinção emite evento e para os
  sistemas dependentes; população 100× a inicial não estoura o teto de iterações do tick.
- **Idempotência de replay**: para cada snapshot em `t ∈ {0, T/4, T/2, 3T/4}`, reaplicar o
  log até `T` produz o mesmo hash canônico. Pega escrita não-determinística escondida.
- Salvar no tick T, reabrir **em outro processo** e rodar até T+3650 dá o mesmo hash de
  rodar direto.
- **`BranchId` não é decoração**: o teste de arquitetura enumera por reflexão **todos** os
  métodos de repositório e consulta e reprova qualquer um que não filtre por `BranchId` —
  falha também se algum método ficar sem cobertura. E o hash canônico é **por branch**:
  dois branches com conteúdo idêntico têm hashes distintos. Sem esses dois, a coluna existe
  e não funciona no dia em que a fase temporal precisar dela (ADR-0009).
- Bytes/NPC/ano abaixo do teto declarado no cenário, medido no run de 10 anos.
- Desligar o sistema de nascimentos muda o hash canônico em 10 anos (ele entra na conta).

## Fora do escopo
Necessidades, rotina e comportamento (Fase 4), economia e profissão (Fase 5), atração e
casamento (Fase 7). Aqui a reprodução é regra demográfica do cenário, não escolha do NPC.

## Ver também
[npc.md](../domain/npc.md) · [genetics-and-family.md](../domain/genetics-and-family.md) ·
[ADR-0002](../adr/ADR-0002-sqlite-agora-postgres-depois.md) ·
[ADR-0006](../adr/ADR-0006-snapshot-mais-event-log.md) ·
[ADR-0009](../adr/ADR-0009-branchid-no-esquema-desde-a-fase-3.md) ·
[rules/eval-criteria.md](../../rules/eval-criteria.md) ·
[rules/database-entities.md](../../rules/database-entities.md)
