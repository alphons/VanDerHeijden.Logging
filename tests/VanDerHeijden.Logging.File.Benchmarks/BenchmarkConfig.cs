using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Reports;

/// <summary>
/// Shared BenchmarkDotNet configuration:
/// - 1 warmup iteration so the JIT and OS file caches are stable
/// - 3 measured iterations for a reliable mean without waiting too long
/// - Throughput as the primary statistic
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
