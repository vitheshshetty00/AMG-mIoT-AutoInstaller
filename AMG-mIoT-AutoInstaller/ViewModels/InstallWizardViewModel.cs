using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using AMG_mIoT_AutoInstaller.Commands;
using AMG_mIoT_AutoInstaller.Models;
using AMG_mIoT_AutoInstaller.Services;
using Microsoft.WindowsAPICodePack.Dialogs;
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

        public ICommand NextCommand { get; }
        public ICommand PreviousCommand { get; }
        public ICommand BrowseCommand { get; }
        public ICommand StartInstallationCommand { get; }
        public ICommand AddCustomServiceCommand { get; }
        public ICommand RemoveCustomServiceCommand { get; }

        public string StartupPath { get; set; }
        private WizardStep _currentStep;

        private ObservableCollection<InstallableComponent>? _componentsToInstall;
        private ObservableCollection<InstallableComponent>? _selectedComponents;
        public ObservableCollection<string> InstallationLog { get; set; } = new();

        public InstallWizardViewModel(IInstallationService installationService)
        {
            _installationService =
                installationService ?? throw new ArgumentNullException(nameof(installationService));

            CurrentStep = WizardStep.Step1_SelectComponents;
            SelectedComponents = new ObservableCollection<InstallableComponent>();
            InitializeComponents();

            NextCommand = new RelayCommand(ExecuteNext, CanExecuteNext);
            PreviousCommand = new RelayCommand(ExecutePrevious, CanExecutePrevious);
            StartInstallationCommand = new RelayCommand(
                ExecuteStartInstallation,
                CanExecuteStartInstallation
            );
            BrowseCommand = new RelayCommand<string>(ExecuteBrowse);

            // New commands for Windows Services
            AddCustomServiceCommand = new RelayCommand(ExecuteAddCustomService);
            RemoveCustomServiceCommand = new RelayCommand<WindowsServiceConfig>(
                ExecuteRemoveCustomService
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
                    component.Config = new DotNetConfig();
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
                case ComponentType.SQLServerInstall:
                    component.Config = new SQLServerConfig
                    {
                        // Default SQL Server configuration paths
                        InstanceName = "MSSQLSERVER",
                        InstanceId = "MSSQLSERVER",
                        InstallSharedDir = "C:\\Program Files\\Microsoft SQL Server",
                        InstallSharedWowDir = "C:\\Program Files (x86)\\Microsoft SQL Server",
                        InstanceDir = "C:\\Program Files\\Microsoft SQL Server",
                        SqlUserDbDir = "C:\\Program Files\\Microsoft SQL Server",
                        SqlUserDbLogDir =
                            "C:\\Program Files\\Microsoft SQL Server\\MSSQL16.MSSQLSERVER\\MSSQL\\Data",
                        SqlTempDbDir = "C:\\Program Files\\Microsoft SQL Server",
                        SqlTempDbLogDir =
                            "C:\\Program Files\\Microsoft SQL Server\\MSSQL16.MSSQLSERVER\\MSSQL\\Data",
                        SaPassword = string.Empty,
                    };
                    break;
                case ComponentType.WindowsService:
                    // Initialize with the new WindowsServicesConfig
                    var servicesConfig = new WindowsServicesConfig();

                    // Load predefined services from the AMGmIoT Services folder
                    servicesConfig.AddPredefinedService();

                    // Add a default custom service if no predefined services found
                    if (servicesConfig.PredefinedServices.Count == 0)
                    {
                        servicesConfig.CustomServices.Add(
                            new WindowsServiceConfig
                            {
                                ServiceName = "MyService",
                                DisplayName = "My Service",
                                ServicePath = "",
                                Description = "Windows service installed via AMGIOT installer",
                                StartupType = "Auto",
                                ServiceAccount = "LocalSystem",
                            }
                        );
                    }

                    component.Config = servicesConfig;
                    break;
                case ComponentType.RestoreDatabase:
                    component.Config =
                        GetDefaultDBConfig()
                        ?? new DBRestoreConfig
                        {
                            ServerInstance = "localhost/MSSQLSERVER",
                            Password = "pctadmin$1234",
                            DatabaseName = "AMGIOT",
                            Username = "sa",
                        };
                    break;

                // Add other cases as needed
            }
        }

        private DBRestoreConfig? GetDefaultDBConfig()
        {
            string dbBackupPath = Path.Combine(AppContext.BaseDirectory, "DbBackup");
            if (!Directory.Exists(dbBackupPath))
            {
                Directory.CreateDirectory(dbBackupPath);
            }
            if (Directory.GetFiles(dbBackupPath, "*.bak").Length == 0)
            {
                return null;
            }
            else
            {
                //check sql installation is choosen or not
                if (
                    SelectedComponents
                        ?.Where(c => c.Type == ComponentType.SQLServerInstall)
                        ?.Where(c => c.IsSelected)
                        ?.Count() == 0
                )
                {
                    //get the sql config
                    var sqlConfig =
                        SelectedComponents
                            ?.Where(c => c.Type == ComponentType.SQLServerInstall)
                            ?.FirstOrDefault()
                            ?.Config as SQLServerConfig;
                    return new DBRestoreConfig
                    {
                        ServerInstance = sqlConfig?.InstanceName ?? @"localhost\MSSQLSERVER",
                        Password = sqlConfig?.SaPassword ?? "pctadmin$1234",
                        DatabaseName = "AMGIOT",
                        Username = "sa",
                        BackupFilePath = Directory.GetFiles(dbBackupPath, "*.bak")[0],
                    };
                }
                return new DBRestoreConfig
                {
                    ServerInstance = "localhost/MSSQLSERVER",
                    Password = "pctadmin$1234",
                    DatabaseName = "AMGIOT",
                    Username = "sa",
                    BackupFilePath = Directory.GetFiles(dbBackupPath, "*.bak")[0],
                };
            }
        }

        // New method to add a custom service
        private void ExecuteAddCustomService(object parameter)
        {
            if (SelectedConfigTab?.Config is WindowsServicesConfig servicesConfig)
            {
                servicesConfig.CustomServices.Add(
                    new WindowsServiceConfig
                    {
                        ServiceName = "NewService",
                        DisplayName = "New Service",
                        ServicePath = "",
                        Description = "Windows service installed via AMGIOT installer",
                    }
                );
            }
        }

        // New method to remove a custom service
        private void ExecuteRemoveCustomService(WindowsServiceConfig serviceConfig)
        {
            if (SelectedConfigTab?.Config is WindowsServicesConfig servicesConfig)
            {
                servicesConfig.CustomServices.Remove(serviceConfig);
            }
        }

        // Updated method to browse for service executable
        private void ExecuteBrowse(string propertyName)
        {
            if (
                SelectedConfigTab?.Config is WindowsServicesConfig servicesConfig
                && propertyName == "ServicePath"
            )
            {
                var dialog = new CommonOpenFileDialog
                {
                    Title = "Select Service Executable",
                    IsFolderPicker = false,
                    EnsureFileExists = true,
                    DefaultExtension = ".exe",
                    Filters = { new CommonFileDialogFilter("Executable Files", "*.exe") },
                };

                if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
                {
                    // For custom services, need to find which specific service is being configured
                    // In a real implementation, you might pass the service object as a parameter.
                    // For now, assuming it's for the last added custom service.
                    var lastService = servicesConfig.CustomServices.LastOrDefault();
                    if (lastService != null)
                    {
                        lastService.ServicePath = dialog.FileName;
                    }
                }
                return;
            }

            // Regular folder browse dialog for other property types
            var folderDialog = new CommonOpenFileDialog { IsFolderPicker = true };

            if (folderDialog.ShowDialog() == CommonFileDialogResult.Ok)
            {
                SetProperty(propertyName, folderDialog.FileName);
            }
        }

        private void SetProperty(string propertyName, string path)
        {
            var config = SelectedConfigTab.Config;
            var property = config.GetType().GetProperty(propertyName);
            if (property != null && property.PropertyType == typeof(string))
            {
                property.SetValue(config, path);
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

                    ComponentsToInstall = new ObservableCollection<InstallableComponent>(
                        //selected components except EnableIIS
                        selectedComponents!.Where(c => c.Type != ComponentType.EnableIIS)
                    );

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
