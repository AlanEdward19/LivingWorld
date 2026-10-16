using LivingWorld.Domain.Performance;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Infrastructure.EventLog;
using LivingWorld.Simulation.Population.Archive;

namespace LivingWorld.Tests.Integration.Infrastructure;

public sealed class ColdTierPersistenceTests
{
    private static readonly WorldCalendar Calendar = new(HoursPerDay: 24, DaysPerMonth: 30, MonthsPerYear: 12);

    [Fact]
    public void Round_trip_single_summary_preserves_all_fields()
    {
        string path = NewTempPath();
        var summary = Sample(1, "Aldric", Sex.Male, death: true);

        var persistence = new ColdTierPersistence(path);
        persistence.Save(ToDict(summary), Calendar);

        var loaded = Assert.Single(persistence.Load(Calendar).Values);
        Assert.Equal(summary, loaded);
    }

    [Fact]
    public void Round_trip_multiple_summaries_by_id()
    {
        string path = NewTempPath();
        var entries = new[]
        {
            Sample(1, "Aldric", Sex.Male, death: true),
            Sample(2, "Brynn", Sex.Female, death: false),
            Sample(3, "Cedric", Sex.Male, death: true, professionId: 4),
        };

        var persistence = new ColdTierPersistence(path);
        persistence.Save(ToDict(entries), Calendar);

        var loaded = persistence.Load(Calendar);
        Assert.Equal(3, loaded.Count);
        Assert.Equal(entries[0], loaded[1]);
        Assert.Equal(entries[1], loaded[2]);
        Assert.Equal(entries[2], loaded[3]);
    }

    [Fact]
    public void Round_trip_null_death_date()
    {
        string path = NewTempPath();
        var summary = Sample(9, "Alive-ish", Sex.Female, death: false);

        new ColdTierPersistence(path).Save(ToDict(summary), Calendar);

        Assert.Null(new ColdTierPersistence(path).Load(Calendar)[9].DeathDate);
    }

    [Theory]
    [InlineData(ColdTierCompressionCodec.GZip)]
    [InlineData(ColdTierCompressionCodec.Brotli)]
    public void Round_trip_with_compression_codec(ColdTierCompressionCodec codec)
    {
        string path = NewTempPath();
        var entries = Enumerable.Range(1, 5)
            .Select(i => Sample(i, $"Npc-{i}", i % 2 == 0 ? Sex.Female : Sex.Male, death: i % 3 == 0))
            .ToArray();

        var persistence = new ColdTierPersistence(path, codec);
        persistence.Save(ToDict(entries), Calendar);

        Assert.Equal(codec, persistence.Codec);
        Assert.Equal(entries.Length, persistence.Load(Calendar).Count);
    }

    [Fact]
    public void String_interning_deduplicates_repeated_names()
    {
        string pathUnique = NewTempPath();
        string pathRepeated = NewTempPath();
        var uniqueEntries = Enumerable.Range(1, 20)
            .Select(i => Sample(i, $"Name-{i}", Sex.Male, death: true))
            .ToArray();
        var repeatedEntries = Enumerable.Range(1, 20)
            .Select(i => Sample(i, i % 4 == 0 ? "Smith" : "Jones", Sex.Male, death: true))
            .ToArray();

        new ColdTierPersistence(pathRepeated, ColdTierCompressionCodec.Brotli).Save(ToDict(repeatedEntries), Calendar);
        new ColdTierPersistence(pathUnique, ColdTierCompressionCodec.Brotli).Save(ToDict(uniqueEntries), Calendar);

        long repeatedSize = new ColdTierPersistence(pathRepeated).FileSizeBytes;
        long uniqueSize = new ColdTierPersistence(pathUnique).FileSizeBytes;

        Assert.True(repeatedSize < uniqueSize);
        Assert.All(new ColdTierPersistence(pathRepeated).Load(Calendar).Values,
            s => Assert.Contains(s.Name, new[] { "Smith", "Jones" }));
    }

    [Fact]
    public void TryGet_returns_single_entry_without_full_scan_contract()
    {
        string path = NewTempPath();
        var target = Sample(42, "Target", Sex.Female, death: true);
        var persistence = new ColdTierPersistence(path);
        persistence.Save(ToDict(target, Sample(7, "Other", Sex.Male, death: true)), Calendar);

        Assert.Equal(target, persistence.TryGet(42, Calendar));
        Assert.Null(persistence.TryGet(99, Calendar));
    }

    [Fact]
    public void Load_missing_file_returns_empty_dictionary()
    {
        string path = NewTempPath();
        var loaded = new ColdTierPersistence(path).Load(Calendar);
        Assert.Empty(loaded);
    }

    [Fact]
    public void Load_throws_on_invalid_magic()
    {
        string path = NewTempPath();
        File.WriteAllBytes(path, [0xDE, 0xAD, 0xBE, 0xEF, 0xCA, 0xFE, 0xBA, 0xBE]);

        var ex = Assert.Throws<InvalidDataException>(() => new ColdTierPersistence(path).Load(Calendar));
        Assert.Contains("magic", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Load_throws_on_corrupt_compressed_payload()
    {
        string path = NewTempPath();
        using (var fs = File.Create(path))
        {
            fs.Write("LWCLD1\0"u8);
            fs.WriteByte((byte)ColdTierCompressionCodec.Brotli);
            ColdTierPersistenceTestsHelpers.WriteInt32(fs, 32);
            fs.Write([0x01, 0x02, 0x03, 0x04]);
        }

        Assert.Throws<InvalidDataException>(() => new ColdTierPersistence(path).Load(Calendar));
    }

    [Fact]
    public void Bytes_per_archived_npc_per_year_stays_under_perf_rules_ceiling()
    {
        const int npcCount = 100;
        const int simulatedYears = 10;
        string path = NewTempPath();
        var entries = Enumerable.Range(1, npcCount)
            .Select(i => Sample(
                i,
                i % 5 == 0 ? "Farmer" : "Villager",
                i % 2 == 0 ? Sex.Female : Sex.Male,
                death: true,
                cultureId: i % 3,
                professionId: i % 4))
            .ToArray();

        new ColdTierPersistence(path, ColdTierCompressionCodec.Brotli).Save(ToDict(entries), Calendar);

        double bytesPerNpcPerYear = (double)new ColdTierPersistence(path).FileSizeBytes / (npcCount * simulatedYears);
        Assert.True(
            bytesPerNpcPerYear <= PerfRules.Default.MaxBytesPerAliveNpcPerYear,
            $"{bytesPerNpcPerYear:F1} bytes/NPC/ano excede o teto de {PerfRules.Default.MaxBytesPerAliveNpcPerYear}");
    }

    [Fact]
    public void Load_throws_when_calendar_mismatches_file()
    {
        string path = NewTempPath();
        new ColdTierPersistence(path).Save(ToDict(Sample(1, "A", Sex.Male, death: true)), Calendar);

        var otherCalendar = new WorldCalendar(HoursPerDay: 12, DaysPerMonth: 20, MonthsPerYear: 6);
        Assert.Throws<InvalidDataException>(() => new ColdTierPersistence(path).Load(otherCalendar));
    }

    private static ColdTierArchive.NpcSummary Sample(
        long id,
        string name,
        Sex sex,
        bool death,
        int cultureId = 1,
        int professionId = 2)
    {
        var birth = WorldDate.Epoch(Calendar).AddYears(-40);
        WorldDate? deathDate = death ? birth.AddYears(35) : null;
        return new ColdTierArchive.NpcSummary(
            new NpcId(id),
            name,
            sex,
            birth,
            deathDate,
            new CultureId(cultureId),
            new ProfessionType(professionId));
    }

    private static Dictionary<long, ColdTierArchive.NpcSummary> ToDict(params ColdTierArchive.NpcSummary[] entries) =>
        entries.ToDictionary(s => s.Id.Value);

    private static string NewTempPath() =>
        Path.Combine(Path.GetTempPath(), $"lw-cold-tier-{Guid.NewGuid():N}.bin");
}

internal static class ColdTierPersistenceTestsHelpers
{
    public static void WriteInt32(Stream stream, int value)
    {
        Span<byte> buffer = stackalloc byte[4];
        System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(buffer, value);
        stream.Write(buffer);
    }
}
