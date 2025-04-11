using Dalamud.Configuration;
using Dalamud.Plugin;
using System;

namespace RenderAdjust;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 0;

    public int TargetFPS { get; set; } = 60;

    public bool Enabled { get; set; } = true;
    public bool IsConfigWindowMovable { get; set; } = true;
    public bool SomePropertyToBeSavedAndWithADefault { get; set; } = true;

    // the below exist just to make saving less cumbersome
    public void Save()
    {
        Plugin.PluginInterface.SavePluginConfig(this);
    }
}
