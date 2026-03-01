using BenchmarkDotNet.Running;
using ExpressRecipe.Messaging.Benchmarks.Benchmarks;

// Run all benchmarks. To run in Release mode (required for accurate results):
//   dotnet run -c Release -- --filter *
BenchmarkSwitcher.FromAssembly(typeof(SerializationBenchmarks).Assembly).Run(args);
