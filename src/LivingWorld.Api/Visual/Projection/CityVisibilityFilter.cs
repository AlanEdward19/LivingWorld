using LivingWorld.Domain.Geography;
using LivingWorld.Simulation.Visibility;

namespace LivingWorld.Api.Visual.Projection;

/// <summary>Fase 15, T7 (VTT-08, VTT-09): aplica FOW por raio sobre um <see cref="CitySnapshot"/>
/// já montado — nunca reconsulta o mundo, só filtra o que <see cref="CityProjector"/> projetou.
/// SPEC_DEVIATION: design.md descreve <c>ApplyFog(snapshot, player)</c> dentro de
/// <c>PlayerVisibilityService</c> (Simulation/Visibility), mas <see cref="CitySnapshot"/> é tipo
/// de Api (Simulation não referencia Api) — o predicado geométrico (<see
/// cref="PlayerVisibilityService.CanSee"/>) mora em Simulation; o filtro sobre o DTO visual mora
/// aqui. <c>Buildings</c> nunca é filtrado: <c>Building</c> não tem <c>CellCoord</c> própria
/// (T5) — não há posição para aplicar FOW ainda.</summary>
public static class CityVisibilityFilter
{
    public static CitySnapshot ApplyFog(CitySnapshot snapshot, CellCoord playerLocation, bool adminOverride) =>
        adminOverride
            ? snapshot
            : snapshot with
            {
                Residents = snapshot.Residents
                    .Where(r => PlayerVisibilityService.CanSee(r.Location, playerLocation, adminOverride: false))
                    .ToList(),
            };
}
