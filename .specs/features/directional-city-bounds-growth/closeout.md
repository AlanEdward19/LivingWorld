# Closeout - Directional City Bounds Growth (specs-closeout 2026-10-12)

Estado: **Spec** (registrada, nao projetada nem implementada).

| Requisito | Estado | Evidencia |
|---|---|---|
| Goal 1: crescimento por borda (minX/maxX/minY/maxY) | Pending - nao implementado | `src/LivingWorld.Domain/Cities/Spatial/CityBoundsResolver.cs` `Resolve` ainda parte de caixa quadrada simetrica (`SideFor`) e encolhe o lado inteiro em `ClampSideAgainstOtherCities` |
| Goal 2: cidade encaixada cresce nos lados abertos | Pending - nao implementado | Mesmo arquivo; unica uniao assimetrica e a de footprints de overflow (`CityGrownBoundsTests`), sem teste por borda |
| AC1: borda do mapa trava so aquela borda | Pending - nao implementado | `ClampOrigin` empurra a origem, nao para uma borda isolada; sem teste |
| AC2: vizinho trava so aquela direcao | Pending - nao implementado | `Resolve` ignora a caixa candidata inteira (`continue`) ou encolhe o lado; sem teste por borda |
| AC3: cidade totalmente cercada para de crescer | Pending - sem teste dedicado | Comportamento de parar existe para a caixa toda, nao provado por borda |
| Teste independente (canto do mapa + vizinho) | Pending - inexistente | Nenhum teste em `tests/` com esse cenario |

Resumo: 0 Verified, 6 Pending, 0 Deferred. Nenhuma entrada em `docs/decisions-log.md` nem fase no ROADMAP.
