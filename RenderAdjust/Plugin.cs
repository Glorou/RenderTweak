using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using System.IO;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using System.Timers;
using Dalamud.Utility;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using RenderAdjust.Windows;
using System;
using static Lumina.Models.Materials.Texture;
using Dalamud;
namespace RenderAdjust;

/*.rdata:0000000142056BC0 dword_142056BC0 dd 819 
*.rdata:0000000142056BC4                 dd 200
*.rdata:0000000142056BC8                 dd 100
*.rdata:0000000142056BCC                 dd 75
 * 1409533C1 dd 50
 * Framework->FrameRate
 */
public sealed class Plugin : IDalamudPlugin
{

    private const string CommandName = "/render";

    public Configuration Configuration { get; init; }

    public readonly WindowSystem WindowSystem = new("Render Adjust");
    private ConfigWindow ConfigWindow { get; init; }
    private MainWindow MainWindow { get; init; }

    private readonly System.Timers.Timer _timer = new();

    public float AvgGPUUsage = 0.0f;
    public float AvgFPS = 0.0f;

    public float[] FPSSamples = [0.0f, 0.0f, 0.0f, 0.0f, 0.0f];
    public float[] GPUUsageSamples = [0.0f,0.0f,0.0f,0.0f,0.0f];

    public Plugin(IDalamudPluginInterface pluginInterface)
    {
        pluginInterface.Create<Service>();
        Configuration = pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        // you might normally want to embed resources and load them from the manifest stream
        var goatImagePath = Path.Combine(pluginInterface.AssemblyLocation.Directory?.FullName!, "goat.png");

        ConfigWindow = new ConfigWindow(this);
        MainWindow = new MainWindow(this, goatImagePath);

        WindowSystem.AddWindow(ConfigWindow);
        WindowSystem.AddWindow(MainWindow);

        Service.CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "A useful message to display in /xlhelp"
        });

        Service.PluginInterface.UiBuilder.Draw += DrawUI;

        // This adds a button to the plugin installer entry of this plugin which allows
        // to toggle the display status of the configuration ui
        Service.PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUI;

        // Adds another button that is doing the same but for the main ui of the plugin
        //Service.PluginInterface.UiBuilder.OpenMainUi += ToggleMainUI;

        // Add a simple message to the log with level set to information
        // Use /xllog to open the log window in-game
        // Example Output: 00:57:54.959 | INF | [SamplePlugin] ===A cool log message from Sample Plugin===
        Service.Log.Information($"==={Service.PluginInterface.Manifest.Name} starting up===");

        Enable();


    }


    public void Enable()
    {
        _timer.Elapsed += Timer_Elapsed;
        _timer.Interval = 3000; // every 3 seconds
        _timer.Start();
    }
    public void Disable()
    {
        _timer.Stop();
    }

    public void Dispose()
    {
        WindowSystem.RemoveAllWindows();

        ConfigWindow.Dispose();
        MainWindow.Dispose();
        Disable();
        _timer.Dispose();


        Service.CommandManager.RemoveHandler(CommandName);
    }

    private void Timer_Elapsed(object? sender, ElapsedEventArgs e)
    {
        if (Configuration.Enabled == true && !Configuration.Wine)
        {
            var usage = GetGPUUsage(GetCounters()).Result;
            var fps = GetFPS();
            GetAverage(fps, usage);

            uint currentSetting;
            Service.GameConfig.TryGet(Dalamud.Game.Config.SystemConfigOption.DisplayObjectLimitType, out currentSetting);
            if (AvgFPS == 0.0 || AvgGPUUsage == 0.0)
            {
                return;
            }
            if (Configuration.Override == false)
            {
                if (currentSetting < 4 && ((AvgGPUUsage > 80 && AvgFPS < Configuration.TargetFPS * 0.8f) || (AvgFPS < Configuration.TargetFPS * 0.65f))) // Check if we're already at an extrema, if we are then no point in changing
                {
                    Service.Log.Information($"Going down towards min {currentSetting} {AvgGPUUsage} {AvgFPS} {Configuration.TargetFPS}");
                    Service.GameConfig.Set(Dalamud.Game.Config.SystemConfigOption.DisplayObjectLimitType, currentSetting + 1);
                }
                else if (currentSetting > 0 && ((AvgGPUUsage < 80 && AvgFPS > Configuration.TargetFPS * 0.95f) || (AvgGPUUsage < 40))) // cant put it at 1.0 for stuff like frame limiters and chill frames, accounting for error we'd never see this condition otherwise
                {
                    Service.Log.Information($"Going up towards max {currentSetting} {AvgGPUUsage} {AvgFPS} {Configuration.TargetFPS}");
                    Service.GameConfig.Set(Dalamud.Game.Config.SystemConfigOption.DisplayObjectLimitType, currentSetting - 1);
                }
            }

        }else if (Configuration.Enabled == true && Configuration.Wine) //Config for Wine systems since I cant check gpu usage https://github.com/goatcorp/Dalamud/blob/master/Dalamud/Utility/Util.cs#L509 for later when I can 
        {

            var fps = GetFPS();
            GetAverage(fps, 0.0f);

            uint currentSetting;
            Service.GameConfig.TryGet(Dalamud.Game.Config.SystemConfigOption.DisplayObjectLimitType, out currentSetting);
            if (AvgFPS == 0.0)
            {
                return;
            }
            if (Configuration.Override == false)
            {
                if (currentSetting < 4 && AvgFPS < Configuration.TargetFPS * 0.7f) // Check if we're already at an extrema, if we are then no point in changing
                {
                    Service.Log.Information($"Going down towards min {currentSetting} {AvgGPUUsage} {AvgFPS} {Configuration.TargetFPS}");
                    Service.GameConfig.Set(Dalamud.Game.Config.SystemConfigOption.DisplayObjectLimitType, currentSetting + 1);
                }
                else if (currentSetting > 0 && AvgFPS > Configuration.TargetFPS * 0.95f) // cant put it at 1.0 for stuff like frame limiters and chill frames, accounting for error we'd never see this condition otherwise
                {
                    Service.Log.Information($"Going up towards max {currentSetting} {AvgGPUUsage} {AvgFPS} {Configuration.TargetFPS}");
                    Service.GameConfig.Set(Dalamud.Game.Config.SystemConfigOption.DisplayObjectLimitType, currentSetting - 1);
                }
            }

        }


        return;
    }

    public static void Override(int limit)
    {
        Service.Framework.RunOnFrameworkThread(() =>
        {
            if (Service.GameConfig.System.TryGet("DisplayObjectLimitType", out uint limitType))
            {
                Service.Log.Verbose("object limit = " + limitType); //4 = minimum which is the fallback to 50
            }
            Service.GameConfig.Set(Dalamud.Game.Config.SystemConfigOption.DisplayObjectLimitType, 4);
            var address = Service.SigScanner.ScanText("BE ?? ?? ?? ?? 89 74 24 58");
            SafeMemory.Write(address + 1, (byte)limit); //We overwrite the 50 with the value we want
            SafeMemory.Write(address + 2, (byte)0);
            SafeMemory.Write(address + 3, (byte)0);
            SafeMemory.Write(address + 4, (byte)0);
        });
    }

    private void OnCommand(string command, string args)
    {
        // in response to the slash command, just toggle the display status of our main ui
        Service.Log.Information($"{GPUUsageSamples[0]} {GPUUsageSamples[1]} + {GPUUsageSamples[2]} + {GPUUsageSamples[3]} + {GPUUsageSamples[4]}");
        
        Service.Log.Information($"{FPSSamples[0]} + {FPSSamples[1]} + {FPSSamples[2]} + {FPSSamples[3]} + {FPSSamples[4]}");
        //ToggleMainUI();
    }

    private void DrawUI() => WindowSystem.Draw();

    public void ToggleConfigUI() => ConfigWindow.Toggle();
    //public void ToggleMainUI() => MainWindow.Toggle();

    public void GetAverage(float fps, float usage)
    {
        Array.Copy(FPSSamples, 1, FPSSamples, 0, FPSSamples.Length - 1);
        FPSSamples[4] = fps;
        Array.Copy(GPUUsageSamples, 1, GPUUsageSamples, 0, GPUUsageSamples.Length - 1);
        GPUUsageSamples[4] = usage;

        if (FPSSamples[0] != 0.0f && GPUUsageSamples[0] != 0.0f)
        {
            AvgGPUUsage = (GPUUsageSamples[0] + GPUUsageSamples[1] + GPUUsageSamples[2] + GPUUsageSamples[3] + GPUUsageSamples[4]) / 5;
            AvgFPS = (FPSSamples[0] + FPSSamples[1] + FPSSamples[2] + FPSSamples[3] + FPSSamples[4]) / 5;
        }
    }

    public List<PerformanceCounter> GetCounters()
    {
        var category = new PerformanceCounterCategory("GPU Engine");
        var names = category.GetInstanceNames();
        var utilization = names
                            .Where(counterName => counterName.EndsWith("engtype_3D"))
                            .SelectMany(counterName => category.GetCounters(counterName))
                            .Where(counter => counter.CounterName.Equals("Utilization Percentage"))
                            .ToList();
        return utilization;
    }

    public static async Task<float> GetGPUUsage(List<PerformanceCounter> gpuCounters)
    {

        if (!Service.Framework.IsInFrameworkUpdateThread)
        {
            gpuCounters.ForEach(x => x.NextValue());

            Thread.Sleep(1000);

            var result = gpuCounters.Sum(x => x.NextValue());

            return result;
        }
        else
        {
            var result = await Task.Run(() =>
            {
                gpuCounters.ForEach(x => x.NextValue());

                Thread.Sleep(1000);

                return gpuCounters.Sum(x => x.NextValue());
            });
            return result;
        }
    }



    public float GetFPS()
    {
        var temp = 1 / Service.Framework.UpdateDelta.TotalSeconds;
        return (float)temp;
    }

}
