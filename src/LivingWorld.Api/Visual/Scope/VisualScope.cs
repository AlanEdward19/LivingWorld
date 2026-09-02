namespace LivingWorld.Api.Visual;

/// <summary>Fase 15, T1 (VTT-01, VTT-04): granularidade de foco — mundo, cidade ou interior.</summary>
public enum VisualScopeKind { World, City, Interior }

/// <summary>Fase 15, T1 (VTT-07, VTT-09): modo do viewer — espectador/admin ou personagem.</summary>
public enum ViewerMode { Spectator, Player }

/// <summary>Fase 15, T1 (VTT-01, VTT-04): escopo endereçável para subscribe/replay realtime.
/// <paramref name="RefId"/> é ignorado para <see cref="VisualScopeKind.World"/>.</summary>
public sealed record VisualScope(VisualScopeKind Kind, string RefId)
{
    public string ScopeKey => Kind switch
    {
        VisualScopeKind.World => "world",
        VisualScopeKind.City => $"city:{RefId}",
        VisualScopeKind.Interior => $"interior:{RefId}",
        _ => throw new ArgumentOutOfRangeException(nameof(Kind), Kind, null)
    };
}
