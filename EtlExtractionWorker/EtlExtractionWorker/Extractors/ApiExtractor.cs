using EtlExtractionWorker.Abstractions;
using EtlExtractionWorker.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Net.Http;

namespace EtlExtractionWorker.Extractors;

public sealed class ApiExtractor : IExtractor
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string _endpoint;
    private readonly ILogger<ApiExtractor> _logger;

    public ApiExtractor(IHttpClientFactory httpClientFactory, IConfiguration configuration, ILogger<ApiExtractor> logger)
    {
        _httpClientFactory = httpClientFactory;
        _endpoint = configuration["Extraction:CommentsEndpoint"] ?? "comments";
        _logger = logger;
    }

    public string SourceName => "ApiRest";

    public async Task<IReadOnlyList<ExtractedRecord>> ExtractAsync(CancellationToken cancellationToken = default)
    {
        var results = new List<ExtractedRecord>();
        var client = _httpClientFactory.CreateClient("CommentsApi");

        using var response = await client.GetAsync(_endpoint, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        foreach (var item in doc.RootElement.EnumerateArray())
        {
            var id = item.TryGetProperty("id", out var idProp) ? idProp.ToString() : Guid.NewGuid().ToString();
            var body = item.TryGetProperty("body", out var bodyProp) ? bodyProp.GetString() ?? string.Empty : string.Empty;
            results.Add(new ExtractedRecord(SourceName, id, body, DateTime.UtcNow));
        }

        _logger.LogInformation("API REST extrajo {Count} comentarios", results.Count);
        return results;
    }
}