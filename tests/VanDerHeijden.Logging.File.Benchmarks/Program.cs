using BenchmarkDotNet.Running;

// Draai alle benchmarks in de assembly
// Gebruik: dotnet run -c Release
BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).RunAll();
