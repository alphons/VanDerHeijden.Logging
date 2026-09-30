using BenchmarkDotNet.Running;

// Run all benchmarks in the assembly
// Usage: dotnet run -c Release
// Run a selection: dotnet run -c Release -- --filter *StructuredLogging*
var switcher = BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly);
if (args.Length > 0)
	switcher.Run(args);
else
	switcher.RunAll();
