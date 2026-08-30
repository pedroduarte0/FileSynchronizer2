using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using FileSynchronizer.App.Services;
using FileSynchronizer.Core;

namespace FileSynchronizer.App.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly SyncApplicationServiceFactory? _syncServiceFactory;

    public MainViewModel()
    {
    }

    public MainViewModel(SyncApplicationServiceFactory syncServiceFactory)
    {
        _syncServiceFactory = syncServiceFactory;
    }

    [ObservableProperty]
    public partial string Greeting { get; set; } = "Welcome to Avalonia!";

    public SyncApplicationService CreateSyncService(IEnumerable<ISyncLocationProvider> providers)
    {
        return (_syncServiceFactory
            ?? throw new InvalidOperationException("Sync services are unavailable in the designer."))
            .Create(providers);
    }
}
