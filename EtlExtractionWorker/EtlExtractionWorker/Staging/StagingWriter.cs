using System.IO;
using EtlExtractionWorker.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace EtlExtractionWorker.Staging;

public sealed class StagingWriter
{
    private readonly string _stagingFolder;
    private readonly ILogger<StagingWriter> _logger;

    public StagingWriter(IConfiguration configuration, ILogger<StagingWriter> logger)
    {
        _stagingFolder = configuration["Extraction:StagingFolder"] ?? "StagingOutput";
        _logger = logger;
    }

    public async Task<string> WriteBatchAsync(string sourceName, IReadOnlyList<ExtractedRecord> records, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_stagingFolder);
        var fileName = $"{sourceName.ToLowerInvariant()}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.json";
        var fullPath = Path.Combine(_stagingFolder, fileName);

        var json = JsonSerializer.Serialize(records, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(fullPath, json, cancellationToken);

        _logger.LogInformation("Guardados {Count} registros de {Source} en staging: {Path}", records.Count, sourceName, fullPath);
        return fullPath;
    }
}

