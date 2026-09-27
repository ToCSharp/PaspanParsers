using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;

namespace PaspanParsers.Benchmarks;

/// <summary>
/// A run over a large corpus takes seconds, so a few iterations are enough. Adds the columns
/// that do not depend on the corpus size: throughput and bytes allocated per source byte.
/// </summary>
internal sealed class CorpusConfig : ManualConfig
{
    public CorpusConfig()
    {
        AddJob(Job.Default.WithWarmupCount(2).WithIterationCount(7).WithId("Corpus"));
        AddDiagnoser(MemoryDiagnoser.Default);
        AddColumn(new CorpusColumn("MB/s", "Megabytes of UTF-8 source parsed per second", higherIsBetter: true));
        AddColumn(new CorpusColumn("Alloc/byte", "Bytes allocated per byte of UTF-8 source", higherIsBetter: false));
        AddLogicalGroupRules(BenchmarkLogicalGroupRule.ByParams);
    }
}

internal sealed class CorpusColumn(string name, string legend, bool higherIsBetter) : IColumn
{
    public string Id => nameof(CorpusColumn) + name;

    public string ColumnName => name;

    public bool AlwaysShow => true;

    public ColumnCategory Category => ColumnCategory.Custom;

    public int PriorityInCategory => higherIsBetter ? 0 : 1;

    public bool IsNumeric => true;

    public UnitType UnitType => UnitType.Dimensionless;

    public string Legend => legend;

    public bool IsAvailable(Summary summary) => true;

    public bool IsDefault(Summary summary, BenchmarkCase benchmarkCase) => false;

    public string GetValue(Summary summary, BenchmarkCase benchmarkCase) => GetValue(summary, benchmarkCase, summary.Style);

    public string GetValue(Summary summary, BenchmarkCase benchmarkCase, SummaryStyle style)
    {
        var report = summary[benchmarkCase];
        var corpus = benchmarkCase.Parameters[nameof(CSharpParserBenchmarks.Corpus)] as string;
        if (report?.ResultStatistics == null || corpus == null)
        {
            return "-";
        }

        var bytes = CSharpCorpus.Load(corpus).Utf8Bytes;
        if (higherIsBetter)
        {
            var seconds = report.ResultStatistics.Mean / 1e9;
            return (bytes / 1024.0 / 1024.0 / seconds).ToString("F1");
        }

        var allocated = report.GcStats.GetBytesAllocatedPerOperation(benchmarkCase);
        return allocated == null ? "-" : ((double)allocated.Value / bytes).ToString("F1");
    }
}
