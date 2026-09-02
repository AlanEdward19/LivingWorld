using System.Security.Cryptography;
using System.Text;
using LivingWorld.Domain.Cities;
using LivingWorld.Domain.History;
using LivingWorld.Domain.History.Distortion;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.History.Distortion;
using LivingWorld.Simulation.Scenarios;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.History.Books;

/// <summary>Digest determinístico de livros (Fase 10, HIST-09) — usado pelos testes de dois
/// processos.</summary>
public static class HistoryBookDigest
{
    public static string Compute(ulong seed, HistoryRules rules)
    {
        var (world, _) = ScenarioRunner.Create(seed, historyRules: rules);
        var npc = world.Npcs[0];
        var city = world.FindCity(npc.City)
            ?? EnsureCity(world, npc.City);

        var fact = new Fact(new FactId(1), 5, WorldEventKind.Marriage, [npc.Id, new NpcId(2)], npc.City, 0.9, "1|2");
        world.AddFact(fact);

        var report = new ReportState(
            world.NextReportIdAndAdvance(),
            fact.Id,
            npc.City,
            TransmissionMediumType.OralTradition,
            HopCount: 0,
            Weight: fact.Significance,
            CreatedAtTick: 10,
            LastHopTick: 10);
        world.RegisterReport(report);
        CanonSlotManager.Admit(city, report, rules, nowTick: 10);

        var book = new Book(
            world.NextBookIdAndAdvance(),
            report.Id,
            CopyOfBookId: null,
            Lost: false,
            LostAtTick: null,
            RediscoveredAtTick: null);
        world.AddBook(book);

        BookOperations.Copy(world, book, rules, nowTick: 20);

        world.CurrentDate = world.CurrentDate.AddHours(20);
        var ctx = new TickContext(world, world.Rng, world.Scheduler, sink: null);
        BookOperations.MarkLost(world, book.Id, ctx);

        world.CurrentDate = world.CurrentDate.AddHours(5);
        var rediscoveryCtx = new TickContext(world, world.Rng, world.Scheduler, sink: null);
        BookRediscoverySystem.OnRediscovered(world, rediscoveryCtx, book.Id);
        var rediscovered = world.FindBook(book.Id)!;
        var copyBook = world.Books.Single(b => b.CopyOfBookId == book.Id);
        var copyReport = world.FindReport(copyBook.CarriesReportId)!;
        var distance = DistortionEngine.DistanceFromFact(copyReport, fact, rules, world.Rng, world);

        return Hash($"{rediscovered.Lost}:{rediscovered.RediscoveredAtTick}:{copyReport.HopCount}:{distance:F6}");
    }

    private static City EnsureCity(WorldState world, CityId cityId)
    {
        var city = new City(cityId, ScenarioRunner.DefaultVillageLocation, 0, null, AggregatePopulationPool.Empty);
        world.AddCity(city);
        return city;
    }

    private static string Hash(string content) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(content)));
}
