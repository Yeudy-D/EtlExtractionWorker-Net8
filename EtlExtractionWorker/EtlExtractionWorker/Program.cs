using EtlExtractionWorker;
using EtlExtractionWorker.Abstractions;
using EtlExtractionWorker.Extractors;
using EtlExtractionWorker.Staging;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddHttpClient("CommentsApi", client =>
{
    var baseUrl = builder.Configuration["Extraction:ApiBaseUrl"] ?? "https://jsonplaceholder.typicode.com/";
    client.BaseAddress = new Uri(baseUrl);
});

builder.Services.AddSingleton<IExtractor, CsvExtractor>();
builder.Services.AddSingleton<IExtractor, DatabaseExtractor>();
builder.Services.AddSingleton<IExtractor, ApiExtractor>();
builder.Services.AddSingleton<StagingWriter>();
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();