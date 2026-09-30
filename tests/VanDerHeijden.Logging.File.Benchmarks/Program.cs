using BenchmarkDotNet.Running;

// Run all benchmarks in the assembly
// Usage: dotnet run -c Release
BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).RunAll();
