using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Exporters.Csv;
using BenchmarkDotNet.Reports;
using Perfolizer.Metrology;

namespace BenchmoniK.Utils;

public class BenchmarkDotNetConfig: ManualConfig
{
        public BenchmarkDotNetConfig()
        {
            var csvExporter = new CsvExporter(
                CsvSeparator.CurrentCulture,
                new SummaryStyle(
                    cultureInfo:System.Globalization.CultureInfo.CurrentCulture,
                    printUnitsInContent: false,
                    printUnitsInHeader: true,
                    timeUnit: Perfolizer.Horology.TimeUnit.Nanosecond,
                    sizeUnit: SizeUnit.KB
                )
            );
            AddLogger(BenchmarkDotNet.Loggers.ConsoleLogger.Default);
            AddExporter(BenchmarkDotNet.Exporters.HtmlExporter.Default);
            AddExporter(csvExporter);
            AddExporter(BenchmarkDotNet.Exporters.MarkdownExporter.GitHub);
        }
}