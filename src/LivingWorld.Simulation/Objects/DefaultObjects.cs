using System.Text.Json.Nodes;

namespace LivingWorld.Simulation.Objects;

/// <summary>Mobília que o cliente desenhava a partir de <c>assets.json</c> (#40), agora como blocos de cenário (#47 fatia 1, ADR-0034).
/// Cada peça é uma caixa só, do mesmo tamanho e cor de antes: o interior renderizado não muda.
/// <c>offset</c> é o centro da parte relativo à base do objeto (chão), por isso y = altura/2.</summary>
public static class DefaultObjects
{
    private static readonly (string Id, string Category, double X, double Y, double Z, string Color)[] Pieces =
    [
        ("table", "furniture", 0.8, 0.4, 0.5, "8a6a43"), ("bench", "furniture", 0.7, 0.25, 0.25, "7a5a38"),
        ("crate", "storage", 0.4, 0.4, 0.4, "a27b52"), ("barrel", "storage", 0.35, 0.5, 0.35, "6b4a2b"),
        ("counter", "furniture", 0.9, 0.45, 0.3, "5c4630"), ("shelf", "storage", 0.7, 0.8, 0.2, "6f5436"),
        ("bed", "bed", 0.5, 0.2, 0.9, "d8cfb8"), ("chest", "storage", 0.5, 0.3, 0.3, "4f3a26"),
        ("anvil", "tool", 0.35, 0.3, 0.2, "5b6068"),
    ];

    /// <summary>Pontos de uso (#47 fatia 2): objeto → (nome, ActionType, pose, offset local, facing em graus). Ids de ação: Eat 0, Sleep 1, Work 2, Socialize 3.</summary>
    private static readonly Dictionary<string, (string Name, int Action, string Pose, double X, double Y, double Z, double Facing)> UsePoints = new()
    {
        ["bed"] = ("lie", 1, "sleep", 0.0, 0.05, 0.3, 0.0),
        ["table"] = ("eat", 0, "eat", 0.0, 0.0, 0.45, 180.0),
        ["bench"] = ("sit", 3, "socialize", 0.0, 0.0, 0.3, 180.0),
        ["anvil"] = ("hammer", 2, "work", 0.0, 0.0, 0.3, 180.0),
        ["counter"] = ("serve", 2, "work", 0.0, 0.0, -0.35, 0.0),
    };

    /// <summary>Acrescenta <c>objectsFormat</c>, <c>Objects</c> e <c>Furnishings</c> ao JSON do período.</summary>
    public static void AddTo(JsonObject root)
    {
        var objects = new JsonArray();
        foreach (var (id, category, x, y, z, color) in Pieces)
            objects.Add(new JsonObject
            {
                ["id"] = id,
                ["category"] = category,
                ["variationSeed"] = 0,
                ["shape"] = new JsonArray(new JsonObject
                {
                    ["shape"] = "box",
                    ["size"] = new JsonArray(x, y, z),
                    ["offset"] = new JsonArray(0.0, y / 2.0, 0.0),
                    ["color"] = color,
                }),
                ["usePoints"] = UsePointsOf(id),
            });
        root["objectsFormat"] = 1;
        root["Objects"] = objects;
        root["Furnishings"] = new JsonObject
        {
            ["default"] = Ids("table", "bench", "crate"),
            ["1"] = Ids("counter", "shelf", "crate", "barrel"),
            ["2"] = Ids("table", "bench", "barrel"),
            ["3"] = Ids("bed", "table", "chest"),
            ["4"] = Ids("anvil", "shelf", "barrel", "crate"),
            ["10"] = Ids("bed", "bed", "table", "chest"),
            ["11"] = Ids("counter", "table", "shelf", "crate"),
        };
    }

    private static JsonArray UsePointsOf(string id) =>
        UsePoints.TryGetValue(id, out var p)
            ? new JsonArray(new JsonObject
            {
                ["name"] = p.Name,
                ["offset"] = new JsonArray(p.X, p.Y, p.Z),
                ["facing"] = p.Facing,
                ["action"] = p.Action,
                ["pose"] = p.Pose,
            })
            : new JsonArray();

    private static JsonArray Ids(params string[] ids) => new(ids.Select(id => (JsonNode?)JsonValue.Create(id)).ToArray());
}
