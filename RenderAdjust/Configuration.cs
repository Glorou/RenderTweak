using Dalamud.Configuration;
using Dalamud.Plugin;
using System;
using Dalamud.Utility;
using System.Security.Cryptography.X509Certificates;

namespace RenderAdjust;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 0;

    public int TargetFPS { get; set; } = 60;

    public bool Enabled { get; set; } = true;

    public bool Override { get; set; } = false;

    public int ObjectOverrideNum { get; set; } = 50;
    public bool Wine = Util.IsWine();
    // the below exist just to make saving less cumbersome
    public void Save()
    {
        Service.PluginInterface.SavePluginConfig(this);
    }
}
