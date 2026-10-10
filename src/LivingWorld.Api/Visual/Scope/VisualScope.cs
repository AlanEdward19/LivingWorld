namespace LivingWorld.Api.Visual.Scope;

public enum VisualScopeKind { World, City, Interior }

public enum ViewerMode { Spectator, Player }

/// <summary>Escopo endereçável para subscribe/replay realtime.
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
