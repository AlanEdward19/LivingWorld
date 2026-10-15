# Ontogeny debts [Estado: Entregue - issue #93, AD-118, ADR-0029]

## Problem
A Fase 21 fechou com dívidas: órfãos sem parente morrem de sede (AD-115), `Cry` não move ninguém, a exposição é binária, `Upbringing` não
acompanha quem cria e o pool agregado não tem como receber criança. Sonda (5 seeds × 10 anos): 9 mortes por desidratação, todas de criança
em domicílio unitário sem adulto.

## Flow
1. `OrphanCareSystem` (new, mensal): criança sem adulto/idoso vivo no domicílio é acolhida — 1º tio/tia (irmão de pai ou mãe), 2º contato com
   afeto ≥ mínimo do `MutualAid`, 3º adulto da mesma cidade de maior altruísmo (empate: menor id) cujo domicílio tem menos membros.
2. `BehaviorDecisionSystem` (exists, extended): adulto que ouve choro (criança do domicílio, mesma célula, despensa sem comida ou água) compra
   mesmo na rotina de dormir.
3. `ExposureAccumulator` (exists, extended): `1 − Π(1 − AttentionPerCaregiver)`; padrão 1,0 reproduz a exposição binária.
4. `MilestoneProgressSystem` (exists, extended): `Upbringing` caminha para a média dos cuidadores que não são os pais, ponderado pela exposição.
5. `City.Dematerialize` (exists, guarded): criança não vai ao pool.
6. `HouseholdRedistribution` e `MarriageSystem` (exist, extended): órfão é acolhido no momento da morte dos pais (antes só havia parente ou domicílio
   unitário) e quem casa leva os filhos pequenos que ficariam sozinhos (sonda: 3 seeds perdiam crianças para o casamento do único adulto).

## Critérios (EARS), braços pareados em 20 seeds, horizonte ≤ 10 anos
1. WHEN uma criança fica sem adulto no domicílio e existe adulto vivo na cidade, ela SHALL ser acolhida até o fim do mês; em 10 anos de vila nenhuma
   criança com adulto disponível na cidade fica descoberta em duas viradas de mês seguidas (a morte dos pais dispara depois do sistema, então uma
   é esperada). Controle (`OrphanCare=false`, 20 seeds × 6 anos): mortes de crianças por privação menores no total, em mais seeds do que as
   que pioram (mundos divergem após a 1ª adoção: por seed há ruído; magnitude e ≥ 18/20 não são exigidos). Mutação: desligar o sistema reprova.
2. WHEN a criança chora com a despensa sem comida ou água e há adulto co-localizado com carteira, ele SHALL escolher Comprar mesmo na hora de
   dormir; sem choro continua dormindo. Mutação: tirar o gancho reprova.
3. WHEN `AttentionPerCaregiver` < 1, dois cuidadores SHALL render mais progresso que um, e com 1,0 o resultado é o da exposição binária.
4. WHEN os cuidadores não são os pais, `Upbringing` ao completar SHALL estar mais perto do deles que o do nascimento (20/20); com os pais como
   cuidadores SHALL permanecer o do nascimento. Mutação: desligar a cópia reprova.
5. Criança SHALL ser rejeitada por `City.Dematerialize`.
5b. WHEN um adulto casa, os filhos pequenos que ficariam sem adulto no domicílio antigo SHALL ir com ele; os que ficam com outro adulto não, salvo os seus.
6. Determinismo em dois processos; desligar o acolhimento muda o hash.

## One-way doors
`OntogenyRules.AttentionPerCaregiver` (cenário, opcional, padrão 1,0) e `Upbringing` passa a mudar após o nascimento (campo `Npc.Upbringing`
ganha mutador; formato do snapshot não muda). Novo sistema `ONTOGENY`/`FAMILY` sem evento novo além de `OrphanTakenIn`.

## Out of scope
Resample de desenvolvimento no pool agregado: continua descartado — não há criança no pool, a guarda do item 5 reativa o assunto se isso mudar.
Adoção formal com herança/direitos (Fase 7), exposição por ensino deliberado.
