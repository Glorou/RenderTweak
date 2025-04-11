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

    private const string CommandName = "/pmycommand";

    public Configuration Configuration { get; init; }

    public readonly WindowSystem WindowSystem = new("SamplePlugin");
    private ConfigWindow ConfigWindow { get; init; }
    private MainWindow MainWindow { get; init; }

    private readonly System.Timers.Timer _timer = new();

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
        Service.PluginInterface.UiBuilder.OpenMainUi += ToggleMainUI;

        // Add a simple message to the log with level set to information
        // Use /xllog to open the log window in-game
        // Example Output: 00:57:54.959 | INF | [SamplePlugin] ===A cool log message from Sample Plugin===
        Service.Log.Information($"===A cool log message from {Service.PluginInterface.Manifest.Name}===");

    }

    
    void Enable()
    {
        _timer.Elapsed += Timer_Elapsed;
        _timer.Interval = 10000; // every 10 seconds
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
        var usage = GetGPUUsage(GetCounters()).Result;
        var fps = GetFPS();
        uint currentSetting;
        Service.GameConfig.TryGet(Dalamud.Game.Config.SystemConfigOption.DisplayObjectLimitType, out currentSetting);

        if (currentSetting < 4 && (usage > 80 || fps < Configuration.TargetFPS * 0.8f)) // Check if we're already at an extrema, if we are then no point in changing
        {
            Service.Log.Information($"Going down towards min {currentSetting} {usage} {fps} {Configuration.TargetFPS}");
            Service.GameConfig.Set(Dalamud.Game.Config.SystemConfigOption.DisplayObjectLimitType, currentSetting + 1);
        }
        else if (currentSetting > 0 && usage < 80 && fps > Configuration.TargetFPS * 0.95f)
        {
            Service.Log.Information($"Going up towards max {currentSetting} {usage} {GetFPS()} {Configuration.TargetFPS}");
            Service.GameConfig.Set(Dalamud.Game.Config.SystemConfigOption.DisplayObjectLimitType, currentSetting - 1);
        }
        return;
    }
    private void OnCommand(string command, string args)
    {
        // in response to the slash command, just toggle the display status of our main ui
        ToggleMainUI();
    }

    private void DrawUI() => WindowSystem.Draw();

    public void ToggleConfigUI() => ConfigWindow.Toggle();
    public void ToggleMainUI() => MainWindow.Toggle();



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
