using FlowSynx.PluginCore;
using FlowSynx.PluginCore.Extensions;
using FlowSynx.Plugins.Sqlite.Models;
using FlowSynx.Plugins.Sqlite.Services;
using Microsoft.Data.Sqlite;

namespace FlowSynx.Plugins.Sqlite;

public class SqlitePlugin: IPlugin
{
    private IPluginLogger? _logger;
    private readonly IGuidProvider _guidProvider;
    private readonly IReflectionGuard _reflectionGuard;
    private SqlitePluginSpecifications _sqliteSpecifications = null!;
    private bool _isInitialized;

    public SqlitePlugin() : this(new GuidProvider(), new DefaultReflectionGuard()) { }

    internal SqlitePlugin(IGuidProvider guidProvider, IReflectionGuard reflectionGuard)
    {
        _guidProvider = guidProvider ?? throw new ArgumentNullException(nameof(guidProvider));
        _reflectionGuard = reflectionGuard ?? throw new ArgumentNullException(nameof(reflectionGuard));
    }

    public PluginMetadata Metadata => new()
    {
        Id = Guid.Parse("6457ab5d-0487-4c06-a313-1ebf789f2b52"),
        Name = "Sqlite",
        CompanyName = "FlowSynx",
        Description = Resources.PluginDescription,
        Version = new Version(1, 2, 0),
        Category = PluginCategory.Database,
        Authors = new List<string> { "FlowSynx" },
        Copyright = "© FlowSynx. All rights reserved.",
        Icon = "flowsynx.png",
        ReadMe = "README.md",
        RepositoryUrl = "https://github.com/flowsynx/plugin-sqlite",
        ProjectUrl = "https://flowsynx.io",
        Tags = new List<string>() { "flowSynx", "sql", "database", "data", "sqlite" },
        MinimumFlowSynxVersion = new Version(1, 1, 1)
    };

    public PluginSpecifications? Specifications { get; set; }

    public Type SpecificationsType => typeof(SqlitePluginSpecifications);

    private Dictionary<string, Func<InputParameter, CancellationToken, Task<object?>>> OperationMap =>
        new Dictionary<string, Func<InputParameter, CancellationToken, Task<object?>>>(StringComparer.OrdinalIgnoreCase)
        {
            ["query"] = async (p, t) => await ExecuteQueryAsync(p, t),
            ["execute"] = async (p, t) => { await ExecuteNonQueryAsync(p, t); return null; }
        };

    public IReadOnlyCollection<string> SupportedOperations => OperationMap.Keys;

    public Task Initialize(IPluginLogger logger)
    {
        ThrowIfReflection();
        ArgumentNullException.ThrowIfNull(logger);

        _sqliteSpecifications = Specifications.ToObject<SqlitePluginSpecifications>();
        _logger = logger;
        _isInitialized = true;

        return Task.CompletedTask;
    }

    public async Task<object?> ExecuteAsync(PluginParameters parameters, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfReflection();
        ThrowIfNotInitialized();

        var input = parameters.ToObject<InputParameter>();
        if (!OperationMap.TryGetValue(input.Operation, out var handler))
            throw new NotSupportedException($"Sqlite plugin: Operation '{input.Operation}' is not supported.");

        return await handler(input, cancellationToken);
    }

    #region private methods

    private async Task ExecuteNonQueryAsync(InputParameter input, CancellationToken token)
    {
        var (sql, sqlParams) = ExtractSqlParameters(input);
        var connectionString = _sqliteSpecifications.ConnectionString;

        try
        {
            await using var connection = new SqliteConnection(connectionString);
            await connection.OpenAsync(token);

            var context = ParseInputData(input.Data);

            if (context.StructuredData?.Count > 0)
            {
                await ExecuteStructuredDataAsync(connection, sql, context, token);
            }
            else
            {
                await ExecuteSingleNonQueryAsync(connection, sql, sqlParams, token);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Error executing Sqlite SQL statement: {ex.Message}");
            throw;
        }
    }

    private async Task<PluginContext> ExecuteQueryAsync(InputParameter input, CancellationToken token)
    {
        var (sql, sqlParams) = ExtractSqlParameters(input);
        var connectionString = _sqliteSpecifications.ConnectionString;

        try
        {
            var result = new List<Dictionary<string, object>>();
            await using var connection = new SqliteConnection(connectionString);
            await connection.OpenAsync(token);

            await using var cmd = new SqliteCommand(sql, connection);
            AddParameters(cmd, sqlParams);

            await using var reader = await cmd.ExecuteReaderAsync(token);
            while (await reader.ReadAsync(token))
            {
                var row = Enumerable.Range(0, reader.FieldCount)
                                    .ToDictionary(reader.GetName, reader.GetValue);
                result.Add(row);
            }

            _logger?.LogInfo($"Query executed successfully. Rows returned: {result.Count}.");

            return new PluginContext(_guidProvider.NewGuid().ToString(), "Data")
            {
                Format = "Database",
                StructuredData = result
            };
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Error executing Sqlite query: {ex.Message}");
            throw;
        }
    }

    private async Task ExecuteStructuredDataAsync(
        SqliteConnection connection, string sql, PluginContext context, CancellationToken token)
    {
        int totalAffected = 0;

        if (context.StructuredData is not null)
        {
            foreach (var row in context.StructuredData)
            {
                await using var cmd = new SqliteCommand(sql, connection);
                AddParameters(cmd, row);
                totalAffected += await cmd.ExecuteNonQueryAsync(token);
            }
        }

        _logger?.LogInfo($"Executed structured SQL for {context.StructuredData?.Count} rows. Total affected: {totalAffected}");
    }

    private async Task ExecuteSingleNonQueryAsync(
        SqliteConnection connection, string sql, Dictionary<string, object> sqlParams, CancellationToken token)
    {
        await using var cmd = new SqliteCommand(sql, connection);
        AddParameters(cmd, sqlParams);
        int affected = await cmd.ExecuteNonQueryAsync(token);
        _logger?.LogInfo($"Non-query executed successfully. Rows affected: {affected}.");
    }

    private (string Sql, Dictionary<string, object> Params) ExtractSqlParameters(InputParameter input)
    {
        if (string.IsNullOrWhiteSpace(input.Sql))
            throw new ArgumentException("Missing 'sql' parameter.");

        var sqlParams = input.Params as Dictionary<string, object> ?? new();
        return (input.Sql, sqlParams);
    }

    private void AddParameters(SqliteCommand cmd, Dictionary<string, object>? parameters)
    {
        if (parameters == null) return;

        foreach (var kvp in parameters)
        {
            var paramName = kvp.Key.StartsWith("@") ? kvp.Key : "@" + kvp.Key;
            var paramValue = kvp.Value;

            if (paramValue == null)
            {
                cmd.Parameters.AddWithValue(paramName, DBNull.Value);
            }
            else if (paramValue is Guid guidValue)
            {
                // Store GUID as TEXT
                cmd.Parameters.AddWithValue(paramName, guidValue.ToString());
            }
            else if (paramValue is string strValue)
            {
                // Try to detect if string is Guid
                if (Guid.TryParse(strValue, out var parsedGuid))
                {
                    cmd.Parameters.AddWithValue(paramName, parsedGuid.ToString());
                }
                else
                {
                    cmd.Parameters.AddWithValue(paramName, strValue);
                }
            }
            else if (paramValue is int or long or double or float or decimal)
            {
                cmd.Parameters.AddWithValue(paramName, paramValue);
            }
            else if (paramValue is bool boolValue)
            {
                // SQLite uses INTEGER for booleans: 0 = false, 1 = true
                cmd.Parameters.AddWithValue(paramName, boolValue ? 1 : 0);
            }
            else if (paramValue is DateTime dateValue)
            {
                // Store DateTime as ISO8601 string
                cmd.Parameters.AddWithValue(paramName, dateValue.ToString("yyyy-MM-dd HH:mm:ss"));
            }
            else if (paramValue is byte[] byteArray)
            {
                cmd.Parameters.AddWithValue(paramName, byteArray); // store as BLOB
            }
            else
            {
                throw new InvalidOperationException($"Unsupported parameter type for '{paramName}': {paramValue.GetType()}");
            }
        }
    }

    private PluginContext ParseInputData(object? data)
    {
        if (data is null)
            throw new ArgumentNullException(nameof(data), "Input data cannot be null.");

        return data switch
        {
            PluginContext context => context,
            IEnumerable<PluginContext> => throw new NotSupportedException("List of PluginContext is not supported."),
            _ => throw new NotSupportedException("Unsupported input data format.")
        };
    }

    private void ThrowIfReflection()
    {
        if (_reflectionGuard.IsCalledViaReflection())
            throw new InvalidOperationException(Resources.ReflectionBasedAccessIsNotAllowed);
    }

    private void ThrowIfNotInitialized()
    {
        if (!_isInitialized)
            throw new InvalidOperationException($"Plugin '{Metadata.Name}' v{Metadata.Version} is not initialized.");
    }

    #endregion
}