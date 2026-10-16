using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Llm;

namespace LivingWorld.Tests.Unit.Llm;

/// <summary>Fase 11, LLM-01/02: <see cref="LlmRules"/> valida limiar de hostilidade e a
/// cobertura exaustiva de <see cref="ConversationCompatibility"/> por <see cref="ActionType"/> —
/// mesmo padrão de <see cref="ActionCatalog"/>.</summary>
public class LlmRulesTests
{
    private static IReadOnlyDictionary<ActionType, ConversationCompatibility> FullCompatibility() =>
        Enum.GetValues<ActionType>().ToDictionary(a => a, _ => ConversationCompatibility.Compatible);

    [Fact]
    public void Create_with_valid_values_succeeds()
    {
        var result = LlmRules.Create(30, FullCompatibility());

        Assert.True(result.IsSuccess);
        Assert.Equal(30, result.Value!.HostileTrustThreshold);
    }

    [Theory]
    [InlineData(-1.0)]
    [InlineData(101.0)]
    public void Create_fails_naming_hostile_trust_threshold_out_of_range(double invalid)
    {
        var result = LlmRules.Create(invalid, FullCompatibility());

        Assert.False(result.IsSuccess);
        Assert.Contains("HostileTrustThreshold", result.Error);
    }

    [Fact]
    public void Create_fails_naming_action_compatibility_missing_entry()
    {
        var incomplete = FullCompatibility().Where(kv => kv.Key != ActionType.Sleep)
            .ToDictionary(kv => kv.Key, kv => kv.Value);

        var result = LlmRules.Create(30, incomplete);

        Assert.False(result.IsSuccess);
        Assert.Contains("Sleep", result.Error);
    }
}
