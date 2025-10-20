using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnosers;
using R3E.Data;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;

namespace R3E.Benchmarks
{
    [MemoryDiagnoser]
    public class PollLoopHeaderBench
    {
        private int expectedSize;
        private byte[] buffer = null!;
        private MemoryMappedFile? mmf;

        private int prevTicks;
        private int prevPaused;

        [GlobalSetup]
        public void Setup()
        {
            expectedSize = Marshal.SizeOf<Shared>();
            buffer = new byte[expectedSize];

            // Open existing MMF from the running game. If not present, this will throw.
            mmf = MemoryMappedFile.OpenExisting(Constant.SharedMemoryName);
            prevTicks = -1;
            prevPaused = -1;
        }

        [GlobalCleanup]
        public void Cleanup()
        {
            mmf?.Dispose();
        }

        [Benchmark]
        public int ReadHeaderOnly()
        {
            using var view = mmf!.CreateViewStream();
            var read = 0;
            while (read < expectedSize)
            {
                var r = view.Read(buffer, read, expectedSize - read);
                if (r <= 0) break;
                read += r;
            }

            // Offsets based on Shared layout
            // GamePaused at offset 20 (Int32)
            // Player starts at offset 40, GameSimulationTicks at offset 44
            int gamePaused = BitConverter.ToInt32(buffer, 20);
            int simTicks = BitConverter.ToInt32(buffer, 44);

            int changed = 0;
            if (gamePaused != prevPaused || simTicks != prevTicks)
            {
                changed = 1;
                prevPaused = gamePaused;
                prevTicks = simTicks;
            }

            return changed; // prevent optimization
        }
    }
}
