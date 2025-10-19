using System;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnosers;
using R3E;
using R3E.Data;

namespace R3E.Benchmarks
{
    [MemoryDiagnoser]
    public class PollLoopFullBench
    {
        private int expectedSize;
        private byte[] buffer = null!;
        private MemoryMappedFile? mmf;

        [GlobalSetup]
        public void Setup()
        {
            expectedSize = Marshal.SizeOf<Shared>();
            buffer = new byte[expectedSize];

            // Open existing MMF from the running game. If not present, this will throw.
            mmf = MemoryMappedFile.OpenExisting(Constant.SharedMemoryName);
        }

        [GlobalCleanup]
        public void Cleanup()
        {
            mmf?.Dispose();
        }

        [Benchmark]
        public void ReadAndSample()
        {
            using var view = mmf!.CreateViewStream();
            var read = 0;
            while (read < expectedSize)
            {
                var r = view.Read(buffer, read, expectedSize - read);
                if (r <= 0) break;
                read += r;
            }

            // compute sample checksum (same as service)
            var hash = ComputeSampleChecksum(buffer);

            // marshall to struct (fall back via GCHandle)
            var handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
            try
            {
                var ptr = handle.AddrOfPinnedObject();
                var s = Marshal.PtrToStructure<Shared>(ptr);
            }
            finally
            {
                if (handle.IsAllocated) handle.Free();
            }
        }

        private static ulong ComputeSampleChecksum(ReadOnlySpan<byte> data, int sampleSize = 256)
        {
            if (data.Length <= sampleSize) return ComputeChecksum(data);

            var hash = 14695981039346656037UL;
            int region = sampleSize / 3;
            if (region <= 0) region = Math.Min(sampleSize, 64);

            hash = CombineFnv(hash, data.Slice(0, region));
            int midStart = Math.Max(0, (data.Length / 2) - region / 2);
            hash = CombineFnv(hash, data.Slice(midStart, region));
            hash = CombineFnv(hash, data.Slice(data.Length - region, region));

            return hash;
        }

        private static ulong CombineFnv(ulong seed, ReadOnlySpan<byte> span)
        {
            const ulong prime = 1099511628211UL;
            ulong h = seed;
            for (int i = 0; i < span.Length; i++)
            {
                h ^= span[i];
                h *= prime;
            }
            return h;
        }

        private static ulong ComputeChecksum(ReadOnlySpan<byte> data)
        {
            const ulong offset = 14695981039346656037UL;
            const ulong prime = 1099511628211UL;
            ulong hash = offset;
            for (int i = 0; i < data.Length; i++)
            {
                hash ^= data[i];
                hash *= prime;
            }
            return hash;
        }
    }
}
