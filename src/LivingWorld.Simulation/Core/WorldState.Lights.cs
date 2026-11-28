using System.Text.Json.Serialization;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Objects;

namespace LivingWorld.Simulation.Core;

// Parte de WorldState — as luzes ligadas dos objetos do catálogo (#258).
public sealed partial class WorldState
{
    /// <summary>As luzes ligadas. Nulo enquanto nenhuma foi ligada: omitido do snapshot e do hash (o mundo sem luz de catálogo fica idêntico ao de antes).</summary>
    [Canonical, JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public LightLedger? Lights { get; private set; }

    /// <summary>Liga ou desliga a luz da peça; devolve se o estado mudou. Marca o hash como sujo.</summary>
    internal bool SetLight(long building, int piece, bool on)
    {
        if (!on && Lights is null) return false;
        Lights ??= new LightLedger();
        bool changed = Lights.Set(building, piece, on);
        if (Lights.On.Count == 0) Lights = null;
        if (changed) CanonicalHashCache.MarkPropertyDirty(nameof(Lights));
        return changed;
    }

    public bool LightIsOn(long building, int piece) => Lights?.IsOn(building, piece) == true;

    /// <summary>Restaura do snapshot; sem nenhuma luz ligada, volta a nulo.</summary>
    internal void RestoreLights(LightLedger? ledger)
    {
        Lights = ledger is { On.Count: > 0 } ? ledger : null;
        CanonicalHashCache.MarkPropertyDirty(nameof(Lights));
    }
}
