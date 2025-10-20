using BenchmarkDotNet.Running;

namespace R3E.Benchmarks
{
    public class Program
    {
        public static void Main(string[] args)
        {
            BenchmarkRunner.Run<SharedMemoryBenchmarks>();
        }
    }
}
