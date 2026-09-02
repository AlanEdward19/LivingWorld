namespace LivingWorld.Domain.Geography.Spatial;

/// <summary>Nível de espaço endereçável (Fase 15.1, T46/ADR-0018) — mesmos três níveis de
/// `VisualScopeKind` (`src/LivingWorld.Api/Visual/VisualScope.cs:4`), definido em Domain porque
/// <see cref="SpatialAddress"/> e futuros consumidores (T21 `SpatialPortal`, T47 ocupação de NPC)
/// vivem em Domain/Simulation, não em Api.</summary>
public enum SpaceKind { World, City, Building }

/// <summary>Andar sem unidade física (Fase 15.1, T46/ADR-0018) — inteiro livre, <see
/// cref="Ground"/> = 0. Nunca <c>[Canonical]</c> por si só: quem persiste um andar (T47) decide
/// isso na sua própria estrutura.</summary>
public readonly record struct FloorLevel(int Value)
{
    public static readonly FloorLevel Ground = new(0);
}

/// <summary>Endereço espacial completo (Fase 15.1, T46/ADR-0018): nível, referência ao
/// City/Building (ignorada para <see cref="SpaceKind.World"/>, mesma convenção de
/// <c>VisualScope.RefId</c>), andar e célula local dentro daquele espaço/andar.</summary>
public readonly record struct SpatialAddress(SpaceKind Kind, Guid RefId, FloorLevel Floor, CellCoord Cell);
