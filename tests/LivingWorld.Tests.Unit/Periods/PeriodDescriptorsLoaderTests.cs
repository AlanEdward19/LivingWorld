using System.Text.Json.Nodes;
using LivingWorld.Simulation.Periods;

namespace LivingWorld.Tests.Unit.Periods;

/// <summary>Fase 15.1, T48 (backend-gaps.md G8): descritores legíveis por categoria — só existem
/// se o período os declarar; ausência nunca é falha, sempre lista vazia.</summary>
public class PeriodDescriptorsLoaderTests
{
    private static JsonObject RootWithDescriptors(JsonObject descriptors) => new() { ["Descriptors"] = descriptors };

    [Fact]
    public void Missing_Descriptors_block_returns_empty_for_every_category()
    {
        var result = PeriodDescriptorsLoader.Load(new JsonObject().ToJsonString());

        Assert.True(result.IsSuccess, result.Error);
        var d = result.Value!;
        Assert.Empty(d.Terrain);
        Assert.Empty(d.Biome);
        Assert.Empty(d.Resource);
        Assert.Empty(d.Culture);
        Assert.Empty(d.LocationType);
        Assert.Empty(d.BuildingType);
        Assert.Empty(d.Action);
    }

    [Fact]
    public void Happy_path_parses_a_descriptor_in_every_category()
    {
        var descriptors = new JsonObject();
        foreach (var category in new[] { "Terrain", "Biome", "Resource", "Culture", "LocationType", "BuildingType", "Action" })
            descriptors[category] = new JsonArray(new JsonObject { ["Id"] = 1, ["Name"] = $"{category}-1" });

        var result = PeriodDescriptorsLoader.Load(RootWithDescriptors(descriptors).ToJsonString());

        Assert.True(result.IsSuccess, result.Error);
        var d = result.Value!;
        Assert.Equal("Terrain-1", Assert.Single(d.Terrain).Name);
        Assert.Equal("Biome-1", Assert.Single(d.Biome).Name);
        Assert.Equal("Resource-1", Assert.Single(d.Resource).Name);
        Assert.Equal("Culture-1", Assert.Single(d.Culture).Name);
        Assert.Equal("LocationType-1", Assert.Single(d.LocationType).Name);
        Assert.Equal("BuildingType-1", Assert.Single(d.BuildingType).Name);
        Assert.Equal("Action-1", Assert.Single(d.Action).Name);
    }

    [Fact]
    public void Descriptor_carries_explanation_range_and_unit_when_declared()
    {
        var descriptors = new JsonObject
        {
            ["Resource"] = new JsonArray(new JsonObject
            {
                ["Id"] = 1,
                ["Name"] = "Grão",
                ["Explanation"] = "Alimento básico",
                ["RangeMin"] = 0,
                ["RangeMax"] = 1000,
                ["Unit"] = "kg",
            }),
        };

        var result = PeriodDescriptorsLoader.Load(RootWithDescriptors(descriptors).ToJsonString());

        Assert.True(result.IsSuccess, result.Error);
        var descriptor = Assert.Single(result.Value!.Resource);
        Assert.Equal("Grão", descriptor.Name);
        Assert.Equal("Alimento básico", descriptor.Explanation);
        Assert.Equal(0, descriptor.RangeMin);
        Assert.Equal(1000, descriptor.RangeMax);
        Assert.Equal("kg", descriptor.Unit);
    }

    [Fact]
    public void Descriptor_without_explanation_or_range_leaves_those_fields_null_not_invented()
    {
        var descriptors = new JsonObject
        {
            ["Terrain"] = new JsonArray(new JsonObject { ["Id"] = 1, ["Name"] = "Grama" }),
        };

        var result = PeriodDescriptorsLoader.Load(RootWithDescriptors(descriptors).ToJsonString());

        var descriptor = Assert.Single(result.Value!.Terrain);
        Assert.Null(descriptor.Explanation);
        Assert.Null(descriptor.RangeMin);
        Assert.Null(descriptor.RangeMax);
        Assert.Null(descriptor.Unit);
    }

    [Fact]
    public void Missing_Id_fails_naming_the_field()
    {
        var descriptors = new JsonObject { ["Terrain"] = new JsonArray(new JsonObject { ["Name"] = "Grama" }) };

        var result = PeriodDescriptorsLoader.Load(RootWithDescriptors(descriptors).ToJsonString());

        Assert.False(result.IsSuccess);
        Assert.Contains("Descriptors.Terrain[].Id", result.Error);
    }

    [Fact]
    public void Missing_Name_fails_naming_the_field()
    {
        var descriptors = new JsonObject { ["Biome"] = new JsonArray(new JsonObject { ["Id"] = 1 }) };

        var result = PeriodDescriptorsLoader.Load(RootWithDescriptors(descriptors).ToJsonString());

        Assert.False(result.IsSuccess);
        Assert.Contains("Descriptors.Biome[].Name", result.Error);
    }

    [Fact]
    public void RangeMin_greater_than_RangeMax_is_rejected()
    {
        var descriptors = new JsonObject
        {
            ["Resource"] = new JsonArray(new JsonObject
            {
                ["Id"] = 1,
                ["Name"] = "Grão",
                ["RangeMin"] = 100,
                ["RangeMax"] = 10,
            }),
        };

        var result = PeriodDescriptorsLoader.Load(RootWithDescriptors(descriptors).ToJsonString());

        Assert.False(result.IsSuccess);
        Assert.Contains("RangeMin", result.Error);
    }
}
