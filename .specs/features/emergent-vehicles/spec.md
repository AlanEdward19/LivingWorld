> **Estado: Spec** (specs-closeout 2026-10-12) - plano apenas; nenhuma implementacao de veiculos em src/ ou tests/ (ver closeout.md).

# Veículos emergentes — Specification

> **Spec, não gate. Sem implementação.** Depende da Fase 24 (tecnologia como composição, limiar
> de novidade, denominação por cultura), do ADR-0021 (movimento por passos reais) e da Fase 9 (LOD).

## Problem Statement

Hoje todo NPC se desloca a pé; só poder extraordinário (ADR-0021) acelera. No fixture de escala
27% da população está em `Travel` a qualquer hora e a duração de um trajeto domina o dia. Uma
sociedade real encurta distância por engenho: monta, carrega, rola, navega. Queremos que isso
**seja descoberto, não escrito**: montaria, trenó, carroça, barco, carro são nomes que as
culturas dão a combinações de eixos que o motor derivou — nenhum existe em código ou `enum`.

## Goals

- [ ] **Veículo é entidade emergente** (nó de tecnologia da Fase 24, categoria *mobilidade*) definida
      só por eixos: propulsão (músculo, tração animal, vento, correnteza, energia armazenada),
      contato (roda, patim, casco, montado), meio (fator por terreno/água), carga, passageiros,
      requisitos (recursos, habilidade), manutenção e modo de falha. Ids de recurso, terreno e
      espécie vêm do cenário; fixtures de teste podem nomear, produção nunca.
- [ ] **Animal como propulsão sem "cavalo" no motor**: a espécie vem do cenário (porte, força,
      temperamento). Domesticação é conhecimento que também emerge, gradual, por familiaridade
      NPC↔animal. O animal come, cansa, envelhece e morre.
- [ ] **Descoberta por motivo nomeado**, nunca por evento escrito: pressão (trajeto longo, carga,
      urgência, fuga, comércio, curiosidade) + observação (tronco que rola, animal que
      carrega, correnteza) + habilidade + recursos → hipótese → protótipo (gasta recursos e
      tempo) → adoção.
- [ ] **Várias descobertas, várias versões.** O mesmo problema em lugares isolados gera variantes
      (vetores de eixos diferentes); o limiar de novidade da Fase 24 decide *entidade nova* ou
      *variação*. Cada cultura nomeia à sua maneira e pode discordar — divergência é dado.
- [ ] **Difusão e esquecimento reais**: o saber viaja por ensino, relação e comércio (relatos
      distorcem, Fase 10); cópia imperfeita gera variação; sem artesãos vivos o saber se perde.
- [ ] **Consumidores causais**: duração real de `Travel` (passos/hora, sem teleporte), decisão
      (veículos acessíveis entram no `DecisionContext`), economia (carga, comércio, custo de
      construção e manutenção), ecologia (ração e pasto), saúde (acidentes), sociedade (posse,
      empréstimo, herança, desigualdade de acesso).
- [ ] **Adoção humana, não automática**: decide quem tem acesso, riqueza, personalidade
      (aversão ao risco, abertura), memória de acidente e status. Nem todo mundo adota.
- [ ] **Sem física por tick**: entidade leve, condição por decaimento preguiçoso, tempo de
      trajeto calculado na partida da ação (orçamento da Fase 9).

## Out of Scope

| Item | Razão |
| --- | --- |
| Voo e velocidade por poder | ADR-0021 / Fase 16 — poder modula, aqui é engenho. |
| Estradas, pontes, canais como infraestrutura emergente | Spec irmã; aqui só se lê o fator de terreno. |
| Modelos 3D, animação, som | Cliente Godot (ADR-0022). |
| Nome e prosa por LLM | Fase 24 / Fase 12 — o rótulo é volátil (ADR-0014). |
| Transporte público, frota, guilda de cocheiros | Instituição futura sobre este mecanismo. |
| Combate e cerco com veículos | Fase 10+ (guerra). |

## Critérios de verificação (R1–R5 de `rules/eval-criteria.md`)

- **Funcional antes de ter nome**: com `NullLlmProvider` os veículos nascem, são construídos e
  usados; ligar o provider na mesma seed mantém o hash canônico byte-idêntico em 10 anos.
- **Mesma seed, mesmos veículos**: dois processos, 10 anos — o conjunto de ids estruturais e a
  ordem de nascimento são idênticos (comparação sobre estrutura, nunca sobre nome).
- **Nenhum veículo sem pré-requisito**: enumerar por reflexão todo veículo emergido e exigir
  que todo pré-requisito declarado existisse no tick; falha se algum ficar sem cobertura.
- **Veículo encurta o dia** (par base/tratamento, mesma seed; tratamento = espécie de tração e
  recurso disponíveis): tempo médio de trajeto e parcela em `Travel` menores em ≥ 18/20 seeds.
- **Variantes independentes**: duas regiões isoladas com a mesma pressão geram ≥ 2 ids
  estruturais distintos em ≥ 18/20 seeds; sem isolamento, menos variantes (direção).
- **Difusão pelo contato**: tratamento = rota de contato cortada; o saber chega mais tarde
  (ou não chega) em ≥ 18/20 seeds.
- **Conservação**: recursos gastos na construção saem dos estoques; o assert de conservação
  roda a cada tick por 10 anos.
- **Falha é causal**: acidente só ocorre com veículo em uso e na taxa do modo de falha
  declarado; par de mutação — desligar o modo de falha **faz** este critério falhar.
- **Adoção heterogênea**: correlação entre aversão ao risco e adoção é negativa em ≥ 18/20
  seeds; adoção < população elegível.
- **Entrou na conta**: desligar o sistema por flag muda o hash canônico em 10 anos; com a flag
  desligada o hash é o do mundo de hoje.

## Questões em aberto
- Saber sem artesãos vivos: nó morto (relatos ainda apontam para ele) ou coletado?
- Estrada emergente e veículo são a mesma entidade, duas acopladas ou independentes?
- Animal doméstico é `Animal` com dono ou entidade nova com vínculo canônico?
- Quanto de carga muda o lote de compra de um domicílio (ligação com o abastecimento)?
