# ADR-0037: História nos mundos do catálogo

- Status: aceito
- Data: 2026-10-16
- Referência: issue #91; integração central autorizada pelo usuário

## Contexto
O carregador deixava HistoryRules.Disabled no mundo criado pelo menu.
Ligar as regras sozinho não bastava: o runtime enviava eventos ao sink,
sem chamar o gravador de fatos. Os cinco períodos continuavam sem fatos
após três anos no teste de integração.

## Decisão
Ligar história no ScenarioLoaderV2 com os parâmetros existentes e limiar 0.4.
Morte por privação vale 0.414375; morte comum, 0.49725.
Não criar configuração pública nova no formato de períodos.
Gravar fatos em TickContext.LogEvent antes de notificar o sink.
Excluir metadados históricos e os dois produtores que já escrevem fatos explícitos.
Remover a regravação manual dos digests de infraestrutura.
Preservar o modo desligado do WorldState e do ScenarioRunner.
Derivar a pista "hunger" do kind Starvation no relato, sem alterar o payload factual.
O consumidor existente de crenças aumenta a utilidade de Buy em 40.

## Custo e cache
O primeiro sensor detectou custo de ~3 vezes o controle.
Rejeitar eventos cujo peso máximo não alcança o limiar antes de interpretar payloads.
Reutilizar crenças hop-0 por mundo/comunidade, enquanto cânone e quantidade de fatos
não mudam; SetCanonSlots substitui a lista para invalidar também uma expulsão.
Não armazenar crenças com hops: a distorção existente consome RNG.
Omitir seeds vazias e compartilhar a lista de leitura com DecisionContext.
Caches são derivadas, não serializadas, e não alteram a sequência de RNG.

## Consequências
A ordem morte → fato → perda de testemunha permite relatos reais.
Novos mundos de catálogo mudam de hash e passam a preservar história.
Mundos salvos mantêm suas regras; nenhuma migração foi editada.
Testes cobrem os cinco períodos pela API, causalidade e invalidação do cânone.
Sensor: 1.000 NPCs × 720 ticks, cinco pares alternados após aquecimento, teto 103%.
Evidências finais e verificação independente vivem na spec da issue #91.

O baseline de colapso muda somente nas seeds 11 e 17 porque agora existem eventos
ReportConverted (dois e um, respectivamente); comparação em cópia isolada confirmou
que nenhum evento anterior foi alterado ou removido. O gravador existente regenerou
o baseline sem alterar as asserções.
