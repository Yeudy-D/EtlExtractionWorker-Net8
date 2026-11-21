using CsvHelper;
using EtlExtractionWorker.Abstractions;
using EtlExtractionWorker.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;



namespace EtlExtractionWorker.Extractors;

public sealed class CsvExtractor : IExtractor
{
    private readonly string _csvPath;
    private readonly ILogger<CsvExtractor> _logger;

    public CsvExtractor(IConfiguration configuration, ILogger<CsvExtractor> logger)
    {
        _csvPath = configuration["Extraction:CsvPath"] ?? "Data/encuestas.csv";
        _logger = logger;
    }

    public string SourceName => "CSV";

    public async Task<IReadOnlyList<ExtractedRecord>> ExtractAsync(CancellationToken cancellationToken = default)
    {
        var results = new List<ExtractedRecord>();

        if (!File.Exists(_csvPath))
        {
            _logger.LogWarning("Archivo CSV no encontrado en {Path}", _csvPath);
            return results;
        }
        
        using var reader = new StreamReader(_csvPath);
        using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);

        await foreach (var row in csv.GetRecordsAsync<dynamic>(cancellationToken))
        {
            var dict = (IDictionary<string, object>)row;
            var id = dict.TryGetValue("Id", out var idVal) ? idVal?.ToString() ?? Guid.NewGuid().ToString() : Guid.NewGuid().ToString();
            var comment = dict.TryGetValue("Respuesta", out var respVal) ? respVal?.ToString() ?? string.Empty : string.Empty;

            results.Add(new ExtractedRecord(SourceName, id, comment, DateTime.UtcNow));
        }

        _logger.LogInformation("CSV extrajo {Count} registros desde {Path}", results.Count, _csvPath);
        return results;
    }
}