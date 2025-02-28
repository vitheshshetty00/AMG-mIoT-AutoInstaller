using System.Collections.ObjectModel;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.IO;
using AMG_mIoT_AutoInstaller.ViewModels;

namespace AMG_mIoT_AutoInstaller.Models
{
    /// <summary>
    /// Represents an installable component with properties and basic validation.
    /// </summary>
    public class InstallableComponent : BaseViewModel
    {
        private Guid _id;
        private string _name = string.Empty;
        private string _description = string.Empty;
        private bool _isSelected;

        private ComponentType _type;
        private ComponentConfiguration _config;

        private ObservableCollection<string> _installationLogs = new ObservableCollection<string>();
        private string _status = "Pending";

        public InstallableComponent()
        {
            Id = Guid.NewGuid();
        }

        public Guid Id
        {
            get => _id;
            set => SetProperty(ref _id, value, nameof(Id));
        }

        [Required(ErrorMessage = "Component name is required.")]
        public string Name
        {
            get => _name;
            set
            {
                _name = value;
                OnPropertyChanged(nameof(Name));
            }
        }

        public string Description
        {
            get => _description;
            set
            {
                _description = value;
                OnPropertyChanged(nameof(Description));
            }
        }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                _isSelected = value;
                OnPropertyChanged(nameof(IsSelected));
            }
        }

        public ComponentType Type
        {
            get => _type;
            set
            {
                _type = value;
                OnPropertyChanged(nameof(Type));
            }
        }

        public ComponentConfiguration Config
        {
            get => _config;
            set
            {
                _config = value;
                OnPropertyChanged(nameof(Config));
            }
        }

        public ObservableCollection<string> InstallationLogs
        {
            get => _installationLogs;
            set => SetProperty(ref _installationLogs, value, nameof(InstallationLogs));
        }

        public string Status
        {
            get => _status;
            set => SetProperty(ref _status, value, nameof(Status));
        }

        public bool ValidateConfiguration()
        {
            return Config?.Validate() ?? false;
        }

        #region IDataErrorInfo Implementation

        public string this[string columnName]
        {
            get
            {
                var results = new List<ValidationResult>();
                var context = new ValidationContext(this) { MemberName = columnName };
                var property = GetType().GetProperty(columnName);
                if (property != null)
                {
                    var value = property.GetValue(this);
                    bool isValid = Validator.TryValidateProperty(value, context, results);
                    if (!isValid && results.Count > 0)
                    {
                        return results[0].ErrorMessage ?? string.Empty;
                    }
                }
                return string.Empty;
            }
        }

        public string Error
        {
            get
            {
                var results = new List<ValidationResult>();
                bool isValid = Validator.TryValidateObject(
                    this,
                    new ValidationContext(this),
                    results,
                    true
                );
                if (!isValid)
                {
                    return string.Join(Environment.NewLine, results);
                }
                return string.Empty;
            }
        }

        #endregion
    }

    public abstract class ComponentConfiguration
    {
        public abstract bool Validate();
    }

    public class IISConfig : ComponentConfiguration
    {
        public string WebsiteName { get; set; }

        public string Port { get; set; }

        public override bool Validate()
        {
            return !string.IsNullOrWhiteSpace(WebsiteName) && !string.IsNullOrWhiteSpace(Port);
        }
    }

    public class SQLServerConfig : ComponentConfiguration
    {
        public string ServerName { get; set; }

        public string DatabaseName { get; set; }

        public override bool Validate()
        {
            return !string.IsNullOrWhiteSpace(ServerName)
                && !string.IsNullOrWhiteSpace(DatabaseName);
        }
    }

    public class DotNetConfig : ComponentConfiguration
    {
        public string Version { get; set; }
        public string InstallPath { get; set; }

        public override bool Validate()
        {
            return !string.IsNullOrWhiteSpace(Version);
        }
    }

    public class IISEnableConfig : ComponentConfiguration
    {
        public bool EnableWebManagement { get; set; }
        public bool EnableAspNet { get; set; }

        public override bool Validate()
        {
            return true; // Basic features are controlled by checkboxes
        }
    }

    public class IISDeployConfig : ComponentConfiguration
    {
        public string WebsiteName { get; set; } = "";
        public string Port { get; set; } = "80";
        public string PhysicalPath { get; set; } = "";

        public override bool Validate()
        {
            if (
                string.IsNullOrWhiteSpace(WebsiteName)
                || string.IsNullOrWhiteSpace(Port)
                || string.IsNullOrWhiteSpace(PhysicalPath)
            )
                return false;

            if (!Directory.Exists(PhysicalPath))
                return false;

            if (!int.TryParse(Port, out int portNum) || portNum < 1 || portNum > 65535)
                return false;

            return true;
        }
    }

    public class FirewallConfig : ComponentConfiguration
    {
        public string Ports { get; set; } = "";
        private string _protocol = "TCP";
        public string Protocol
        {
            get => _protocol;
            set
            {
                if (_protocol != value)
                {
                    _protocol = value;
                    OnPropertyChanged(nameof(Protocol));
                }
            }
        }

        public string[] Protocols => new[] { "TCP", "UDP" };

        public string RuleName { get; set; } = "Custom Port Rule";
        public string Description { get; set; } = "Rule created by installer";

        public override bool Validate()
        {
            return !string.IsNullOrWhiteSpace(Ports) && !string.IsNullOrWhiteSpace(RuleName);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        // Add the OnPropertyChanged method
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class WindowsServiceConfig : ComponentConfiguration
    {
        public string ServiceName { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string ServicePath { get; set; } = "";
        public string Description { get; set; } = "Windows service installed via installer";
        public string StartupType { get; set; } = "Auto";
        public string ServiceAccount { get; set; } = "LocalSystem";
        public string ServicePassword { get; set; } = "";
        public string Dependencies { get; set; } = "";

        public string[] StartupTypes => new[] { "Auto", "Delayed", "Manual", "Disabled" };
        public string[] ServiceAccounts =>
            new[] { "LocalSystem", "LocalService", "NetworkService", "Custom" };

        public override bool Validate()
        {
            return !string.IsNullOrWhiteSpace(ServiceName)
                && !string.IsNullOrWhiteSpace(ServicePath)
                && File.Exists(ServicePath);
        }
    }
}
