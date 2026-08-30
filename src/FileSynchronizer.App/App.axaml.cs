using System;
using System.IO;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using FileSynchronizer.App.Services;
using FileSynchronizer.App.ViewModels;
using FileSynchronizer.App.Views;
using FileSynchronizer.Core;
using FileSynchronizer.Infrastructure;

namespace FileSynchronizer.App;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var applicationDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "FileSynchronizer2");
            var settings = new ApplicationSettingsStore(
                Path.Combine(applicationDataPath, "settings.json"))
                .Load();
            var syncStore = new SqliteSyncStore(Path.Combine(applicationDataPath, "sync-state.db"));
            var syncServiceFactory = new SyncApplicationServiceFactory(
                syncStore,
                syncStore,
                new SyncResultRetention(settings.MaximumRetainedSyncResults));
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainViewModel(syncServiceFactory),
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
