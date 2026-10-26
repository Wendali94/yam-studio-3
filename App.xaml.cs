using System.IO;
using System.Threading;
using System.Windows;
using YamStudio.Core;
using YamStudio.Views;

namespace YamStudio;

public partial class App : Application
{
    static Mutex _single;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        _single = new Mutex(true, "YamStudioManagerSingleInstance", out bool first);
        if (!first)
        {
            MessageBox.Show("YAM PICTURES Studio is already running.");
            Shutdown();
            return;
        }

        DispatcherUnhandledException += (_, ex) =>
        {
            try
            {
                string log = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "YamStudio", "error.log");
                Directory.CreateDirectory(Path.GetDirectoryName(log)!);
                File.AppendAllText(log, $"{DateTime.Now:s}\n{ex.Exception}\n\n");
            }
            catch { }
            MessageBox.Show(ex.Exception.Message, "YAM PICTURES");
            ex.Handled = true;
        };

        SettingsStore.Load();
        try { Directory.CreateDirectory(SettingsStore.Current.RootDir); } catch { }
        Loc.Lang = SettingsStore.Current.Lang;

        var main = new MainWindow();
        MainWindow = main;
        main.Show();
    }
}
