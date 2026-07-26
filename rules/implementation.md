# rules/implementation.md — carregada em: nova feature / fluxo / refactor

## Regras
- Uma unidade = um comportamento. Sem "one-shot hero" costurando 5 sistemas de uma vez.
- Respeite a direção das dependências. `Domain` não referencia nada do projeto;
  `Simulation` → `Domain`; `AI`/`Infrastructure` → `Domain`; `Api`/`Workers` → todos.
  **`Domain` e `Simulation` nunca referenciam `AI`.** Violação = build quebrado, não debate.
- `Domain` é puro: sem I/O, sem EF, sem HTTP, sem log de infra. Só tipos e invariantes.
- Entrada externa (API, LLM, arquivo de cenário) é validada na borda. Erro de negócio é
  explícito (`Result<T>`), não `null` nem exceção.
- Estado inválido não deve ser representável: valide no construtor/factory, não no chamador.
- Sem TODO nem dead code no que entrega. `bash scripts/verify.sh` sai 0.
- Dependência nova ou decisão de arquitetura → PARE e registre ADR em `docs/adr/`.

## Padrões do projeto
- **Sistemas de simulação** implementam uma interface única e são registrados por
  frequência de tick. Um sistema não chama outro direto — comunica por evento.
- **Value objects** para grandezas do domínio (`Money`, `Age`, `SkillLevel`, `WorldDate`).
  Nada de `int` solto atravessando camadas.
- **IDs** são tipados (`NpcId`, `CityId`), não `Guid`/`int` cru — evita troca de argumento.
- Coleções grandes de NPCs usam layout amigável a lote (arrays paralelos / struct of arrays)
  quando o perfil mostrar necessidade. **Não otimize antes de medir.**

## Exemplo — Result explícito em vez de exceção
```csharp
Result<Job> Hire(Npc npc, Workplace shop)   // Ok(job) | Err("no_vacancy") | Err("underage")
```

## Exemplo — sistema de simulação
```csharp
sealed class HungerSystem : ISimulationSystem
{
    public TickFrequency Frequency => TickFrequency.Daily;
    public void Tick(WorldState world, TickContext ctx) { /* muta world, emite eventos */ }
}
```
