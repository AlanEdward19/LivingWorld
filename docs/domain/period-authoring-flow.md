# Autoria de período — exemplos, fluxo de cadastro e checklist

Volta pro [índice](period-authoring.md).

## Exemplo válido mínimo

Veja `scenarios/default.json` para um período completo funcionando (mapa + população +
comportamento), e os testes em `tests/LivingWorld.Tests/Periods/` para o payload completo com
Economia/Cidades/Dynamics. Um trecho mínimo do bloco `Dynamics` isolado:

```json
{
  "Dynamics": {
    "ProfessionBiases": [{ "ProfessionId": 1, "Weight": 2.0 }]
  }
}
```

## Exemplo inválido (e o erro esperado)

```json
{
  "Dynamics": {
    "TransformationRules": [
      { "Kind": "Merge", "SourceProfessionIds": [1], "TargetProfessionIds": [3] }
    ]
  }
}
```

`Merge` exige 2+ ids de origem (ver [Dynamics](period-authoring-dynamics.md)) — só declarou 1.
Resposta: `400 Bad Request` com corpo determinístico apontando o campo/regra, por exemplo:

```
Dynamics.TransformationRules[]: Merge exige 2+ SourceProfessionIds e exatamente 1 TargetProfessionIds
```

## Fluxo de cadastro (`POST /periods`)

```json
POST /periods
{
  "PeriodId": "medieval-v2",
  "Version": 1,
  "PeriodDefinition": { /* o periodDefinition completo — ver schema.md e dynamics.md */ },
  "Source": "nome-da-ia-ou-autor"
}
```

Respostas:

| Código | Quando |
|---|---|
| `201 Created` | `periodDefinition` válido e `(PeriodId, Version)` inéditos — corpo é o resumo do template registrado |
| `400 Bad Request` | `periodDefinition` reprova alguma validação de forma/referência — corpo nomeia o campo/regra exato |
| `409 Conflict` | já existe um template com o mesmo `PeriodId` **e** `Version` — envie uma `Version` maior pra atualizar |

Depois de cadastrado, o catálogo fica disponível em `GET /periods` (lista, versão mais
recente por período) e `GET /periods/{PeriodId}` (detalhe, inclui o `periodDefinition`
persistido). Para materializar um mundo a partir do template registrado:

```json
POST /worlds/start
{ "PeriodId": "medieval-v2", "Seed": 12345 }
```

`Seed` sobrescreve a semente do template registrado — o mesmo `PeriodId` pode gerar mundos
diferentes e reproduzíveis por seed. `404 Not Found` se `PeriodId` não estiver registrado.

## Checklist de validação antes de enviar

- [ ] Todos os blocos obrigatórios (Mapa, População, Comportamento, Economia, Cidades)
      estão presentes, mesmo quando `EconomyEnabled`/`CitiesEnabled` é `false`.
- [ ] Nenhum campo numérico obrigatório está ausente ou com tipo errado (nome exato, sem
      sinônimo, sem camelCase).
- [ ] Todo `Weight` em `ProfessionBiases`/`SkillBiases` é `> 0`.
- [ ] Todo `Skill` em `SkillBiases` é um dos 13 nomes fechados (ver dynamics.md).
- [ ] Toda `TransformationRules[].Kind` respeita a cardinalidade de origem/destino.
- [ ] Toda profissão citada em `Dynamics` existe em `ProfessionIds` (ou este está vazio).
- [ ] `PeriodId`/`Version` do envelope de cadastro são novos, ou a `Version` foi
      incrementada de propósito (nunca reenviar a mesma versão esperando sobrescrever).
