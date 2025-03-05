using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
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
        private ComponentConfiguration? _config;

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

    public class IISEnableConfig : ComponentConfiguration
    {
        public bool EnableWebManagement { get; set; }
        public bool EnableAspNet { get; set; }

        public override bool Validate()
        {
            return true; // Basic features are controlled by checkboxes
        }
    }
}
