using System.Net.Http.Json;
using System.Text.Json;
using LivingWorld.Api.Visual.Catalogs;
using LivingWorld.Domain.Behavior;
using LivingWorld.Tests.Shared;

namespace LivingWorld.Tests.Integration.NpcDirectory;

/// <summary>godot-api-readiness C11/C12: um rótulo servido por valor de <see cref="ActionType"/>.</summary>
public class ActionLabelCatalogTests : IClassFixture<LivingWorldApiFactory>
{
    private readonly LivingWorldApiFactory _factory;

    public ActionLabelCatalogTests(LivingWorldApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Endpoint_lists_every_action_once()
    {
        var items = await _factory.CreateClient().GetFromJsonAsync<JsonElement>("/visual/catalog/actions");

        var served = items.EnumerateArray().Select(i => i.GetProperty("actionType").GetInt32()).ToArray();
        var expected = Enum.GetValues<ActionType>().Select(a => (int)a).Order().ToArray();
        Assert.Equal(9, expected.Length);
        Assert.Equal(expected, served.Order().ToArray());
        Assert.All(items.EnumerateArray(), i => Assert.False(string.IsNullOrWhiteSpace(i.GetProperty("label").GetString())));
    }

    [Fact]
    public void Every_enum_member_has_label()
    {
        foreach (var action in Enum.GetValues<ActionType>())
            Assert.False(string.IsNullOrWhiteSpace(ActionLabelCatalog.LabelOf(action)), action.ToString());

        Assert.Throws<ArgumentOutOfRangeException>(() => ActionLabelCatalog.LabelOf((ActionType)999));
    }
}
