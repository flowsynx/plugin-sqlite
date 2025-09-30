namespace FlowSynx.Plugins.Sqlite.Services;

public interface IReflectionGuard
{
    bool IsCalledViaReflection();
}