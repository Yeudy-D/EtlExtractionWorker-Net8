using EtlExtractionWorker.Abstractions;
using EtlExtractionWorker.Staging;
using System.Diagnostics;

namespace EtlExtractionWorker;

public sealed class Worker : BackgroundService
{
    private readonly IEnumerable<IExtractor> _extractors;
    private readonly StagingWriter _stagingWriter;
    private readonly ILogger<Worker> _logger;

    public Worker(IEnumerable<IExtractor> extractors, StagingWriter stagingWriter, ILogger<Worker> logger)
    {
        _extractors = extractors;
        _stagingWriter = stagingWriter;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Iniciando proceso de extracción ETL...");
        var stopwatch = Stopwatch.StartNew();

        var tasks = _extractors.Select(async extractor =>
        {
            try
            {
                var data = await extractor.ExtractAsync(stoppingToken);
                await _stagingWriter.WriteBatchAsync(extractor.SourceName, data, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extrayendo desde la fuente {Source}", extractor.SourceName);
            }
        });

        await Task.WhenAll(tasks);

        stopwatch.Stop();
        _logger.LogInformation("Extracción completada en {ElapsedMs} ms.", stopwatch.ElapsedMilliseconds);
    }
}