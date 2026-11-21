using EtlExtractionWorker.Abstractions;
using EtlExtractionWorker.Models;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EtlExtractionWorker.Extractors;

public sealed class DatabaseExtractor : IExtractor
{
    private readonly string _connectionString;
    private readonly string _query;
    private readonly ILogger<DatabaseExtractor> _logger;

    public DatabaseExtractor(IConfiguration configuration, ILogger<DatabaseExtractor> logger)
    {
        _connectionString = configuration["Extraction:SqlConnectionString"]
            ?? throw new InvalidOperationException("Falta SqlConnectionString");
        _query = configuration["Extraction:ReviewsQuery"]
            ?? "SELECT TOP 100 Id, Comment FROM Reviews";
        _logger = logger;
    }

    public string SourceName => "Database";

    public async Task<IReadOnlyList<ExtractedRecord>> ExtractAsync(CancellationToken cancellationToken = default)
    {
        var results = new List<ExtractedRecord>();

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(_query, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            var id = reader["Id"]?.ToString() ?? string.Empty;
            var comment = reader["Comment"]?.ToString() ?? string.Empty;
            results.Add(new ExtractedRecord(SourceName, id, comment, DateTime.UtcNow));
        }

        _logger.LogInformation("SQL Server extrajo {Count} reseñas", results.Count);
        return results;
    }
}