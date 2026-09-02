using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Population.Archive;

namespace LivingWorld.Infrastructure.EventLog;

public enum ColdTierCompressionCodec : byte
{
    GZip = 1,
    Brotli = 2,
}

/// <summary>Persiste <see cref="ColdTierArchive.NpcSummary"/> comprimido em disco com
/// dicionário de strings (Fase 28, T20, CMP-01/05).</summary>
public sealed class ColdTierPersistence
{
    private static readonly byte[] Magic = "LWCLD1\0"u8.ToArray();
    private const byte FormatVersion = 1;

    private readonly string _filePath;
    private readonly ColdTierCompressionCodec _codec;

    public ColdTierPersistence(string filePath, ColdTierCompressionCodec codec = ColdTierCompressionCodec.Brotli)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);
        _filePath = filePath;
        _codec = codec;
    }

    public string FilePath => _filePath;

    public ColdTierCompressionCodec Codec => _codec;

    public void Save(IReadOnlyDictionary<long, ColdTierArchive.NpcSummary> entries, WorldCalendar calendar)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(calendar);

        byte[] payload = BuildPayload(entries, calendar);
        byte[] compressed = Compress(payload, _codec);

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(_filePath))!);
        using var fs = File.Create(_filePath);
        fs.Write(Magic);
        fs.WriteByte((byte)_codec);
        WriteInt32(fs, payload.Length);
        fs.Write(compressed);
    }

    public IReadOnlyDictionary<long, ColdTierArchive.NpcSummary> Load(WorldCalendar calendar)
    {
        ArgumentNullException.ThrowIfNull(calendar);

        if (!File.Exists(_filePath))
            return new Dictionary<long, ColdTierArchive.NpcSummary>();

        using var fs = File.OpenRead(_filePath);
        ValidateMagic(fs);

        int codecByte = fs.ReadByte();
        if (codecByte < 0)
            throw new EndOfStreamException();

        var codec = (ColdTierCompressionCodec)codecByte;
        if (codec is not (ColdTierCompressionCodec.GZip or ColdTierCompressionCodec.Brotli))
            throw new InvalidDataException($"codec inválido: {codecByte}");

        int expectedPayloadLength = ReadInt32(fs);
        byte[] compressed = ReadRemaining(fs);
        byte[] payload = Decompress(compressed, codec, expectedPayloadLength);

        return ParsePayload(payload, calendar);
    }

    public ColdTierArchive.NpcSummary? TryGet(long npcId, WorldCalendar calendar) =>
        Load(calendar).GetValueOrDefault(npcId);

    public long FileSizeBytes => File.Exists(_filePath) ? new FileInfo(_filePath).Length : 0;

    internal static byte[] BuildPayload(
        IReadOnlyDictionary<long, ColdTierArchive.NpcSummary> entries,
        WorldCalendar calendar)
    {
        var pool = new StringInternPool();
        foreach (var summary in entries.Values.OrderBy(s => s.Id.Value))
            pool.Intern(summary.Name);

        using var ms = new MemoryStream();
        ms.WriteByte(FormatVersion);
        WriteCalendar(ms, calendar);
        WriteStringPool(ms, pool);

        var sorted = entries.Values.OrderBy(s => s.Id.Value).ToArray();
        WriteInt32(ms, sorted.Length);
        foreach (var summary in sorted)
            WriteSummary(ms, summary, pool);

        return ms.ToArray();
    }

    internal static Dictionary<long, ColdTierArchive.NpcSummary> ParsePayload(byte[] payload, WorldCalendar calendar)
    {
        using var ms = new MemoryStream(payload);
        int version = ms.ReadByte();
        if (version != FormatVersion)
            throw new InvalidDataException($"versão inválida: {version}");

        WorldCalendar storedCalendar = ReadCalendar(ms);
        if (!storedCalendar.Equals(calendar))
            throw new InvalidDataException("calendário do arquivo não coincide com o esperado");

        var pool = ReadStringPool(ms);
        int count = ReadInt32(ms);
        var result = new Dictionary<long, ColdTierArchive.NpcSummary>(count);
        for (int i = 0; i < count; i++)
        {
            var summary = ReadSummary(ms, calendar, pool);
            result[summary.Id.Value] = summary;
        }

        return result;
    }

    private static void WriteSummary(Stream ms, ColdTierArchive.NpcSummary summary, StringInternPool pool)
    {
        WriteInt64(ms, summary.Id.Value);
        WriteInt32(ms, pool.Intern(summary.Name));
        ms.WriteByte((byte)summary.Sex);
        WriteInt64(ms, summary.BirthDate.TotalHours);
        if (summary.DeathDate is { } death)
        {
            ms.WriteByte(1);
            WriteInt64(ms, death.TotalHours);
        }
        else
        {
            ms.WriteByte(0);
        }

        WriteInt32(ms, summary.Culture.Id);
        WriteInt32(ms, summary.Profession.Id);
    }

    private static ColdTierArchive.NpcSummary ReadSummary(Stream ms, WorldCalendar calendar, StringInternPool pool)
    {
        long id = ReadInt64(ms);
        int nameId = ReadInt32(ms);
        string name = pool.Resolve(nameId);
        int sexByte = ms.ReadByte();
        if (sexByte < 0)
            throw new EndOfStreamException();

        long birthHours = ReadInt64(ms);
        int hasDeath = ms.ReadByte();
        if (hasDeath < 0)
            throw new EndOfStreamException();

        WorldDate? deathDate = null;
        if (hasDeath != 0)
            deathDate = new WorldDate(calendar, ReadInt64(ms));

        int cultureId = ReadInt32(ms);
        int professionId = ReadInt32(ms);

        return new ColdTierArchive.NpcSummary(
            new NpcId(id),
            name,
            (Sex)sexByte,
            new WorldDate(calendar, birthHours),
            deathDate,
            new CultureId(cultureId),
            new ProfessionType(professionId));
    }

    private static void WriteStringPool(Stream ms, StringInternPool pool)
    {
        WriteInt32(ms, pool.Count);
        for (int i = 0; i < pool.Count; i++)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(pool.Resolve(i));
            WriteInt32(ms, bytes.Length);
            ms.Write(bytes);
        }
    }

    private static StringInternPool ReadStringPool(Stream ms)
    {
        var pool = new StringInternPool();
        int count = ReadInt32(ms);
        for (int i = 0; i < count; i++)
        {
            int length = ReadInt32(ms);
            var bytes = new byte[length];
            ms.ReadExactly(bytes);
            string value = Encoding.UTF8.GetString(bytes);
            if (pool.Intern(value) != i)
                throw new InvalidDataException("ordem do dicionário de strings corrompida");
        }

        return pool;
    }

    private static void WriteCalendar(Stream ms, WorldCalendar calendar)
    {
        WriteInt32(ms, calendar.HoursPerDay);
        WriteInt32(ms, calendar.DaysPerMonth);
        WriteInt32(ms, calendar.MonthsPerYear);
    }

    private static WorldCalendar ReadCalendar(Stream ms) =>
        new(ReadInt32(ms), ReadInt32(ms), ReadInt32(ms));

    private static byte[] Compress(byte[] payload, ColdTierCompressionCodec codec)
    {
        using var output = new MemoryStream();
        using (Stream compressor = codec switch
        {
            ColdTierCompressionCodec.GZip => new GZipStream(output, CompressionLevel.Optimal, leaveOpen: true),
            ColdTierCompressionCodec.Brotli => new BrotliStream(output, CompressionLevel.Optimal, leaveOpen: true),
            _ => throw new InvalidDataException($"codec não suportado: {codec}"),
        })
        {
            compressor.Write(payload);
        }

        return output.ToArray();
    }

    private static byte[] Decompress(byte[] compressed, ColdTierCompressionCodec codec, int expectedLength)
    {
        using var input = new MemoryStream(compressed);
        using var output = new MemoryStream(expectedLength);
        using Stream decompressor = codec switch
        {
            ColdTierCompressionCodec.GZip => new GZipStream(input, CompressionMode.Decompress),
            ColdTierCompressionCodec.Brotli => new BrotliStream(input, CompressionMode.Decompress),
            _ => throw new InvalidDataException($"codec não suportado: {codec}"),
        };
        decompressor.CopyTo(output);

        byte[] payload = output.ToArray();
        if (payload.Length != expectedLength)
            throw new InvalidDataException("tamanho do payload descomprimido não coincide");

        return payload;
    }

    private static void ValidateMagic(Stream fs)
    {
        Span<byte> magic = stackalloc byte[Magic.Length];
        fs.ReadExactly(magic);
        if (!magic.SequenceEqual(Magic))
            throw new InvalidDataException("magic inválido");
    }

    private static byte[] ReadRemaining(Stream fs)
    {
        using var ms = new MemoryStream();
        fs.CopyTo(ms);
        return ms.ToArray();
    }

    private static void WriteInt32(Stream stream, int value)
    {
        Span<byte> buffer = stackalloc byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(buffer, value);
        stream.Write(buffer);
    }

    private static void WriteInt64(Stream stream, long value)
    {
        Span<byte> buffer = stackalloc byte[8];
        BinaryPrimitives.WriteInt64LittleEndian(buffer, value);
        stream.Write(buffer);
    }

    private static int ReadInt32(Stream stream)
    {
        Span<byte> buffer = stackalloc byte[4];
        stream.ReadExactly(buffer);
        return BinaryPrimitives.ReadInt32LittleEndian(buffer);
    }

    private static long ReadInt64(Stream stream)
    {
        Span<byte> buffer = stackalloc byte[8];
        stream.ReadExactly(buffer);
        return BinaryPrimitives.ReadInt64LittleEndian(buffer);
    }
}
