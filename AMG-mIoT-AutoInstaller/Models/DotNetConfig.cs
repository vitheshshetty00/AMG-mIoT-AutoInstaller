using System.Collections.ObjectModel;

namespace AMG_mIoT_AutoInstaller.Models;

public class DotNetConfig : ComponentConfiguration
{
    // List of available versions, each with its own IsSelected flag.
    public ObservableCollection<DotNetVersionOption> AvailableVersions { get; set; } =
        new ObservableCollection<DotNetVersionOption>
        {
            new DotNetVersionOption
            {
                Version = "7",
                IsSelected = false,
                Description = "Microsoft.AspNetCore.App 7.0.20 and Microsoft.NETCore.App 7.0.20 ",
            },
            new DotNetVersionOption
            {
                Version = "8",
                IsSelected = false,
                Description = "Microsoft.AspNetCore.App 8.0.13 and Microsoft.NETCore.App 8.0.13",
            },
            new DotNetVersionOption
            {
                Version = "9",
                IsSelected = false,
                Description = "Microsoft.AspNetCore.App 9.0.2 and Microsoft.NETCore.App 9.0.2",
            },
        };

    // Option to force reinstallation
    public bool Force { get; set; } = false;

    public override bool Validate()
    {
        return AvailableVersions.Any(v => v.IsSelected);
    }
}

public class DotNetVersionOption : ComponentConfiguration
{
    public string Version { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected != value)
            {
                _isSelected = value;
                OnPropertyChanged(nameof(IsSelected));
            }
        }
    }

    public override bool Validate()
    {
        return !string.IsNullOrWhiteSpace(Version);
    }
}
