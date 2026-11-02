# Execução #92

## Unidade
Dois testes HTTP em `Visual/PooledNpcContractTests.cs`.
Contrato e implementação de produção preservados.

## Adequação (arquivo relativo a tests/LivingWorld.Tests.Integration)
| Critério | Evidência e assertion | Resultado exigido |
|---|---|---|
| AC-1 | `Visual/PooledNpcContractTests.cs:138` `Assert.True(body["food"] is not null, ...)` | comida presente |
| AC-1 | `Visual/PooledNpcContractTests.cs:141` `Assert.True(errors.Count == 0, ...)` | forma tipada inteira |
| AC-2 | `Visual/PooledNpcContractTests.cs:146` `Assert.Fail(...)` | falha se 72 horas sem refeição |
| AC-3 | `Visual/PooledNpcContractTests.cs:170` `Assert.True(body["motherId"] is not null, ...)` | mãe presente |
| AC-3 | `Visual/PooledNpcContractTests.cs:171` `Assert.True(body["fatherId"] is not null, ...)` | pai presente |
| AC-3 | `Visual/PooledNpcContractTests.cs:174` `Assert.True(errors.Count == 0, ...)` | forma tipada inteira |
| AC-3 | `Visual/PooledNpcContractTests.cs:175` `Assert.Equal(...Ana..., ...motherId...)` | ID real da mãe |
| AC-3 | `Visual/PooledNpcContractTests.cs:176` `Assert.Equal(...Beto..., ...fatherId...)` | ID real do pai |

## Necessidade
O teste de refeição cobre AC-1 e AC-2; o de família cobre AC-3.
As assertions de HTTP e presença são premissas necessárias às assertions de forma.
As duas novas funções têm critério associado; nenhum teste removido ou pulado.
AC-4 fica a cargo do sensor independente; AC-5 pela duração do runner.
Seguidas `rules/tests.md` e `rules/implementation.md`.

## Medição
Antes: 2 testes focados, 0 falhas, duração reportada 1 s.
Depois: 4 testes focados, 0 falhas, duração reportada 1 s.
A mudança só adiciona testes; nenhum byte de fonte do motor ou cliente muda.
Custo por NPC-tick e FPS são idênticos por construção, sem benchmark runtime.

## Verificação
Gate e sensor: ver `validation.md` para resultados finais.
