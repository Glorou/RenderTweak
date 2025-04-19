using System;
using System.Linq;
using System.Numerics;
using Dalamud.Interface.Windowing;
using ImGuiNET;

namespace RenderAdjust.Windows;

public class ConfigWindow : Window, IDisposable
{
    private Configuration Configuration;

    // We give this window a constant ID using ###
    // This allows for labels being dynamic, like "{FPS Counter}fps###XYZ counter window",
    // and the window ID will always be "###XYZ counter window" for ImGui
    public ConfigWindow(Plugin plugin) : base("Render Adjust Settings###With a constant ID")
    {
        Flags =  ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar |
                ImGuiWindowFlags.NoScrollWithMouse;

        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(330, 250),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };
        SizeCondition = ImGuiCond.Always;

        Configuration = plugin.Configuration;
    }

    public void Dispose() { }

    public override void PreDraw()
    {
        // Flags must be added or removed before Draw() is being called, or they won't apply

    }

    public override void Draw()
    {
        // can't ref a property, so use a local copy
        var enabled = Configuration.Enabled;
        if (ImGui.Checkbox("Enable dynamic setting", ref enabled))
        {
            Configuration.Enabled = enabled;
            // can save immediately on change, if you don't want to provide a "Save and Close" button
            Configuration.Save();
        }
        ImGui.TextUnformatted("Target FPS");

        if (Service.PluginInterface.InstalledPlugins.Any(x => x.InternalName == "ChillFrames"))
        {
            ImGui.TextUnformatted("!!Note to Chill Frames users!!");
            ImGui.TextUnformatted("set this slightly below your lower limit");
        }
        var target = Configuration.TargetFPS;

        ImGui.InputInt("##target", ref target);
        if (ImGui.IsItemDeactivatedAfterEdit())
        {
            Configuration.TargetFPS = target;
            Configuration.Save();
        }
        var overrideEnabled = Configuration.Override;
        if (ImGui.Checkbox("Custom limiter", ref overrideEnabled))
        {
            Configuration.Override = overrideEnabled;
            if (!overrideEnabled)
            {

                Plugin.Override(50);
            }
            // can save immediately on change, if you don't want to provide a "Save and Close" button
            Configuration.Save();
        }
        ImGui.TextUnformatted("Minimum character limit");    

        var overrideLimit = Configuration.ObjectOverrideNum;
        ImGui.InputInt("##minLimit", ref overrideLimit);
        if (ImGui.IsItemDeactivatedAfterEdit())
        {
            if(overrideLimit < 0)
            {
                overrideLimit = 0;
            }if (overrideLimit > 100)
            {
                overrideLimit = 100;
            }
            Configuration.ObjectOverrideNum = overrideLimit;
            // can save immediately on change, if you don't want to provide a "Save and Close" button

        }
        if (ImGui.Button("Set limit"))
        {
            if (overrideEnabled) {
                Configuration.Save();
                Plugin.buttonCounter++;
            }

        }

    }
}
