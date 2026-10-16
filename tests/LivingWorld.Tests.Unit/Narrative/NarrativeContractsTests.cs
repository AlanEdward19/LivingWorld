using System.Reflection;
using LivingWorld.Domain.Narrative;
using LivingWorld.Domain.Shared;

namespace LivingWorld.Tests.Unit.Narrative;

/// <summary>Fase 12, T1: contratos narrativos estruturados (NARR-01..04).</summary>
public class NarrativeContractsTests
{
    [Fact]
    public void NarrativeClaim_creation_preserves_text_and_event_ids()
    {
        var claim = new NarrativeClaim("A vila cresceu.", [1, 2, 3]);

        Assert.Equal("A vila cresceu.", claim.Text);
        Assert.Equal(new long[] { 1, 2, 3 }, claim.EventIds);
    }

    [Fact]
    public void NarrativeClaim_allows_constructing_with_empty_event_ids()
    {
        // NARR-02 exige que um claim SEM eventIds válidos seja descartado — mas essa é uma
        // regra de validação (ClaimAnchorValidator, T3), não uma restrição do tipo de dado em
        // si. O contrato precisa aceitar a lista vazia para que o validador tenha algo a
        // reprovar; a reprovação em si é coberta pelos testes de T3.
        var claim = new NarrativeClaim("texto sem ancoragem", []);

        Assert.Empty(claim.EventIds);
    }

    [Fact]
    public void NarrativeDraft_creation_preserves_window_and_claims()
    {
        var claims = new List<NarrativeClaim> { new("evento A", [10]) };
        var location = new CityId(Guid.NewGuid());

        var draft = new NarrativeDraft(location, PeriodStartTick: 100, PeriodEndTick: 200, claims);

        Assert.Equal(location, draft.Location);
        Assert.Equal(100, draft.PeriodStartTick);
        Assert.Equal(200, draft.PeriodEndTick);
        Assert.Same(claims, draft.Claims);
    }

    [Fact]
    public void NarrativeDraft_allows_null_location_for_world_level_windows()
    {
        var draft = new NarrativeDraft(null, 0, 10, []);

        Assert.Null(draft.Location);
    }

    [Theory]
    [InlineData(NarrativeType.Chronicle)]
    [InlineData(NarrativeType.Biography)]
    [InlineData(NarrativeType.Report)]
    public void NarrativeDocument_creation_preserves_id_type_prose_and_claims(NarrativeType type)
    {
        var claims = new List<NarrativeClaim> { new("evento A", [1]) };
        var doc = new NarrativeDocument(new NarrativeId(7), type, "Prosa final.", claims);

        Assert.Equal(new NarrativeId(7), doc.Id);
        Assert.Equal(type, doc.Type);
        Assert.Equal("Prosa final.", doc.Prose);
        Assert.Same(claims, doc.Claims);
    }

    [Fact]
    public void NarrativeId_formats_as_narrative_prefixed_string()
    {
        Assert.Equal("narrative-7", new NarrativeId(7).ToString());
    }

    [Fact]
    public void Narrative_contract_types_are_sealed_immutable_records_without_setters()
    {
        foreach (var type in new[] { typeof(NarrativeClaim), typeof(NarrativeDraft), typeof(NarrativeDocument) })
        {
            Assert.True(type.IsSealed, $"{type.Name} deveria ser sealed");

            foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                var set = prop.SetMethod;
                if (set is null || !set.IsPublic)
                    continue;
                Assert.Contains(
                    "IsExternalInit",
                    set.ReturnParameter.GetRequiredCustomModifiers().Select(t => t.Name));
            }
        }
    }
}
