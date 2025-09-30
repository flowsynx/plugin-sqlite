namespace FlowSynx.Plugins.Sqlite.Services;

internal class GuidProvider : IGuidProvider
{
    public Guid NewGuid() => Guid.NewGuid();
}