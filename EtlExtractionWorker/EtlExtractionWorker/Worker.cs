using EtlExtractionWorker.Abstractions;
using EtlExtractionWorker.Loaders;
using EtlExtractionWorker.Models;
using EtlExtractionWorker.Staging;
using System.Diagnostics;

namespace EtlExtractionWorker;

public sealed class Worker : BackgroundService
{
    private readonly IEnumerable<IExtractor> _extractors;
    private readonly StagingWriter _stagingWriter;
    private readonly DimensionLoader _dimensionLoader;
    private readonly IConfiguration _configuration;
    private readonly ILogger<Worker> _logger;

    public Worker(
        IEnumerable<IExtractor> extractors,
        StagingWriter stagingWriter,
        DimensionLoader dimensionLoader,
        IConfiguration configuration,
        ILogger<Worker> logger)
    {
        _extractors = extractors;
        _stagingWriter = stagingWriter;
        _dimensionLoader = dimensionLoader;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Iniciando proceso de extracción ETL...");
        var stopwatch = Stopwatch.StartNew();
        var allExtracted = new List<ExtractedRecord>();

        var tasks = _extractors.Select(async extractor =>
        {
            try
            {
                var data = await extractor.ExtractAsync(stoppingToken);
                await _stagingWriter.WriteBatchAsync(extractor.SourceName, data, stoppingToken);

                lock (allExtracted)
                {
                    allExtracted.AddRange(data);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extrayendo desde la fuente {Source}", extractor.SourceName);
            }
        });

        await Task.WhenAll(tasks);

        var shouldLoadDimensions = _configuration.GetValue<bool>("Extraction:LoadDimensions", true);
        if (shouldLoadDimensions)
        {
            _logger.LogInformation("Iniciando carga de dimensiones en DW_AnalisisOpiniones...");
            await _dimensionLoader.LoadDimensionsAsync(allExtracted, stoppingToken);
        }

        stopwatch.Stop();
        _logger.LogInformation("Proceso completado en {ElapsedMs} ms.", stopwatch.ElapsedMilliseconds);
    }
}