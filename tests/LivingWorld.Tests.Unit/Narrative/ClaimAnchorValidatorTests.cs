using LivingWorld.Domain.Narrative;
using LivingWorld.Simulation.Narrative;

namespace LivingWorld.Tests.Unit.Narrative;

/// <summary>Fase 12, T3: <see cref="ClaimAnchorValidator"/> (NARR-01..04).</summary>
public class ClaimAnchorValidatorTests
{
    [Fact]
    public void ValidateClaims_rejects_claim_without_event_ids_and_records_the_reason()
    {
        var claim = new NarrativeClaim("um relato sem lastro", []);

        var outcome = ClaimAnchorValidator.ValidateClaims([claim]);

        Assert.Empty(outcome.Approved);
        Assert.Single(outcome.Rejected);
        Assert.Same(claim, outcome.Rejected[0].Claim);
        Assert.Contains("ancoragem", outcome.Rejected[0].Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateClaims_approves_claim_with_non_empty_event_ids()
    {
        var claim = new NarrativeClaim("a vila cresceu", [42]);

        var outcome = ClaimAnchorValidator.ValidateClaims([claim]);

        Assert.Single(outcome.Approved);
        Assert.Same(claim, outcome.Approved[0]);
        Assert.Empty(outcome.Rejected);
    }

    [Fact]
    public void ValidateClaims_partitions_mixed_batch_preserving_approved_order()
    {
        var anchored1 = new NarrativeClaim("evento um", [1]);
        var orphan = new NarrativeClaim("boato sem evento", []);
        var anchored2 = new NarrativeClaim("evento dois", [2]);

        var outcome = ClaimAnchorValidator.ValidateClaims([anchored1, orphan, anchored2]);

        Assert.Equal([anchored1, anchored2], outcome.Approved);
        Assert.Single(outcome.Rejected);
        Assert.Same(orphan, outcome.Rejected[0].Claim);
    }

    [Fact]
    public void ValidateProse_passes_when_numeral_and_proper_noun_trace_to_an_approved_claim()
    {
        var claims = new List<NarrativeClaim> { new("Elandra morreu aos 42 anos.", [1]) };

        var result = ClaimAnchorValidator.ValidateProse(
            "No inverno, Elandra morreu aos 42 anos.", claims);

        Assert.True(result.IsSuccess, result.Error);
    }

    [Fact]
    public void ValidateProse_fails_on_orphan_numeral_not_present_in_any_approved_claim()
    {
        var claims = new List<NarrativeClaim> { new("houve uma colheita fraca", [1]) };

        var result = ClaimAnchorValidator.ValidateProse(
            "A colheita fraca destruiu 500 sacos de trigo.", claims);

        Assert.False(result.IsSuccess);
        Assert.Contains("500", result.Error);
    }

    [Fact]
    public void ValidateProse_fails_on_orphan_proper_noun_not_present_in_any_approved_claim()
    {
        var claims = new List<NarrativeClaim> { new("um mercador chegou à vila", [1]) };

        var result = ClaimAnchorValidator.ValidateProse(
            "O mercador Baltor chegou à vila.", claims);

        Assert.False(result.IsSuccess);
        Assert.Contains("Baltor", result.Error);
    }

    [Fact]
    public void ValidateProse_does_not_flag_grammatical_sentence_initial_capitals_as_orphans()
    {
        var claims = new List<NarrativeClaim> { new("a vila cresceu", [1]) };

        // "A" e "Ela" abrem frase por gramática, não por serem nomes próprios não ancorados.
        var result = ClaimAnchorValidator.ValidateProse(
            "A vila cresceu. Ela recebeu novos moradores.", claims);

        Assert.True(result.IsSuccess, result.Error);
    }

    [Fact]
    public void ValidateProse_passes_trivially_when_prose_has_no_numerals_or_proper_nouns()
    {
        var result = ClaimAnchorValidator.ValidateProse("a colheita foi fraca este ano", []);

        Assert.True(result.IsSuccess, result.Error);
    }
}
