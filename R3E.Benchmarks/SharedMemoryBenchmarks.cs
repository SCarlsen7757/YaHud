// SharedMemoryBenchmarks.cs
// Benchmark comparing shared-memory read strategies and checksum implementations.
// Trade-offs: this benchmark shares a MemoryStream across iterations to avoid measuring stream allocation.
// To measure end-to-end including stream allocation, create the MemoryStream inside the benchmark methods.

using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnosers;

namespace R3E.Benchmarks
{
    [MemoryDiagnoser]
    public class SharedMemoryBenchmarks
    {
        private const int ExpectedSize = 43996; // typical shared memory buffer size used in this project

        private byte[] sourceData = null!;
        private MemoryStream ms = null!;
        private byte[] reusableBuffer = null!;

        [GlobalSetup]
        public void GlobalSetup()
        {
            // Initialize deterministic pseudo-random data once per benchmark run
            sourceData = new byte[ExpectedSize];
            var rnd = new Random(12345);
            rnd.NextBytes(sourceData);

            // Create a MemoryStream backed by the source buffer. Sharing the stream across iterations
            // avoids measuring stream allocations. If you want to measure allocation overhead as well,
            // create the MemoryStream inside the individual benchmark method.
            ms = new MemoryStream(sourceData, writable: false);

            // Reusable buffer for the non-allocating read path
            reusableBuffer = new byte[ExpectedSize];
        }

        [IterationSetup]
        public void IterationSetup()
        {
            // Reset stream position so each measured invocation reads from the start
            ms.Position = 0;
        }

        [GlobalCleanup]
        public void GlobalCleanup()
        {
            ms.Dispose();
        }

        [Benchmark(Description = "BinaryReader.ReadBytes (alloc)")]
        public byte[] ReadBytes_Alloc()
        {
            // Constructing BinaryReader per-invocation preserves allocation behavior
            using var br = new BinaryReader(ms, System.Text.Encoding.Default, leaveOpen: true);
            return br.ReadBytes(ExpectedSize);
        }

        [Benchmark(Description = "Stream.Read into reusable buffer (no alloc)")]
        public int ReadInto_ReusedBuffer()
        {
            // Reads into a pre-allocated buffer to avoid per-call allocations
            return ms.Read(reusableBuffer, 0, ExpectedSize);
        }

        [Benchmark(Description = "MD5.HashData")]
        public ulong Md5Hash()
        {
            // MD5 produces a 16-byte digest; we return the first 8 bytes as a 64-bit fingerprint
            var digest = MD5.HashData(sourceData);
            return BitConverter.ToUInt64(digest, 0);
        }

        [Benchmark(Description = "FNV-1a 64 (ReadOnlySpan)")]
        public ulong Fnv1aHash()
        {
            return Fnv1a64(sourceData);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong Fnv1a64(ReadOnlySpan<byte> data)
        {
            const ulong offset = 14695981039346656037UL;
            const ulong prime = 1099511628211UL;
            ulong hash = offset;

            // Simple index-loop to minimize bounds checks
            for (int i = 0; i < data.Length; i++)
            {
                hash ^= data[i];
                hash *= prime;
            }

            return hash;
        }
    }
}
