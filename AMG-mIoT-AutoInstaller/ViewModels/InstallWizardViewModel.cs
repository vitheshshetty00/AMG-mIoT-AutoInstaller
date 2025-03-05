using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using AMG_mIoT_AutoInstaller.Commands;
using AMG_mIoT_AutoInstaller.Models;
using AMG_mIoT_AutoInstaller.Services;
using AMG_mIoT_AutoInstaller.Views;
using static AMG_mIoT_AutoInstaller.Models.SQLServerConfig;

namespace AMG_mIoT_AutoInstaller.ViewModels
{
    public enum WizardStep
    {
        Step1_SelectComponents,
        Step2_ConfigureComponents,
        Step3_Summary,
    }

    public class InstallWizardViewModel : BaseViewModel
    {
        private readonly IInstallationService _installationService;
        private InstallableComponent _selectedConfigTab;

        public ICommand BrowseFolderCommand { get; }
        public ICommand NextCommand { get; }
        public ICommand PreviousCommand { get; }
        public ICommand StartInstallationCommand { get; }

        public string StartupPath { get; set; }
        private WizardStep _currentStep;

        private ObservableCollection<InstallableComponent>? _componentsToInstall;
        private ObservableCollection<InstallableComponent>? _selectedComponents;
        public ObservableCollection<string> InstallationLog { get; set; } = [];

        public InstallWizardViewModel(IInstallationService installationService)
        {
            _installationService =
                installationService ?? throw new ArgumentNullException(nameof(installationService));

            CurrentStep = WizardStep.Step1_SelectComponents;
            SelectedComponents = [];
            InitializeComponents();

            NextCommand = new RelayCommand(ExecuteNext, CanExecuteNext);
            PreviousCommand = new RelayCommand(ExecutePrevious, CanExecutePrevious);
            StartInstallationCommand = new RelayCommand(
                ExecuteStartInstallation,
                CanExecuteStartInstallation
            );
            StartupPath = AppContext.BaseDirectory;
        }

        private void InitializeComponents()
        {
            var components = new List<InstallableComponent>
            {
                new()
                {
                    Type = ComponentType.EnableIIS,
                    Name = "Enable IIS Features",
                    Description = "Enables required IIS features and components",
                },
                new()
                {
                    Type = ComponentType.DeployIIS,
                    Name = "Deploy to IIS",
                    Description = "Configures and deploys website to IIS",
                },
                new()
                {
                    Type = ComponentType.DotnetInstall,
                    Name = ".NET Runtime Installation",
                    Description = "Installs required .NET runtime version",
                },
                new()
                {
                    Type = ComponentType.SQLServerInstall,
                    Name = "SQL Server Installation",
                    Description = "Installs and configures SQL Server",
                },
                new()
                {
                    Type = ComponentType.Firewall,
                    Name = "Firewall Configuration",
                    Description = "Sets up required firewall rules",
                },
                new()
                {
                    Type = ComponentType.WindowsService,
                    Name = "Windows Services",
                    Description = "Installs and configures Windows services",
                },
                new()
                {
                    Type = ComponentType.RestoreDatabase,
                    Name = "Restore Database",
                    Description = "Restores database from backup",
                },
            };

            foreach (var component in components)
            {
                SelectedComponents?.Add(component);
            }
        }

        private void InitializeComponentConfig(InstallableComponent component)
        {
            switch (component.Type)
            {
                case ComponentType.EnableIIS:
                    component.Config = new IISEnableConfig
                    {
                        EnableWebManagement = true,
                        EnableAspNet = true,
                    };
                    break;
                case ComponentType.DeployIIS:
                    component.Config = new IISDeployConfig
                    {
                        WebsiteName = "Default Website",
                        Port = "80",
                        PhysicalPath = @"C:\inetpub\wwwroot\myapp",
                    };
                    break;
                case ComponentType.DotnetInstall:
                    component.Config = new DotNetConfig
                    {
                        Version = "8.0",
                        InstallPath = @"C:\Program Files\dotnet",
                    };
                    break;

                case ComponentType.Firewall:
                    component.Config = new FirewallConfig
                    {
                        Ports = "80,443",
                        Protocol = "TCP",
                        RuleName = "Default Web Ports",
                        Description = "Allow incoming web traffic",
                    };
                    break;
                case ComponentType.WindowsService:
                    component.Config = new WindowsServiceConfig
                    {
                        ServiceName = "MyService",
                        ServicePath = "",
                        Description = "Windows service installed via installer",
                        StartupType = "Auto",
                        ServiceAccount = "LocalSystem",
                    };
                    break;
                // Add other cases as needed
            }
        }

        public InstallableComponent SelectedConfigTab
        {
            get => _selectedConfigTab;
            set
            {
                _selectedConfigTab = value;
                OnPropertyChanged(nameof(SelectedConfigTab));
            }
        }

        public WizardStep CurrentStep
        {
            get => _currentStep;
            set
            {
                if (_currentStep != value)
                {
                    _currentStep = value;
                    OnPropertyChanged(nameof(CurrentStep));
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public ObservableCollection<InstallableComponent>? SelectedComponents
        {
            get => _selectedComponents;
            set
            {
                _selectedComponents = value;
                OnPropertyChanged(nameof(SelectedComponents));
            }
        }

        public ObservableCollection<InstallableComponent>? ComponentsToInstall
        {
            get => _componentsToInstall;
            set
            {
                _componentsToInstall = value;
                OnPropertyChanged(nameof(ComponentsToInstall));
            }
        }

        private void ExecuteNext(object parameter)
        {
            switch (CurrentStep)
            {
                case WizardStep.Step1_SelectComponents:
                    var selectedComponents = SelectedComponents?.Where(c => c.IsSelected).ToList();
                    if (selectedComponents?.Count == 0)
                    {
                        _ = MessageBox.Show(
                            "Please select at least one component to install.",
                            "Selection Required",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning
                        );
                        return;
                    }
                    selectedComponents?.ForEach(InitializeComponentConfig);

                    ComponentsToInstall = [.. selectedComponents ?? []];

                    CurrentStep = WizardStep.Step2_ConfigureComponents;
                    break;

                case WizardStep.Step2_ConfigureComponents:
                    if (ValidateConfigurations())
                    {
                        CurrentStep = WizardStep.Step3_Summary;
                    }
                    break;

                default:
                    break;
            }
        }

        private bool ValidateConfigurations()
        {
            foreach (var component in ComponentsToInstall!)
            {
                if (component == null || !component.ValidateConfiguration())
                {
                    _ = MessageBox.Show(
                        $"Please complete all required configuration fields for {component?.Name ?? "Unknown Component"}",
                        "Configuration Required",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning
                    );
                    return false;
                }
            }
            return true;
        }

        private bool CanExecuteNext(object parameter)
        {
            if (CurrentStep == WizardStep.Step1_SelectComponents)
            {
                return SelectedComponents?.Any(c => c.IsSelected) == true;
            }
            return CurrentStep != WizardStep.Step3_Summary;
        }

        private void ExecutePrevious(object parameter)
        {
            if (CurrentStep == WizardStep.Step3_Summary)
            {
                CurrentStep = WizardStep.Step2_ConfigureComponents;
            }
            else if (CurrentStep == WizardStep.Step2_ConfigureComponents)
            {
                CurrentStep = WizardStep.Step1_SelectComponents;
            }
        }

        private bool CanExecutePrevious(object parameter)
        {
            return CurrentStep != WizardStep.Step1_SelectComponents;
        }

        private void ExecuteStartInstallation(object parameter)
        {
            if (ComponentsToInstall == null)
            {
                Debug.WriteLine("No components to install.");
                return;
            }
            _installationService.StartInstallation(ComponentsToInstall, InstallationLog);
        }

        private bool CanExecuteStartInstallation(object parameter)
        {
            return CurrentStep == WizardStep.Step3_Summary;
        }
    }
}
