# #92: opcionais preenchidos da ficha de NPC

## Escopo
Testes de integração do contrato congelado `shape.npc-inspect.json`.
Sem alteração de produção, contrato público ou dependência.

## Critérios de aceite
- AC-1: Quando um NPC estiver comendo, a resposta HTTP deve conter `food`
  preenchido e cumprir o contrato tipado inteiro, incluindo `resourceId` numérico.
- AC-2: A busca de refeição deve avançar hora a hora, no máximo 72 horas;
  sem observação, o teste deve falhar explicitamente, nunca pular.
- AC-3: Quando um cenário criar uma família autorada, a ficha do filho deve
  conter `motherId` e `fatherId` preenchidos no formato `{value: number}`,
  iguais aos IDs da mãe e do pai criados pelo cenário.
- AC-4: Mutações de tipo em `food.resourceId`, `motherId` e `fatherId`
  devem ser detectadas por assertions dos novos testes.
- AC-5: Os testes devem rodar no gate de integração em poucos segundos.

## Premissas
Família autorada é alternativa permitida pela issue à espera de nascimento.
O relógio do host de testes é manual e a seed do mundo de refeição é 1.
O arquivo de contrato existente continua sendo a fonte dos tipos aceitos.

## Entrega
Uma unidade: dois testes reais de HTTP no arquivo PooledNpcContractTests.cs.
Verificação: teste focado antes/depois, integração, verify.sh e Verifier
independente com mutações em cópia isolada. Commit local após gate verde.

## Desempenho
Nenhum código de cliente ou motor muda; custo por quadro e por NPC-tick
permanece idêntico. Medir o tempo do teste focado antes e depois.
