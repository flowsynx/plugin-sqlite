using FlowSynx.Plugins.Sqlite;
using FlowSynx.Plugins.Sqlite.Models;
using FlowSynx.PluginCore;
using FlowSynx.Plugins.Sqlite.Services;
using Microsoft.Data.Sqlite;
using Moq;
using System.Reflection;

public class SqlitePluginTests
{
    private SqlitePlugin CreatePlugin(string? connectionString = null, Mock<IPluginLogger>? loggerMock = null)
    {
        var guidProvider = new Mock<IGuidProvider>();
        guidProvider.Setup(g => g.NewGuid()).Returns(Guid.NewGuid());

        var reflectionGuard = new Mock<IReflectionGuard>();
        reflectionGuard.Setup(r => r.IsCalledViaReflection()).Returns(false);

        var plugin = new SqlitePlugin(guidProvider.Object, reflectionGuard.Object)
        {
            Specifications = new SqlitePluginSpecifications
            {
                ConnectionString = connectionString ?? "Data Source=:memory:"
            }
        };

        if (loggerMock != null)
            plugin.Initialize(loggerMock.Object).Wait();
        else
            plugin.Initialize(Mock.Of<IPluginLogger>()).Wait();

        return plugin;
    }

    [Fact]
    public async Task Initialize_SetsIsInitialized()
    {
        var plugin = CreatePlugin();
        Assert.True(typeof(SqlitePlugin).GetField("_isInitialized", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(plugin) as bool?);
    }

    [Fact]
    public async Task ExecuteAsync_ThrowsOnUnsupportedOperation()
    {
        var plugin = CreatePlugin();
        var parameters = new PluginParameters
        {
            { "Operation", "invalid" },
            { "Sql", "SELECT 1" }
        };

        await Assert.ThrowsAsync<NotSupportedException>(() => plugin.ExecuteAsync(parameters, CancellationToken.None));
    }

    [Fact]
    public void GetSqlAndParameters_ThrowsOnMissingSql()
    {
        var plugin = CreatePlugin();
        var input = new InputParameter { Sql = null! };
        var method = typeof(SqlitePlugin).GetMethod("ExtractSqlParameters", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var ex = Assert.Throws<TargetInvocationException>(() => method.Invoke(plugin, new object[] { input }));
        Assert.IsType<ArgumentException>(ex.InnerException);
        Assert.Contains("Missing 'sql' parameter", ex.InnerException!.Message);
    }

    [Fact]
    public void AddParameters_AddsVariousTypes()
    {
        var plugin = CreatePlugin();
        var cmd = new SqliteCommand();
        var parameters = new Dictionary<string, object>
        {
            { "int", 1 },
            { "str", "hello" },
            { "guid", Guid.NewGuid() },
            { "bool", true },
            { "date", DateTime.Now },
            { "bytes", new byte[] { 1, 2, 3 } }
        };
        var method = typeof(SqlitePlugin).GetMethod("AddParameters", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        method.Invoke(plugin, new object[] { cmd, parameters });
        Assert.Equal(6, cmd.Parameters.Count);
    }

    [Fact]
    public void ThrowIfReflection_ThrowsIfCalledViaReflection()
    {
        var guidProvider = new Mock<IGuidProvider>();
        var reflectionGuard = new Mock<IReflectionGuard>();
        reflectionGuard.Setup(r => r.IsCalledViaReflection()).Returns(true);

        var plugin = new SqlitePlugin(guidProvider.Object, reflectionGuard.Object);
        var method = typeof(SqlitePlugin).GetMethod("ThrowIfReflection", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var ex = Assert.Throws<TargetInvocationException>(() => method.Invoke(plugin, Array.Empty<object>()));
        Assert.IsType<InvalidOperationException>(ex.InnerException);
    }

    [Fact]
    public void ThrowIfNotInitialized_ThrowsIfNotInitialized()
    {
        var plugin = CreatePlugin();
        typeof(SqlitePlugin).GetField("_isInitialized", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.SetValue(plugin, false);
        var method = typeof(SqlitePlugin).GetMethod("ThrowIfNotInitialized", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var ex = Assert.Throws<TargetInvocationException>(() => method.Invoke(plugin, Array.Empty<object>()));
        Assert.IsType<InvalidOperationException>(ex.InnerException);
    }
}