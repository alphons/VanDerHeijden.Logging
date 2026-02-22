using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Reports;

/// <summary>
/// Gedeelde BenchmarkDotNet configuratie:
/// - 1 warmup iteratie zodat JIT en OS file caches stabiel zijn
/// - 3 meetiteraties voor een betrouwbaar gemiddelde zonder te lang te wachten
/// - Throughput als primaire statistiek
/// </summary>
public class BenchmarkConfig : ManualConfig
{
	public BenchmarkConfig()
	{
		AddJob(Job.Default
			.WithWarmupCount(1)
			.WithIterationCount(3)
			.WithId("FileLogging"));

		AddColumn(StatisticColumn.P95);
		AddColumn(StatisticColumn.Max);
		WithSummaryStyle(SummaryStyle.Default.WithRatioStyle(RatioStyle.Percentage));
	}
}
