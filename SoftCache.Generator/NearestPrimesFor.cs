using System;
using System.Collections.Generic;
using System.Text;

namespace SoftCache.Generator;

/// <summary>
/// Get nearest primes for 2^N such that the prime is strictly greater than 2^N.
/// </summary>
public static class NearestPrimes
{
    /// <summary>Nearest prime for 2^1 = 2</summary>
    public const int Prime1 = 3;

    /// <summary>Nearest prime for 2^2 = 4</summary>
    public const int Prime2 = 5;

    /// <summary>Nearest prime for 2^3 = 8</summary>
    public const int Prime3 = 11;

    /// <summary>Nearest prime for 2^4 = 16</summary>
    public const int Prime4 = 17;

    /// <summary>Nearest prime for 2^5 = 32</summary>
    public const int Prime5 = 37;

    /// <summary>Nearest prime for 2^6 = 64</summary>
    public const int Prime6 = 67;

    /// <summary>Nearest prime for 2^7 = 128</summary>
    public const int Prime7 = 131;

    /// <summary>Nearest prime for 2^8 = 256</summary>
    public const int Prime8 = 257;

    /// <summary>Nearest prime for 2^9 = 512</summary>
    public const int Prime9 = 521;

    /// <summary>Nearest prime for 2^10 = 1024</summary>
    public const int Prime10 = 1031;

    /// <summary>Nearest prime for 2^11 = 2048</summary>
    public const int Prime11 = 2053;

    /// <summary>Nearest prime for 2^12 = 4096</summary>
    public const int Prime12 = 4099;

    /// <summary>Nearest prime for 2^13 = 8192</summary>
    public const int Prime13 = 8209;

    /// <summary>Nearest prime for 2^14 = 16384</summary>
    public const int Prime14 = 16411;

    /// <summary>Nearest prime for 2^15 = 32768</summary>
    public const int Prime15 = 32771;

    /// <summary>
    /// Get nearest prime greater than 2^cacheBits.
    /// </summary>
    public static int For(int cacheBits)
    {
        return cacheBits switch
        {
            1 => Prime1,
            2 => Prime2,
            3 => Prime3,
            4 => Prime4,
            5 => Prime5,
            6 => Prime6,
            7 => Prime7,
            8 => Prime8,
            9 => Prime9,
            10 => Prime10,
            11 => Prime11,
            12 => Prime12,
            13 => Prime13,
            14 => Prime14,
            15 => Prime15,
            _ => throw new NotSupportedException($"CacheBits {cacheBits} not supported")
        };
    }
}