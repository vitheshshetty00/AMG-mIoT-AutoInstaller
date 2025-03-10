using System;
using System.Collections.ObjectModel;
using System.IO;

namespace AMG_mIoT_AutoInstaller.Models;

public class WindowsServiceConfig : ComponentConfiguration
{
    private string _serviceName = "";
    private bool _isSelected;

    public string ServiceName
    {
        get => _serviceName;
        set
        {
            if (_serviceName != value)
            {
                _serviceName = value;
                if (string.IsNullOrEmpty(DisplayName))
                    DisplayName = value;
                OnPropertyChanged(nameof(ServiceName));
            }
        }
    }

    public string DisplayName { get; set; } = "";
    public string ServicePath { get; set; } = "";
    public string Description { get; set; } = "Windows service installed via AMGIOT  installer";
    public string StartupType { get; set; } = "Auto";
    public string ServiceAccount { get; set; } = "LocalSystem";

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            _isSelected = value;
            OnPropertyChanged(nameof(IsSelected));
        }
    }

    public override bool Validate()
    {
        return !string.IsNullOrWhiteSpace(ServiceName)
            && !string.IsNullOrWhiteSpace(ServicePath)
            && File.Exists(ServicePath);
    }
}

public class WindowsServicesConfig : ComponentConfiguration
{
    public ObservableCollection<WindowsServiceConfig> PredefinedServices { get; } = [];

    public ObservableCollection<WindowsServiceConfig> CustomServices { get; } = [];

    public override bool Validate()
    {
        var allServices = PredefinedServices.Where(s => s.IsSelected).Concat(CustomServices);
        return allServices.Any() && allServices.All(s => s.Validate());
    }

    public void AddPredefinedService()
    {
        string serviceDirPath = Path.Combine(AppContext.BaseDirectory, "AMGmIoT Services");
        if (!Directory.Exists(serviceDirPath))
            return;
        DirectoryInfo PredefinedServicesDir = new(serviceDirPath);
        foreach (var serviceDir in PredefinedServicesDir.GetDirectories())
        {
            //Find the service executable file(any file that has .exe extension)
            string servicePath =
                Directory.GetFiles(serviceDir.FullName, "*.exe").FirstOrDefault() ?? "";

            if (File.Exists(servicePath))
            {
                PredefinedServices.Add(
                    new WindowsServiceConfig
                    {
                        ServiceName = serviceDir.Name,
                        ServicePath = servicePath,
                        IsSelected = true,
                    }
                );
            }
        }
    }
}
