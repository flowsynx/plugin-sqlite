using FlowSynx.PluginCore;

namespace FlowSynx.Plugins.Sqlite.Models;

public class SqlitePluginSpecifications: PluginSpecifications
{
    [RequiredMember]
    public string ConnectionString { get; set; } = string.Empty;
}