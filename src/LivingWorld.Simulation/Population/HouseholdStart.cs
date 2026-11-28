using LivingWorld.Domain.Cities.Buildings;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Population;
using LivingWorld.Simulation.Cities;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Objects;

namespace LivingWorld.Simulation.Population;

/// <summary>#260: onde os moradores de uma família autorada começam dentro da casa. Antes todos nasciam na mesma célula da porta e o casal
/// aparecia um dentro do outro. Agora o primeiro fica na célula da casa (a porta) e cada um dos outros começa numa célula livre distinta,
/// de preferência em outro cômodo (sem móvel em cima, longe das paredes de fora não importa: só o chão da planta). Só vale em mundo com
/// catálogo de objetos e cômodos (o sandbox); sem ele nada muda. Função pura do prédio, do catálogo e da ordem dos moradores.</summary>
internal static class HouseholdStart
{
    /// <summary>Move os moradores (a partir do segundo) para células livres distintas da casa; quem não achar lugar fica onde está.</summary>
    public static void Spread(WorldState world, IReadOnlyList<Npc> members, Building house)
    {
        if (members.Count < 2 || house.Position is not { } origin || world.ObjectCatalog is not { Rooms: not null } catalog) return;
        var cached = BuildingLayoutCache.Of(house);
        var rooms = BuildingRooms.Of(house, catalog).Floors[0];
        var taken = new HashSet<CellCoord>();
        foreach (var piece in HomeFurniture.Of(house, catalog))
            if (piece.Spot is { Floor: 0 } spot) taken.Add(spot.Cell);
        var start = new CellCoord(members[0].CurrentLocation.X - origin.X, members[0].CurrentLocation.Y - origin.Y);
        taken.Add(start);
        var startRoom = RoomIndex(rooms, start);
        var usedRooms = new HashSet<int> { startRoom };

        for (int i = 1; i < members.Count; i++)
        {
            CellCoord? pick = null;
            // primeiro um cômodo ainda sem ninguém, depois qualquer célula livre; dentro do cômodo, a mais perto do centro
            for (int pass = 0; pass < 2 && pick is null; pass++)
                for (int r = 0; r < rooms.Rooms.Count && pick is null; r++)
                {
                    if (pass == 0 && usedRooms.Contains(r)) continue;
                    var free = rooms.Rooms[r].Cells.Where(c => !taken.Contains(c) && cached.IsWalkable(0, c)).ToList();
                    if (free.Count == 0) continue;
                    double cx = rooms.Rooms[r].Cells.Average(c => c.X), cy = rooms.Rooms[r].Cells.Average(c => c.Y);
                    pick = free.OrderBy(c => (c.X - cx) * (c.X - cx) + (c.Y - cy) * (c.Y - cy)).ThenBy(c => c.Y).ThenBy(c => c.X).First();
                    usedRooms.Add(r);
                }
            if (pick is not { } cell) continue;
            taken.Add(cell);
            members[i].MoveTo(new CellCoord(origin.X + cell.X, origin.Y + cell.Y), world.CurrentDate.TotalHours);
        }
    }

    private static int RoomIndex(FloorRooms rooms, CellCoord cell)
    {
        for (int r = 0; r < rooms.Rooms.Count; r++)
            if (rooms.Rooms[r].Cells.Contains(cell)) return r;
        return -1;
    }
}
