using FlowSynx.PluginCore.Helpers;

namespace FlowSynx.Plugins.Sqlite.Services;

internal class DefaultReflectionGuard : IReflectionGuard
{
    public bool IsCalledViaReflection() => ReflectionHelper.IsCalledViaReflection();
}