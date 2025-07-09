﻿namespace FlowSynx.Plugins.Sqlite.Models;

internal class InputParameter
{
    public string Operation { get; set; } = string.Empty;
    public string Sql { get; set; } = string.Empty;
    public Dictionary<string, object>? Params { get; set; }
}