using FlowSynx.PluginCore.Helpers;
using FlowSynx.PluginCore;
using FlowSynx.PluginCore.Extensions;
using FlowSynx.Plugins.Sqlite.Models;
using Microsoft.Data.Sqlite;

namespace FlowSynx.Plugins.Sqlite;

public class SqlitePlugin: IPlugin
{
    private IPluginLogger? _logger;
    private SqlitePluginSpecifications _sqliteSpecifications = null!;
    private bool _isInitialized;

    public PluginMetadata Metadata => new PluginMetadata
    {
        Id = Guid.Parse("6457ab5d-0487-4c06-a313-1ebf789f2b52"),
        Name = "Sqlite",
        CompanyName = "FlowSynx",
        Description = Resources.PluginDescription,
        Version = new Version(1, 1, 0),
        Category = PluginCategory.Data,
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

    private Dictionary<string, Func<InputParameter, CancellationToken, Task<object?>>> OperationMap => new(StringComparer.OrdinalIgnoreCase)
    {
        ["query"] = async (parameters, cancellationToken) => await ExecuteQueryAsync(parameters, cancellationToken),
        ["execute"] = async (parameters, cancellationToken) => { await ExecuteNonQueryAsync(parameters, cancellationToken); return null; }
    };

    public IReadOnlyCollection<string> SupportedOperations => OperationMap.Keys;

    public Task Initialize(IPluginLogger logger)
    {
        if (ReflectionHelper.IsCalledViaReflection())
            throw new InvalidOperationException(Resources.ReflectionBasedAccessIsNotAllowed);

        ArgumentNullException.ThrowIfNull(logger);
        _sqliteSpecifications = Specifications.ToObject<SqlitePluginSpecifications>();
        _logger = logger;
        _isInitialized = true;
        return Task.CompletedTask;
    }

    public async Task<object?> ExecuteAsync(PluginParameters parameters, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (ReflectionHelper.IsCalledViaReflection())
            throw new InvalidOperationException(Resources.ReflectionBasedAccessIsNotAllowed);

        if (!_isInitialized)
            throw new InvalidOperationException($"Plugin '{Metadata.Name}' v{Metadata.Version} is not initialized.");

        var inputParameter = parameters.ToObject<InputParameter>();
        var operation = inputParameter.Operation;

        if (OperationMap.TryGetValue(operation, out var handler))
        {
            return await handler(inputParameter, cancellationToken);
        }

        throw new NotSupportedException($"Sqlite plugin: Operation '{operation}' is not supported.");
    }

    private async Task ExecuteNonQueryAsync(InputParameter parameters, CancellationToken cancellationToken)
    {
        var (sql, sqlParams) = GetSqlAndParameters(parameters);

        try
        {
            var connection = new SqliteConnection(_sqliteSpecifications.ConnectionString);
            await connection.OpenAsync();
            using var cmd = new SqliteCommand(parameters.Sql, connection);

            AddParameters(cmd, sqlParams);

            cancellationToken.ThrowIfCancellationRequested();

            int affectedRows = await cmd.ExecuteNonQueryAsync(cancellationToken);
            _logger?.LogInfo($"Non-query executed successfully. Rows affected: {affectedRows}.");
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Error executing Sqlite sql statement. Error: {ex.Message}");
            throw;
        }
    }

    private async Task<PluginContext> ExecuteQueryAsync(InputParameter parameters, CancellationToken cancellationToken)
    {
        var (sql, sqlParams) = GetSqlAndParameters(parameters);

        try
        {
            var result = new List<Dictionary<string, object>>();
            var connection = new SqliteConnection(_sqliteSpecifications.ConnectionString);
            await connection.OpenAsync();
            using var cmd = new SqliteCommand(sql, connection);

            AddParameters(cmd, sqlParams);

            cancellationToken.ThrowIfCancellationRequested();

            using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                var row = new Dictionary<string, object>();
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    row[reader.GetName(i)] = reader.GetValue(i);
                }
                result.Add(row);
            }

            _logger?.LogInfo($"Query executed successfully. Rows returned: {result.Count}.");
            string key = $"{Guid.NewGuid().ToString()}";
            return new PluginContext(key, "Data")
            {
                Format = "Database",
                StructuredData = result
            };
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Error executing Sqlite sql statement. Error: {ex.Message}");
            throw;
        }
    }

    private (string Sql, Dictionary<string, object> Parameters) GetSqlAndParameters(InputParameter parameters)
    {
        if (string.IsNullOrEmpty(parameters.Sql))
            throw new ArgumentException("Missing 'sql' parameter.");
        
        Dictionary<string, object> sqlParams = new();

        if (parameters.Params is Dictionary<string, object> paramDict)
        {
            sqlParams = paramDict;
        }

        return (parameters.Sql, sqlParams);
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
}