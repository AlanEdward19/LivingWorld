using LivingWorld.Domain;

namespace LivingWorld.Simulation;

/// <summary>
/// Resolve o prefixo mais específico. Dois registros no mesmo prefixo falham na construção
/// (erro de configuração, nunca de invocação).
/// </summary>
public sealed class ExtraordinaryMechanicRegistry : IExtraordinaryMechanicRegistry
{
    private readonly IReadOnlyList<IExtraordinaryMechanic> _mechanics;

    public ExtraordinaryMechanicRegistry(IReadOnlyList<IExtraordinaryMechanic> mechanics)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var mechanic in mechanics)
        {
            if (string.IsNullOrWhiteSpace(mechanic.Prefix))
                throw new ArgumentException("IExtraordinaryMechanic.Prefix não pode ser vazio.");
            if (!seen.Add(mechanic.Prefix))
                throw new ArgumentException(
                    $"IExtraordinaryMechanic: prefixo duplicado '{mechanic.Prefix}'");
        }

        _mechanics = mechanics
            .OrderByDescending(item => item.Prefix.Length)
            .ThenBy(item => item.Prefix, StringComparer.Ordinal)
            .ToList();
    }

    public static ExtraordinaryMechanicRegistry Default { get; } = CreateDefault();

    public IExtraordinaryMechanic? Resolve(string token)
    {
        foreach (var mechanic in _mechanics)
        {
            if (token.StartsWith(mechanic.Prefix, StringComparison.Ordinal))
                return mechanic;
        }

        return null;
    }

    public static ExtraordinaryMechanicRegistry CreateDefault() => new(
    [
        new TeleportMechanic(),
        new ForceActionMechanic(),
        new NpcStatMechanic(),
        new ConstructMechanic(),
        new MovementEffectMechanic(),
        new GravityMechanic(),
        new AttributeMechanic(),
        new AreaSelectorMechanic(),
        new TransferMechanic(),
        new MindMechanic(),
        new LuckMechanic(),
        new CarrierCostMechanic(),
        new HouseholdResourceCostMechanic(),
        new MatterTransmuteMechanic(),
        new SkillMechanic(),
        new EnvironmentTemperatureMechanic(),
        new DimensionMechanic(),
        new FaunaMechanic(),
        new FloraMechanic(),
        new CombatMechanic(),
        new NpcCloneMechanic(),
        new NpcSplitOnDeathMechanic(),
        new NpcReincarnateMechanic(),
        new BondMechanic(),
        new SoulMechanic(),
        new ControlMechanic(),
        new AppearanceMechanic(),
        new ForesightMechanic(),
    ]);
}
