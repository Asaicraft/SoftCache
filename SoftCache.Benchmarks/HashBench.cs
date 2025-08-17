using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;

public sealed partial class TodoQueryKey
{
    public string? Query { get; }
    public int FromDayNumber { get; }
    public int ToDayNumber { get; }
    public bool IsComplete { get; }
    public int Page { get; }
    public int PageSize { get; }

    public TodoQueryKey(string? query, int fromDayNumber, int toDayNumber, bool isComplete, int page, int pageSize)
    {
        Query = query;
        FromDayNumber = fromDayNumber;
        ToDayNumber = toDayNumber;
        IsComplete = isComplete;
        Page = page;
        PageSize = pageSize;
    }

    // Minimal Parameters for the benchmark (mirrors the generator output)
    public readonly struct Parameters
    {
        public readonly string? Query { get; }
        public readonly int FromDayNumber { get; }
        public readonly int ToDayNumber { get; }
        public readonly bool IsComplete { get; }
        public readonly int Page { get; }
        public readonly int PageSize { get; }

        public Parameters(string? query, int fromDayNumber, int toDayNumber, bool isComplete, int page, int pageSize)
        {
            Query = query;
            FromDayNumber = fromDayNumber;
            ToDayNumber = toDayNumber;
            IsComplete = isComplete;
            Page = page;
            PageSize = pageSize;
        }
    }

    // Your MakeSoftHash implementation — inlined here for a self-contained benchmark
    public static unsafe ushort MakeSoftHash(scoped in Parameters parameters)
    {
        uint h = 0u;
        unchecked
        {
            object? object0 = (object?)parameters.Query;
            uint x0 = object0 is null ? 0u : (uint)RuntimeHelpers.GetHashCode(object0);
            x0 ^= x0 >> 16;
            h ^= x0;

            uint x1 = (uint)parameters.FromDayNumber;
            x1 ^= x1 >> 16;
            h ^= x1;

            uint x2 = (uint)parameters.ToDayNumber;
            x2 ^= x2 >> 16;
            h ^= x2;

            uint x3 = parameters.IsComplete ? 1u : 0u;
            h ^= x3;

            uint x4 = (uint)parameters.Page;
            x4 ^= x4 >> 16;
            h ^= x4;

            uint x5 = (uint)parameters.PageSize;
            x5 ^= x5 >> 16;
            h ^= x5;
        }

        return (ushort)h;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ushort Murmur3_16(scoped in Parameters parameters)
    {
        const uint Constant1 = 0xcc9e2d51u;
        const uint Constant2 = 0x1b873593u;

        var hash32 = 0u;

        // String is hashed as a separate Murmur3_32 over UTF-16 (char) bytes,
        // then mixed as a single 4-byte block.
        var stringHash32 = parameters.Query is null ? 0u : ComputeMurmur3_32ForString(parameters.Query.AsSpan());
        MixMurmur32(ref hash32, stringHash32, Constant1, Constant2);

        MixMurmur32(ref hash32, (uint)parameters.FromDayNumber, Constant1, Constant2);
        MixMurmur32(ref hash32, (uint)parameters.ToDayNumber, Constant1, Constant2);
        MixMurmur32(ref hash32, parameters.IsComplete ? 1u : 0u, Constant1, Constant2);
        MixMurmur32(ref hash32, (uint)parameters.Page, Constant1, Constant2);
        MixMurmur32(ref hash32, (uint)parameters.PageSize, Constant1, Constant2);

        // Six 4-byte blocks.
        hash32 ^= 24u;
        hash32 = FinalizeMix32(hash32);

        // Fold to 16 bits.
        return (ushort)(hash32 ^ (hash32 >> 16));
    }

    // --- FNV-1a 32 → 16-bit ---

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ushort Fnv1a_16(scoped in Parameters parameters)
    {
        const uint FnvOffset = 2166136261u;
        const uint FnvPrime = 16777619u;

        var hash32 = FnvOffset;

        // String is processed byte-by-byte (UTF-16 LE). Add a null marker.
        if (parameters.Query is null)
        {
            hash32 ^= 0xFFu;
            hash32 *= FnvPrime;
        }
        else
        {
            foreach (var ch in parameters.Query)
            {
                var lowByte = (byte)ch;
                var highByte = (byte)(ch >> 8);

                hash32 ^= lowByte;
                hash32 *= FnvPrime;

                hash32 ^= highByte;
                hash32 *= FnvPrime;
            }
        }

        // Other fields are mixed as 4 bytes each (little-endian).
        FnvFeedUInt32(ref hash32, (uint)parameters.FromDayNumber);
        FnvFeedUInt32(ref hash32, (uint)parameters.ToDayNumber);
        FnvFeedUInt32(ref hash32, parameters.IsComplete ? 1u : 0u);
        FnvFeedUInt32(ref hash32, (uint)parameters.Page);
        FnvFeedUInt32(ref hash32, (uint)parameters.PageSize);

        var folded = hash32 ^ (hash32 >> 16);
        return (ushort)folded;
    }

    // ---------- Helpers ----------

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void MixMurmur32(ref uint hash32, uint key, uint constant1, uint constant2)
    {
        key *= constant1;
        key = (key << 15) | (key >> 17);
        key *= constant2;

        hash32 ^= key;
        hash32 = (hash32 << 13) | (hash32 >> 19);
        hash32 = hash32 * 5u + 0xe6546b64u;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint FinalizeMix32(uint hash32)
    {
        hash32 ^= hash32 >> 16;
        hash32 *= 0x85ebca6bu;
        hash32 ^= hash32 >> 13;
        hash32 *= 0xc2b2ae35u;
        hash32 ^= hash32 >> 16;
        return hash32;
    }

    // Murmur3 x86_32 over the string's UTF-16 (char) data (seed = 0).
    private static uint ComputeMurmur3_32ForString(ReadOnlySpan<char> text)
    {
        const uint Constant1 = 0xcc9e2d51u;
        const uint Constant2 = 0x1b873593u;

        var hash32 = 0u;

        var index = 0;

        // Process two chars (4 bytes) at a time.
        for (; index + 1 < text.Length; index += 2)
        {
            var key = (uint)text[index] | ((uint)text[index + 1] << 16);

            key *= Constant1;
            key = (key << 15) | (key >> 17);
            key *= Constant2;

            hash32 ^= key;
            hash32 = (hash32 << 13) | (hash32 >> 19);
            hash32 = hash32 * 5u + 0xe6546b64u;
        }

        // Tail (odd char -> 2 bytes).
        if (index < text.Length)
        {
            var tailKey = (uint)text[index];
            tailKey *= Constant1;
            tailKey = (tailKey << 15) | (tailKey >> 17);
            tailKey *= Constant2;
            hash32 ^= tailKey;
        }

        // Length in bytes.
        hash32 ^= (uint)(text.Length * 2);
        return FinalizeMix32(hash32);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void FnvFeedUInt32(ref uint hash32, uint value)
    {
        const uint FnvPrime = 16777619u;

        hash32 ^= (byte)value;
        hash32 *= FnvPrime;

        hash32 ^= (byte)(value >> 8);
        hash32 *= FnvPrime;

        hash32 ^= (byte)(value >> 16);
        hash32 *= FnvPrime;

        hash32 ^= (byte)(value >> 24);
        hash32 *= FnvPrime;
    }

}

[MemoryDiagnoser]
[SimpleJob]
public class HashBench
{
    // Sample size used for timing and collision measurements
    [Params(10_000, 100_000, 500_000)]
    public int SampleSize;

    private TodoQueryKey.Parameters[] _data = default!;

    [GlobalSetup]
    public void Setup()
    {
        // Deterministic dataset
        var random = new Random(12345);
        var vocabulary = CreateVocabulary(random, 2048);

        _data = new TodoQueryKey.Parameters[SampleSize];
        for (var i = 0; i < SampleSize; i++)
        {
            // Add some variability to produce realistic collisions
            var queryText = (i % 7 == 0) ? null : vocabulary[random.Next(vocabulary.Length)];
            var fromDayNumber = random.Next(73_000, 85_000); // DateOnly.DayNumber ~ 2000..2030
            var toDayNumber = fromDayNumber + random.Next(0, 30);
            var isComplete = (i & 1) == 0;
            var page = 1 + random.Next(0, 128);
            var pageSize = 1 + random.Next(1, 128);

            _data[i] = new TodoQueryKey.Parameters(queryText, fromDayNumber, toDayNumber, isComplete, page, pageSize);
        }
    }

    // ---------- TIME ----------

    [Benchmark(Baseline = true)]
    public ulong HashCodeCombine_Time()
    {
        // Accumulate hashes so JIT cannot eliminate the computation
        ulong sum = 0;
        var data = _data;

        for (var i = 0; i < data.Length; i++)
        {
            ref readonly var parameters = ref data[i];
            // Fair baseline: use standard HashCode.Combine and truncate to 16-bit
            var h32 = HashCode.Combine(parameters.Query, parameters.FromDayNumber, parameters.ToDayNumber, parameters.IsComplete, parameters.Page, parameters.PageSize);
            sum += (ushort)h32;
        }

        return sum;
    }

    [Benchmark]
    public ulong SoftHash_Time()
    {
        ulong sum = 0;
        var data = _data;

        for (var i = 0; i < data.Length; i++)
        {
            ref readonly var parameters = ref data[i];
            sum += TodoQueryKey.MakeSoftHash(in parameters);
        }

        return sum;
    }

    [Benchmark]
    public ulong Murmur3_16_Time()
    {
        ulong sum = 0;
        var data = _data;

        for (var i = 0; i < data.Length; i++)
        {
            ref readonly var parameters = ref data[i];
            sum += TodoQueryKey.Murmur3_16(in parameters);
        }

        return sum;
    }

    [Benchmark]
    public ulong Fnv1a_16_Time()
    {
        ulong sum = 0;
        var data = _data;

        for (var i = 0; i < data.Length; i++)
        {
            ref readonly var parameters = ref data[i];
            sum += TodoQueryKey.Fnv1a_16(in parameters);
        }

        return sum;
    }

    private static string[] CreateVocabulary(Random random, int size)
    {
        var array = new string[size];
        for (var i = 0; i < size; i++)
        {
            // Short keywords of varying length — emulate search queries
            var length = 3 + random.Next(0, 10);
            var characters = new char[length];
            for (var j = 0; j < length; j++)
            {
                // a-z letters
                characters[j] = (char)('a' + random.Next(0, 26));
            }
            array[i] = new string(characters);
        }
        return array;
    }
}
