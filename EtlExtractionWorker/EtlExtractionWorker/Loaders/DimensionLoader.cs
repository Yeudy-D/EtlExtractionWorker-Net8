using EtlExtractionWorker.Models;
using Microsoft.Data.SqlClient;
using System.Data;

namespace EtlExtractionWorker.Loaders;

public sealed class DimensionLoader
{
    private readonly string _warehouseConnectionString;
    private readonly ILogger<DimensionLoader> _logger;

    public DimensionLoader(IConfiguration configuration, ILogger<DimensionLoader> logger)
    {
        _warehouseConnectionString = configuration["Extraction:WarehouseConnectionString"]
            ?? throw new InvalidOperationException("Falta Extraction:WarehouseConnectionString");
        _logger = logger;
    }

    public async Task LoadDimensionsAsync(
        IReadOnlyList<ExtractedRecord> extractedRecords,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(_warehouseConnectionString);
        await connection.OpenAsync(cancellationToken);

        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            await LoadDimFuenteAsync(connection, transaction, extractedRecords, cancellationToken);
            await LoadDimSentimientoAsync(connection, transaction, cancellationToken);
            await LoadDimClienteDefaultAsync(connection, transaction, cancellationToken);
            await LoadDimProductoDefaultAsync(connection, transaction, cancellationToken);
            await LoadDimTiempoAsync(connection, transaction, extractedRecords, cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            _logger.LogInformation("Carga de dimensiones en DW_AnalisisOpiniones completada con éxito.");
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Error durante la carga de dimensiones. Se revirtió la transacción.");
            throw;
        }
    }

    private static async Task LoadDimFuenteAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        IReadOnlyList<ExtractedRecord> records,
        CancellationToken cancellationToken)
    {
        var fuentesBase = new List<(int Id, string TipoCanal, string Plataforma)>
        {
            (1, "Archivo", "CSV"),
            (2, "Base de Datos", "SQL Server"),
            (3, "API REST", "JSONPlaceholder")
        };

        // Si aparece alguna fuente distinta en los registros extraídos, le asigna un ID nuevo
        var nextId = fuentesBase.Max(x => x.Id) + 1;
        foreach (var sourceName in records.Select(r => r.Source).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct())
        {
            if (!fuentesBase.Any(f => string.Equals(f.Plataforma, sourceName, StringComparison.OrdinalIgnoreCase)
                                  || string.Equals(f.TipoCanal, sourceName, StringComparison.OrdinalIgnoreCase)))
            {
                fuentesBase.Add((nextId++, "Otra Fuente", sourceName));
            }
        }

        const string sql = """
            IF NOT EXISTS (SELECT 1 FROM dbo.Dim_Fuente WHERE IdFuente = @IdFuente)
            BEGIN
                INSERT INTO dbo.Dim_Fuente (IdFuente, TipoCanal, Plataforma)
                VALUES (@IdFuente, @TipoCanal, @Plataforma);
            END
            """;

        foreach (var f in fuentesBase)
        {
            await using var cmd = new SqlCommand(sql, connection, transaction);
            cmd.Parameters.Add("@IdFuente", SqlDbType.Int).Value = f.Id;
            cmd.Parameters.Add("@TipoCanal", SqlDbType.NVarChar, 100).Value = f.TipoCanal;
            cmd.Parameters.Add("@Plataforma", SqlDbType.NVarChar, 100).Value = f.Plataforma;
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static async Task LoadDimSentimientoAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        var sentimientos = new[]
        {
            (Id: 1, Clasificacion: "Positivo"),
            (Id: 2, Clasificacion: "Neutral"),
            (Id: 3, Clasificacion: "Negativo"),
            (Id: 4, Clasificacion: "Sin Clasificar")
        };

        const string sql = """
            IF NOT EXISTS (SELECT 1 FROM dbo.Dim_Sentimiento WHERE IdSentimiento = @IdSentimiento)
            BEGIN
                INSERT INTO dbo.Dim_Sentimiento (IdSentimiento, Clasificacion)
                VALUES (@IdSentimiento, @Clasificacion);
            END
            """;

        foreach (var s in sentimientos)
        {
            await using var cmd = new SqlCommand(sql, connection, transaction);
            cmd.Parameters.Add("@IdSentimiento", SqlDbType.Int).Value = s.Id;
            cmd.Parameters.Add("@Clasificacion", SqlDbType.NVarChar, 40).Value = s.Clasificacion;
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static async Task LoadDimClienteDefaultAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        const string sql = """
            IF NOT EXISTS (SELECT 1 FROM dbo.Dim_Cliente WHERE IdCliente = @IdCliente)
            BEGIN
                INSERT INTO dbo.Dim_Cliente (IdCliente, Nombre, Pais, Edad, TipoCliente)
                VALUES (@IdCliente, @Nombre, @Pais, @Edad, @TipoCliente);
            END
            """;

        await using var cmd = new SqlCommand(sql, connection, transaction);
        cmd.Parameters.Add("@IdCliente", SqlDbType.Int).Value = 1;
        cmd.Parameters.Add("@Nombre", SqlDbType.NVarChar, 200).Value = "Cliente No Especificado";
        cmd.Parameters.Add("@Pais", SqlDbType.NVarChar, 100).Value = "No Especificado";
        cmd.Parameters.Add("@Edad", SqlDbType.Int).Value = DBNull.Value;
        cmd.Parameters.Add("@TipoCliente", SqlDbType.NVarChar, 100).Value = "General";
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task LoadDimProductoDefaultAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        const string sql = """
            IF NOT EXISTS (SELECT 1 FROM dbo.Dim_Producto WHERE IdProducto = @IdProducto)
            BEGIN
                INSERT INTO dbo.Dim_Producto (IdProducto, NombreProducto, Categoria)
                VALUES (@IdProducto, @NombreProducto, @Categoria);
            END
            """;

        await using var cmd = new SqlCommand(sql, connection, transaction);
        cmd.Parameters.Add("@IdProducto", SqlDbType.Int).Value = 1;
        cmd.Parameters.Add("@NombreProducto", SqlDbType.NVarChar, 200).Value = "Producto General";
        cmd.Parameters.Add("@Categoria", SqlDbType.NVarChar, 200).Value = "General";
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task LoadDimTiempoAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        IReadOnlyList<ExtractedRecord> records,
        CancellationToken cancellationToken)
    {
        var fechas = records
            .Select(r => r.ExtractedAtUtc.Date)
            .Append(DateTime.UtcNow.Date)
            .Distinct()
            .ToList();

        const string sql = """
            IF NOT EXISTS (SELECT 1 FROM dbo.Dim_Tiempo WHERE IdTiempo = @IdTiempo)
            BEGIN
                INSERT INTO dbo.Dim_Tiempo (IdTiempo, Fecha, Anio, Trimestre, Mes, Dia)
                VALUES (@IdTiempo, @Fecha, @Anio, @Trimestre, @Mes, @Dia);
            END
            """;

        foreach (var fecha in fechas)
        {
            int idTiempo = (fecha.Year * 10000) + (fecha.Month * 100) + fecha.Day;
            int trimestre = ((fecha.Month - 1) / 3) + 1;

            await using var cmd = new SqlCommand(sql, connection, transaction);
            cmd.Parameters.Add("@IdTiempo", SqlDbType.Int).Value = idTiempo;
            cmd.Parameters.Add("@Fecha", SqlDbType.Date).Value = fecha;
            cmd.Parameters.Add("@Anio", SqlDbType.Int).Value = fecha.Year;
            cmd.Parameters.Add("@Trimestre", SqlDbType.Int).Value = trimestre;
            cmd.Parameters.Add("@Mes", SqlDbType.Int).Value = fecha.Month;
            cmd.Parameters.Add("@Dia", SqlDbType.Int).Value = fecha.Day;
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }
    }
}