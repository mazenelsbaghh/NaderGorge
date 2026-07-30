using System.Globalization;
using System.Text;
using Npgsql;

namespace NaderGorge.API.Services;

public sealed record ThanaweyaResult(
    string SeatingNumber,
    string ArabicName,
    decimal? TotalDegree,
    string StudentCaseDescription);

public sealed record ThanaweyaImportOutcome(string Status, int ImportedRows, string? Detail = null);

/// <summary>
/// Keeps the public examination lookup backed by the cluster database.  The
/// Google workbook is only an import source; it is never sent to a browser.
/// </summary>
public sealed class ThanaweyaResultsService
{
    private const long ImportAdvisoryLock = 2026072801;
    private const string DefaultSourceUrl =
        "https://docs.google.com/spreadsheets/d/1gy12ll4iWapSag8YtZ0WnhbU9kgt85Sd/gviz/tq?tqx=out%3Acsv&tq=select%20A%2CB%2CC%2CD";

    private readonly HttpClient _httpClient;
    private readonly string _connectionString;
    private readonly ILogger<ThanaweyaResultsService> _logger;

    public ThanaweyaResultsService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<ThanaweyaResultsService> logger)
    {
        _httpClient = httpClient;
        _httpClient.Timeout = Timeout.InfiniteTimeSpan;
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Default database connection is required.");
        _logger = logger;
    }

    public async Task<ThanaweyaResult?> FindBySeatingNumberAsync(string seatingNumber, CancellationToken ct)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(ct);
        await using var command = new NpgsqlCommand(
            """
            SELECT seating_no, arabic_name, total_degree, student_case_desc
            FROM thanaweya_results
            WHERE seating_no = @seatingNumber
            LIMIT 1;
            """, connection);
        command.Parameters.AddWithValue("seatingNumber", seatingNumber);

        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
        {
            return null;
        }

        return new ThanaweyaResult(
            reader.GetString(0),
            reader.GetString(1),
            reader.IsDBNull(2) ? null : reader.GetDecimal(2),
            reader.IsDBNull(3) ? string.Empty : reader.GetString(3));
    }

    public async Task<ThanaweyaImportOutcome> ImportIfEmptyAsync(CancellationToken ct) =>
        await ImportAsync(force: false, ct);

    public async Task<ThanaweyaImportOutcome> ImportAsync(bool force, CancellationToken ct)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        if (!await TryAcquireLockAsync(connection, ct))
        {
            return new ThanaweyaImportOutcome("already-running", 0);
        }

        try
        {
            if (!force && await HasImportedRowsAsync(connection, ct))
            {
                return new ThanaweyaImportOutcome("already-imported", 0);
            }

            var sourceUrl = DefaultSourceUrl;
            using var response = await _httpClient.GetAsync(sourceUrl, HttpCompletionOption.ResponseHeadersRead, ct);
            response.EnsureSuccessStatusCode();
            await using var source = await response.Content.ReadAsStreamAsync(ct);
            using var csv = new StreamReader(source, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 128 * 1024);
            var header = ReadCsvRecord(csv);
            if (header is not ["seating_no", "arabic_name", "total_degree", "student_case_desc"])
            {
                throw new InvalidOperationException("The published examination sheet has an unexpected column layout.");
            }

            await using var transaction = await connection.BeginTransactionAsync(ct);
            await using (var staging = new NpgsqlCommand(
                "CREATE TEMP TABLE thanaweya_results_staging (seating_no varchar(20), arabic_name text, total_degree numeric(7,2), student_case_desc text) ON COMMIT DROP;",
                connection,
                transaction))
            {
                await staging.ExecuteNonQueryAsync(ct);
            }

            var importedRows = 0;
            await using (var importer = await connection.BeginBinaryImportAsync(
                "COPY thanaweya_results_staging (seating_no, arabic_name, total_degree, student_case_desc) FROM STDIN (FORMAT BINARY)", ct))
            {
                while (ReadCsvRecord(csv) is { } row)
                {
                    ct.ThrowIfCancellationRequested();
                    if (row.Count != 4 || string.IsNullOrWhiteSpace(row[0]) || string.IsNullOrWhiteSpace(row[1]))
                    {
                        continue;
                    }

                    var seatingNumber = row[0].Trim();
                    if (seatingNumber.Length > 20 || seatingNumber.Any(character => !char.IsAsciiDigit(character)))
                    {
                        continue;
                    }

                    await importer.StartRowAsync(ct);
                    await importer.WriteAsync(seatingNumber, ct);
                    await importer.WriteAsync(row[1].Trim(), ct);
                    if (decimal.TryParse(row[2], NumberStyles.Number, CultureInfo.InvariantCulture, out var degree))
                    {
                        await importer.WriteAsync(degree, ct);
                    }
                    else
                    {
                        await importer.WriteNullAsync(ct);
                    }
                    await importer.WriteAsync(row[3].Trim(), ct);
                    importedRows++;
                }

                await importer.CompleteAsync(ct);
            }

            if (importedRows == 0)
            {
                throw new InvalidOperationException("The examination sheet did not contain importable results.");
            }

            await using (var replace = new NpgsqlCommand(
                """
                LOCK TABLE thanaweya_results IN ACCESS EXCLUSIVE MODE;
                TRUNCATE thanaweya_results;
                INSERT INTO thanaweya_results (seating_no, arabic_name, total_degree, student_case_desc)
                SELECT DISTINCT ON (seating_no) seating_no, arabic_name, total_degree, student_case_desc
                FROM thanaweya_results_staging
                ORDER BY seating_no;
                ANALYZE thanaweya_results;
                """, connection, transaction))
            {
                await replace.ExecuteNonQueryAsync(ct);
            }

            await transaction.CommitAsync(ct);
            _logger.LogInformation("Imported {ImportedRows} Thanaweya results from the approved source.", importedRows);
            return new ThanaweyaImportOutcome("imported", importedRows);
        }
        finally
        {
            await ReleaseLockAsync(connection);
        }
    }

    private static async Task<bool> HasImportedRowsAsync(NpgsqlConnection connection, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("SELECT EXISTS (SELECT 1 FROM thanaweya_results LIMIT 1);", connection);
        return (bool)(await command.ExecuteScalarAsync(ct) ?? false);
    }

    private static async Task<bool> TryAcquireLockAsync(NpgsqlConnection connection, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("SELECT pg_try_advisory_lock(@key);", connection);
        command.Parameters.AddWithValue("key", ImportAdvisoryLock);
        return (bool)(await command.ExecuteScalarAsync(ct) ?? false);
    }

    private static async Task ReleaseLockAsync(NpgsqlConnection connection)
    {
        await using var command = new NpgsqlCommand("SELECT pg_advisory_unlock(@key);", connection);
        command.Parameters.AddWithValue("key", ImportAdvisoryLock);
        await command.ExecuteNonQueryAsync();
    }

    private static List<string>? ReadCsvRecord(TextReader reader)
    {
        var values = new List<string>(4);
        var value = new StringBuilder();
        var quoted = false;
        var hasInput = false;

        while (reader.Read() is { } next)
        {
            hasInput = true;
            var character = (char)next;
            if (quoted)
            {
                if (character == '"')
                {
                    if (reader.Peek() == '"')
                    {
                        reader.Read();
                        value.Append('"');
                    }
                    else
                    {
                        quoted = false;
                    }
                }
                else
                {
                    value.Append(character);
                }
                continue;
            }

            if (character == '"' && value.Length == 0)
            {
                quoted = true;
            }
            else if (character == ',')
            {
                values.Add(value.ToString());
                value.Clear();
            }
            else if (character == '\n')
            {
                values.Add(value.ToString());
                return values;
            }
            else if (character != '\r')
            {
                value.Append(character);
            }
        }

        if (!hasInput && value.Length == 0 && values.Count == 0)
        {
            return null;
        }

        values.Add(value.ToString());
        return values;
    }
}

public sealed class ThanaweyaResultsImportHostedService : BackgroundService
{
    private readonly ThanaweyaResultsService _resultsService;
    private readonly ILogger<ThanaweyaResultsImportHostedService> _logger;

    public ThanaweyaResultsImportHostedService(
        ThanaweyaResultsService resultsService,
        ILogger<ThanaweyaResultsImportHostedService> logger)
    {
        _resultsService = resultsService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            var outcome = await _resultsService.ImportIfEmptyAsync(stoppingToken);
            _logger.LogInformation("Thanaweya results startup import finished with {Status}; rows: {Rows}.", outcome.Status, outcome.ImportedRows);
        }
        catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogError(exception, "Thanaweya results startup import did not complete.");
        }
    }
}
