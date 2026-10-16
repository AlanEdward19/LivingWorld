# ADR-0034 — Diversidade genética calibrada (FAM-32)

## Status
Aceito pelo usuário em 2026-10-15, issue #65, opção B.

## Contexto
O critério antigo exigia CV real nunca inferior ao controle neutro.
AD-088 mediu a redução sob seleção direcional; o teste barrava apenas aumento.
Assim, texto e gate protegiam propriedades diferentes.

## Decisão
Preservar as regras de herança e substituir o critério por uma faixa empírica.
Após medir CV zero nas 20 seeds, o usuário ampliou explicitamente o escopo:
corrigir também o colapso demográfico no cenário completo, sem cenário substituto.
O cortejo deve escolher candidatos compatíveis antes de comparar atração;
um favorito incompatível não pode bloquear todos os parceiros válidos.
Após ordenar pretendentes, a elegibilidade é revalidada: quem já foi escolhido
não inicia outro cortejo no mesmo tick. O índice social e os buffers só derivam dados.
A convivência é observada no meio do turno (Midshift), junto à produção:
à meia-noite os colegas já estão em casa e a próxima geração não cria vínculos externos.
Nascimento preserva a cidade materna; o domicílio do casamento preserva a cidade do chefe.
Sem isso, a geração seguinte recebe CityId vazio e não pode acessar vagas locais.
A diferença pareada de CV de Vitality deve ficar dentro da faixa calibrada
com 20 seeds versionadas em `tests/baselines/`, com incerteza amostral registrada.
Um atributo sem seleção direta de mortalidade é controle negativo,
comparado ao braço neutro nas mesmas seeds, com faixa também medida.
O CV de Vitality não pode colapsar em checkpoints até 100 anos;
o piso vem do cenário medido, com prova curta e sensor longo.
O teste deve rejeitar um braço com seleção de mortalidade muito forte.
Nenhum limiar pode ser escolhido só para fazer o modelo passar.

## Alternativas
Ajustar parâmetros de mutação ou mortalidade só para inverter a diferença
foi rejeitado na opção B. Correções causais do colapso foram autorizadas depois.

## Consequências
Baseline tem procedência por seed e atualização explícita, nunca no gate.
FAM-33 e os contrafactuais ambientais continuam obrigatórios.
A seleção real pode reduzir diversidade; a regressão é perda além da faixa.
A implementação deve demonstrar discriminação antes de marcar FAM-32 Verified.

## Fontes
- Issue #65; AD-088; decisão do usuário nesta sessão.
