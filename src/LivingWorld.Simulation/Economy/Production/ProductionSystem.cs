using LivingWorld.Domain.Economy;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Skills;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Behavior.Needs;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Economy.Labor;
using LivingWorld.Simulation.Economy.Supply;
using LivingWorld.Simulation.Extraordinary.Mechanics;
using LivingWorld.Simulation.Population.Skills;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.Economy.Production;

/// <summary>Converte trabalho + recipe em saída de estoque por <see cref="Workplace"/>,
/// <c>Daily</c>. Respeita <see cref="EconomyRules.Enabled"/>. Quando <see cref="SkillsRules"/>
/// é informado, escala <c>produced</c> pela habilidade média dos trabalhadores presentes antes
/// de contar o bruto — <c>null</c> (default) preserva o comportamento anterior byte-a-byte
/// (nenhum cenário existente injeta habilidade ainda).</summary>
[LivingCapability("PRODUCTION", Events = [WorldEventKind.ResourceLost], Consumers = ["inspector.production"])]
[RunsAfter(typeof(EmploymentSystem))]
[RunsAfter(typeof(SkillPracticeSystem))]
[RunsAfter(typeof(SkillTeachingSystem))]
[RunsAfter(typeof(InputSupplySystem))]
public sealed class ProductionSystem : ISimulationSystem
{
    public const string SystemName = "economy-production";

    private readonly SkillsRules? _skillsRules;

    private readonly List<KeyValuePair<ResourceType, long>> _stockBuffer = [];

    public ProductionSystem(SkillsRules? skillsRules = null) => _skillsRules = skillsRules;

    public string Name => SystemName;
    public TickFrequency Frequency => TickFrequency.Midshift;

    public void Tick(WorldState world, TickContext ctx)
    {
        if (!world.EconomyRules.Enabled) return;

        var catalog = world.EconomyCatalog;
        var rules = world.EconomyRules;
        var vacancyIndex = VacancyIndex.BuildForTick(world);

        foreach (var workplace in world.Workplaces.OrderBy(w => w.Id.Value))
        {
            if (catalog.Recipes.TryGetValue(workplace.LocationType.Id, out var recipe))
                Produce(world, ctx, workplace, recipe, rules, _skillsRules, vacancyIndex);

            // Spoilage é propriedade do estoque, não da recipe — roda em todo Workplace, mesmo
            // um sem produção declarada (mercado, guarda, etc).
            ApplySpoilage(workplace, rules, ctx);
        }

        // AD-143: quem sabe um design trabalha na encomenda (a receita gerada do design) com as horas livres do dia
        LivingWorld.Simulation.Invention.DesignWorkshop.Advance(world, ctx);

        // a despensa da casa também estraga (AD-135), na mesma taxa do recurso, cobrada uma vez por mês (30 dias de perda)
        if (rules.SpoilagePerDayByResource.Count > 0 && world.CurrentDate.Day == 0)
            foreach (var household in world.Households.OrderBy(h => h.Id.Value))
                SpoilHousehold(household, rules, ctx, world.CurrentDate.Calendar.DaysPerMonth);
    }

    private void SpoilHousehold(LivingWorld.Domain.Population.Family.Household household, EconomyRules rules, TickContext ctx, int days)
    {
        _stockBuffer.Clear();
        _stockBuffer.AddRange(household.Stock);
        foreach (var (resource, amount) in _stockBuffer)
        {
            if (!rules.SpoilagePerDayByResource.TryGetValue(resource.Id, out var rate) || rate <= 0 || amount <= 0) continue;
            double monthly = 1 - DeterministicMath.Exp(days * DeterministicMath.Log(1 - Math.Min(rate, 0.999999)));
            long spoiled = Math.Min(amount, CropSystem.RoundStochastic(amount * monthly, ctx, "household-spoilage", household.Id.Value * 64 + resource.Id, ctx.CurrentTick));
            if (spoiled <= 0 || !household.Withdraw(resource, spoiled).IsSuccess) continue;
            ctx.LogEvent(WorldEventKind.ResourceLost, $"{household.Id.Value}|{resource.Id}|{spoiled}", sourceSystem: "ProductionSystem");
        }
    }

    private static void Produce(
        WorldState world, TickContext ctx, Workplace workplace, ProductionRecipe recipe, EconomyRules rules,
        SkillsRules? skillsRules, VacancyIndex vacancyIndex)
    {
        var presentWorkers = vacancyIndex.PresentWorkersAt(workplace);
        // encomenda (AD-144): o artesão da folha faz a peça encomendada em alguma hora do dia, não só se estiver parado na
        // oficina na hora da produção; a sonda da seed 7 achou ferreiros na folha e 14 de 720 dias com alguém na forja
        if (presentWorkers.Count == 0 && ToolMechanic.MakesToOrder(world, recipe)) presentWorkers = ToolMechanic.Hands(world, workplace);
        if (presentWorkers.Count == 0) return;

        if (recipe.RequiresCellResource is { } requiredResource
            && !world.Map.CellAt(workplace.Location).Resources.Any(r => r.Id == requiredResource))
            return;

        int effectiveWorkers = Math.Min(presentWorkers.Count, recipe.MaxWorkersPerCycle);
        double toolFactor = ToolMechanic.OutputFactor(workplace, recipe, effectiveWorkers);
        ToolMechanic.Wear(world, ctx, workplace, recipe, effectiveWorkers);

        // Escala pra baixo pelo insumo mais escasso — nunca debita além do que o próprio
        // estoque do Workplace tem.
        int scale = effectiveWorkers;
        foreach (var (resourceId, perWorker) in recipe.Inputs)
        {
            if (perWorker <= 0) continue;
            long available = workplace.Stock.GetValueOrDefault(new ResourceType(resourceId));
            scale = Math.Min(scale, (int)(available / perWorker));
        }
        if (scale <= 0 && recipe.Inputs.Count > 0) return;

        foreach (var (resourceId, perWorker) in recipe.Inputs)
        {
            workplace.Withdraw(new ResourceType(resourceId), perWorker * scale);
            world.RecordResourceConsumed(new ResourceType(resourceId), perWorker * scale);
        }

        double skillMultiplier = SkillMultiplierOf(presentWorkers, skillsRules);
        double strengthMultiplier = StrengthMultiplierOf(world, presentWorkers, scale);
        double workCapacityMultiplier = WorkCapacityMultiplierOf(world, presentWorkers, scale);

        foreach (var (resourceId, perWorker) in recipe.Outputs)
        {
            if (resourceId == rules.FoodResourceId && CropSystem.FieldsFeedFood(world))
                continue;

            var resource = new ResourceType(resourceId);
            // o ano de safra da região só mexe no alimento (a água do poço não depende de chuva neste modelo)
            double climate = resourceId == rules.FoodResourceId
                ? world.ClimateFactorAt(workplace.Location, ctx.CurrentTick) / 1000.0
                : 1.0;
            long produced = (long)(perWorker * scale * skillMultiplier * strengthMultiplier * workCapacityMultiplier * climate * toolFactor);
            // ferramenta se faz por encomenda (AD-144): a oficina não enche o depósito além do que os clientes usam
            if (world.EconomyCatalog.IsTool(resourceId))
            {
                produced = Math.Min(produced, Math.Max(0, ToolMechanic.OrderBook(world, workplace.City, resourceId) - workplace.Stock.GetValueOrDefault(resource)));
                if (produced <= 0) continue;
            }
            world.RecordResourceProduced(resource, produced); // conta o bruto, antes do clamp de capacidade
            long lost = workplace.Deposit(resource, produced, rules);
            if (lost > 0)
                ctx.LogEvent(WorldEventKind.ResourceLost, $"{workplace.Id.Value}|{resourceId}|{lost}", sourceSystem: "ProductionSystem");
        }
    }

    /// <summary>1.0 (neutro, byte-idêntico ao comportamento sem skills) sem <see cref="SkillsRules"/>
    /// ou sem nenhum trabalhador presente com profissão mapeada; senão
    /// <c>1 + habilidadeMédia/Cap</c> — dono melhor produz mais, nunca menos que o baseline.</summary>
    private static double SkillMultiplierOf(IReadOnlyList<Npc> presentWorkers, SkillsRules? skillsRules)
    {
        if (skillsRules is null) return 1.0;

        double sum = 0;
        int mapped = 0;
        foreach (var npc in presentWorkers)
        {
            if (!skillsRules.SkillByProfession.TryGetValue(npc.Profession.Id, out var skillType)) continue;
            // quem trabalha no que combina com ele rende mais (AD-136)
            sum += (1.0 + npc.Skills.Get(skillType) / skillsRules.Cap) * VocationMechanic.OutputFit(skillsRules, npc);
            mapped++;
        }
        if (mapped == 0) return 1.0;

        return sum / mapped;
    }

    /// <summary>Segundo multiplicador de <c>attribute.strength</c> sobre a taxa de
    /// skill/RateGene — média dos trabalhadores que entram em <paramref name="scale"/>.</summary>
    private static double StrengthMultiplierOf(WorldState world, IReadOnlyList<Npc> presentWorkers, int scale)
    {
        int count = Math.Min(scale, presentWorkers.Count);
        if (count <= 0) return 1.0;

        double sum = 0;
        for (int i = 0; i < count; i++)
            sum += AttributeMechanic.StrengthMultiplier(world, presentWorkers[i]);
        return sum / count;
    }

    /// <summary>4º fator — média de <see cref="BodyMechanic.WorkCapacityMultiplier"/>
    /// sobre os trabalhadores em <paramref name="scale"/> (neutro 1.0 se BodyRules off).</summary>
    private static double WorkCapacityMultiplierOf(WorldState world, IReadOnlyList<Npc> presentWorkers, int scale)
    {
        int count = Math.Min(scale, presentWorkers.Count);
        if (count <= 0) return 1.0;

        double sum = 0;
        for (int i = 0; i < count; i++)
            sum += BodyMechanic.WorkCapacityMultiplier(world, presentWorkers[i]);
        return sum / count;
    }

    private void ApplySpoilage(Workplace workplace, EconomyRules rules, TickContext ctx)
    {
        if (rules.SpoilagePerDayByResource.Count == 0) return;

        // cópia reaproveitada (retirar durante a enumeração do estoque não pode); mesma ordem de antes
        _stockBuffer.Clear();
        _stockBuffer.AddRange(workplace.Stock);
        foreach (var (resource, amount) in _stockBuffer)
        {
            if (!rules.SpoilagePerDayByResource.TryGetValue(resource.Id, out var rate) || rate <= 0) continue;
            // arredondamento sem viés: com taxa anual realista (fração de unidade por dia) o estoque pequeno também perde
            long spoiled = CropSystem.RoundStochastic(amount * rate, ctx, "spoilage", workplace.Id.Value * 64 + resource.Id, ctx.CurrentTick);
            if (spoiled <= 0) continue;

            workplace.Withdraw(resource, Math.Min(spoiled, amount));
            ctx.LogEvent(WorldEventKind.ResourceLost, $"{workplace.Id.Value}|{resource.Id}|{spoiled}", sourceSystem: "ProductionSystem");
        }
    }
}
