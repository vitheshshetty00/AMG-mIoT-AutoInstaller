using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using AMG_mIoT_AutoInstaller.Commands;
using AMG_mIoT_AutoInstaller.Models;
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
        private WizardStep _currentStep;
        private ObservableCollection<InstallableComponent> _componentsToInstall;
        private ObservableCollection<InstallableComponent> _selectedComponents;
        public ObservableCollection<string> InstallationLog { get; set; } =
            new ObservableCollection<string>();

        // Add this property to track the selected tab
        private object _selectedConfigTab;

        public InstallWizardViewModel()
        {
            CurrentStep = WizardStep.Step1_SelectComponents;
            SelectedComponents = new ObservableCollection<InstallableComponent>();
            InitializeComponents();

            NextCommand = new RelayCommand(ExecuteNext, CanExecuteNext);
            PreviousCommand = new RelayCommand(ExecutePrevious, CanExecutePrevious);
            StartInstallationCommand = new RelayCommand(
                ExecuteStartInstallation,
                CanExecuteStartInstallation
            );
        }

        private void InitializeComponents()
        {
            var components = new List<InstallableComponent>
            {
                new InstallableComponent
                {
                    Type = ComponentType.EnableIIS,
                    Name = "Enable IIS Features",
                    Description = "Enables required IIS features and components",
                },
                new InstallableComponent
                {
                    Type = ComponentType.DeployIIS,
                    Name = "Deploy to IIS",
                    Description = "Configures and deploys website to IIS",
                },
                new InstallableComponent
                {
                    Type = ComponentType.DotnetInstall,
                    Name = ".NET Runtime Installation",
                    Description = "Installs required .NET runtime version",
                },
                new InstallableComponent
                {
                    Type = ComponentType.SQLServerInstall,
                    Name = "SQL Server Installation",
                    Description = "Installs and configures SQL Server",
                },
                new InstallableComponent
                {
                    Type = ComponentType.Firewall,
                    Name = "Firewall Configuration",
                    Description = "Sets up required firewall rules",
                },
                new InstallableComponent
                {
                    Type = ComponentType.WindowsService,
                    Name = "Windows Services",
                    Description = "Installs and configures Windows services",
                },
            };

            foreach (var component in components)
            {
                SelectedComponents.Add(component);
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
                        Version = "6.0",
                        InstallPath = @"C:\Program Files\dotnet",
                    };
                    break;
                case ComponentType.SQLServerInstall:
                    component.Config = new SQLServerConfig
                    {
                        ServerName = "(local)",
                        DatabaseName = "MyDatabase",
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
                        DisplayName = "My Service",
                        ServicePath = "",
                        Description = "Windows service installed via installer",
                        StartupType = "Auto",
                        ServiceAccount = "LocalSystem",
                    };
                    break;
                // Add other cases as needed
            }
        }

        public object SelectedConfigTab
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

        public ObservableCollection<InstallableComponent> SelectedComponents
        {
            get => _selectedComponents;
            set
            {
                _selectedComponents = value;
                OnPropertyChanged(nameof(SelectedComponents));
            }
        }

        public ObservableCollection<InstallableComponent> ComponentsToInstall
        {
            get => _componentsToInstall;
            set
            {
                _componentsToInstall = value;
                OnPropertyChanged(nameof(ComponentsToInstall));
            }
        }

        public ICommand NextCommand { get; }
        public ICommand PreviousCommand { get; }
        public ICommand StartInstallationCommand { get; }

        private void ExecuteNext(object parameter)
        {
            switch (CurrentStep)
            {
                case WizardStep.Step1_SelectComponents:
                    var selectedComponents = SelectedComponents.Where(c => c.IsSelected).ToList();
                    if (!selectedComponents.Any())
                    {
                        MessageBox.Show(
                            "Please select at least one component to install.",
                            "Selection Required",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning
                        );
                        return;
                    }

                    foreach (var component in selectedComponents)
                    {
                        InitializeComponentConfig(component);
                    }

                    ComponentsToInstall = new ObservableCollection<InstallableComponent>(
                        selectedComponents
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
            foreach (var component in ComponentsToInstall)
            {
                if (!component.ValidateConfiguration())
                {
                    MessageBox.Show(
                        $"Please complete all required configuration fields for {component.Name}",
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

        private Task RunPowerShellScriptAsync(
            string scriptPath,
            string args,
            InstallableComponent component
        )
        {
            return Task.Run(() =>
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments =
                        args ?? $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    Verb =
                        "runas" // This ensures the process runs with admin privileges
                    ,
                };

                using (var process = new Process { StartInfo = startInfo })
                {
                    process.OutputDataReceived += (sender, e) =>
                    {
                        if (!string.IsNullOrEmpty(e.Data))
                        {
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                component.InstallationLogs.Add(e.Data);
                            });
                        }
                    };

                    process.ErrorDataReceived += (sender, e) =>
                    {
                        if (!string.IsNullOrEmpty(e.Data))
                        {
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                component.InstallationLogs.Add($"ERROR: {e.Data}");
                            });
                        }
                    };

                    try
                    {
                        process.Start();
                        process.BeginOutputReadLine();
                        process.BeginErrorReadLine();
                        process.WaitForExit();

                        if (process.ExitCode != 0)
                        {
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                component.InstallationLogs.Add(
                                    $"Process exited with code: {process.ExitCode}"
                                );
                            });
                        }
                    }
                    catch (System.ComponentModel.Win32Exception ex)
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            component.InstallationLogs.Add(
                                $"Failed to start process: {ex.Message}"
                            );
                            MessageBox.Show(
                                "This operation requires administrative privileges.",
                                "Administrator Rights Required",
                                MessageBoxButton.OK,
                                MessageBoxImage.Warning
                            );
                        });
                    }
                }
            });
        }

        private async Task ExecuteEnableIISAsync(InstallableComponent component)
        {
            string scriptPath =
                "D:\\Devteam\\AMIT\\AMG-mIoT-AutoInstaller\\AMG-mIoT-AutoInstaller\\Scripts\\Install-IIS.ps1";
            component.InstallationLogs.Add("Starting IIS installation...");
            var args = $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\"";

            await RunPowerShellScriptAsync(scriptPath, args, component);

            component.InstallationLogs.Add("IIS installation completed.");
        }

        private async Task ExecuteFirewallConfigAsync(InstallableComponent component)
        {
            var config = component.Config as FirewallConfig;
            if (config == null)
                return;

            string scriptPath = Path.Combine(
                "D:\\Devteam\\AMIT\\AMG-mIoT-AutoInstaller\\AMG-mIoT-AutoInstaller\\Scripts",
                "Open-FirewallPorts.ps1"
            );

            var args =
                $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\" "
                + $"-Ports \"{config.Ports}\" "
                + $"-Protocol \"{config.Protocol}\" "
                + $"-RuleName \"{config.RuleName}\" "
                + $"-Description \"{config.Description}\"";

            component.InstallationLogs.Add("Configuring firewall rules...");

            var startInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = args,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                Verb = "runas",
            };

            await RunPowerShellScriptAsync(scriptPath, args, component);

            component.InstallationLogs.Add("Firewall configuration completed.");
        }

        private async Task ExecuteIISDeployAsync(InstallableComponent component)
        {
            var config = component.Config as IISDeployConfig;
            if (config == null)
                return;

            string scriptPath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "Scripts",
                "Deploy-IISSite.ps1"
            );

            var args =
                $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\" "
                + $"-SiteName \"{config.WebsiteName}\" "
                + $"-Port {config.Port} "
                + $"-PhysicalPath \"{config.PhysicalPath}\"";

            component.InstallationLogs.Add("Starting IIS site deployment...");
            await RunPowerShellScriptAsync(scriptPath, args, component);
            component.InstallationLogs.Add("IIS site deployment completed.");
        }

        private async Task ExecuteWindowsServiceInstallAsync(InstallableComponent component)
        {
            var config = component.Config as WindowsServiceConfig;
            if (config == null)
                return;

            string scriptPath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "Scripts",
                "Install-Service.ps1"
            );

            var args =
                $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\" "
                + $"-ServiceName \"{config.ServiceName}\" "
                + $"-ServicePath \"{config.ServicePath}\" "
                + $"-DisplayName \"{config.DisplayName}\" "
                + $"-Description \"{config.Description}\" "
                + $"-StartupType \"{config.StartupType}\" "
                + $"-ServiceAccount \"{config.ServiceAccount}\"";

            if (!string.IsNullOrEmpty(config.ServicePassword))
            {
                args += $" -ServicePassword \"{config.ServicePassword}\"";
            }

            if (!string.IsNullOrEmpty(config.Dependencies))
            {
                args += $" -Dependencies \"{config.Dependencies}\"";
            }

            component.InstallationLogs.Add("Installing Windows Service...");
            await RunPowerShellScriptAsync(scriptPath, args, component);
            component.InstallationLogs.Add("Windows Service installation completed.");
        }

        private async void ExecuteStartInstallation(object parameter)
        {
            try
            {
                foreach (var component in ComponentsToInstall)
                {
                    component.Status = "Installing...";
                    try
                    {
                        switch (component.Type)
                        {
                            case ComponentType.EnableIIS:
                                await ExecuteEnableIISAsync(component);
                                break;
                            case ComponentType.Firewall:
                                await ExecuteFirewallConfigAsync(component);
                                break;
                            case ComponentType.WindowsService:
                                await ExecuteWindowsServiceInstallAsync(component);
                                break;
                            case ComponentType.DeployIIS:
                                await ExecuteIISDeployAsync(component);
                                break;

                            // Add additional cases for other components
                        }
                        component.Status = "Completed";
                    }
                    catch (Exception ex)
                    {
                        component.Status = "Failed";
                        component.InstallationLogs.Add($"ERROR: {ex.Message}");
                    }
                }
            }
            finally
            {
                // Optionally close the window when installation completes
                // installationWindow.Close();
                // Or leave it open for the user to review the log
            }
        }

        private bool CanExecuteStartInstallation(object parameter)
        {
            return CurrentStep == WizardStep.Step3_Summary;
        }
    }
}
