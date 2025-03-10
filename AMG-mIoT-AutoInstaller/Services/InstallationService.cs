using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using AMG_mIoT_AutoInstaller.Models;

namespace AMG_mIoT_AutoInstaller.Services
{
    public class InstallationService : IInstallationService
    {
        public async void StartInstallation(
            ObservableCollection<InstallableComponent> components,
            ObservableCollection<string> log
        )
        {
            foreach (var component in components)
            {
                component.Status = "Installing...";
                try
                {
                    switch (component.Type)
                    {
                        case ComponentType.EnableIIS:
                            await ExecuteEnableIISAsync(component);
                            break;
                        case ComponentType.DeployIIS:
                            await ExecuteIISDeployAsync(component);
                            break;
                        case ComponentType.DotnetInstall:
                            await ExecuteDotNetInstallAsync(component);
                            break;
                        case ComponentType.SQLServerInstall:
                            await ExecuteSQLServerInstallAsync(component);
                            break;
                        case ComponentType.Firewall:
                            await ExecuteFirewallConfigAsync(component);
                            break;
                        case ComponentType.WindowsService:
                            await ExecuteWindowsServiceInstallAsync(component);
                            break;
                        case ComponentType.RestoreDatabase:
                            await ExecuteDBRestoreAsync(component);
                            break;
                    }
                    component.Status = "Completed";
                    log.Add($"{component.Name} installation completed successfully.");
                }
                catch (Exception ex)
                {
                    component.Status = "Failed";
                    component.InstallationLogs.Add($"ERROR: {ex.Message}");
                    log.Add($"{component.Name} installation failed: {ex.Message}");
                }
            }
        }

        private async Task ExecuteDBRestoreAsync(InstallableComponent component)
        {
            if (component.Config is not DBRestoreConfig config)
                return;

            string scriptPath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "Scripts",
                "Restore-SqlDatabase.ps1"
            );

            var args =
                $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\" "
                + $"-ServerInstance \"{config.ServerInstance}\" "
                + $"-Username \"{config.Username}\" "
                + $"-Password \"{config.Password}\" "
                + $"-BackupFilePath \"{config.BackupFilePath}\" "
                + $"-DatabaseName \"{config.DatabaseName}\"";

            component.InstallationLogs.Add("Starting database restore...");
            await RunPowerShellScriptAsync(scriptPath, args, component);
            component.InstallationLogs.Add("Database restore completed.");
        }

        private static async Task ExecuteEnableIISAsync(InstallableComponent component)
        {
            string scriptPath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "Scripts",
                "Install-IIS.ps1"
            );
            component.InstallationLogs.Add("Starting IIS installation...");
            var args = $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\"";

            await RunPowerShellScriptAsync(scriptPath, args, component);

            component.InstallationLogs.Add("IIS installation completed.");
        }

        private static async Task ExecuteFirewallConfigAsync(InstallableComponent component)
        {
            if (component.Config is not FirewallConfig config)
                return;

            string scriptPath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "Scripts",
                "Open-FirewallPorts.ps1"
            );

            var args =
                $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\" "
                + $"-Ports \"{config.Ports}\" "
                + $"-Protocol \"{config.Protocol}\" "
                + $"-RuleName \"{config.RuleName}\" "
                + $"-Description \"{config.Description}\"";

            component.InstallationLogs.Add("Configuring firewall rules...");

            await RunPowerShellScriptAsync(scriptPath, args, component);

            component.InstallationLogs.Add("Firewall configuration completed.");
        }

        private static async Task ExecuteIISDeployAsync(InstallableComponent component)
        {
            if (component.Config is not IISDeployConfig config)
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

        private static async Task ExecuteWindowsServiceInstallAsync(InstallableComponent component)
        {
            if (component.Config is not WindowsServicesConfig servicesConfig)
                return;

            var allSelectedServices = servicesConfig
                .PredefinedServices.Where(s => s.IsSelected)
                .Concat(servicesConfig.CustomServices);

            string scriptPath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "Scripts",
                "Install-Service.ps1"
            );

            foreach (var config in allSelectedServices)
            {
                config.DisplayName ??= config.ServiceName;

                var args =
                    $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\" "
                    + $"-ServiceName \"{config.ServiceName}\" "
                    + $"-ServicePath \"{config.ServicePath}\" "
                    + $"-DisplayName \"{config.DisplayName}\" "
                    + $"-Description \"{config.Description}\" "
                    + $"-StartupType \"{config.StartupType}\" "
                    + $"-ServiceAccount \"{config.ServiceAccount}\"";

                component.InstallationLogs.Add(
                    $"Installing Windows Service: {config.ServiceName}..."
                );
                await RunPowerShellScriptAsync(scriptPath, args, component);
                component.InstallationLogs.Add(
                    $"Windows Service '{config.ServiceName}' installation completed."
                );
            }
        }

        private async Task ExecuteDotNetInstallAsync(InstallableComponent component)
        {
            var config =
                component.Config as DotNetConfig
                ?? throw new ArgumentException("Invalid DotNetConfig");

            // Define the script path for the .NET installation script.
            string scriptPath = Path.Combine(
                AppContext.BaseDirectory,
                "Scripts",
                "Install-DotNetHosting.ps1"
            );

            // Ensure the script file exists.
            if (!File.Exists(scriptPath))
            {
                component.InstallationLogs.Add(
                    $"Error: .NET installation script not found at {scriptPath}."
                );
                return;
            }

            // Loop over each selected .NET version option.
            foreach (var option in config.AvailableVersions.Where(v => v.IsSelected))
            {
                // Build the command-line arguments to pass to the PowerShell script.
                string args =
                    $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\" -dotNetVersion \"{option.Version}\"";
                if (config.Force)
                {
                    args += " -Force";
                }

                component.InstallationLogs.Add(
                    $"Starting .NET {option.Version} Hosting Bundle installation..."
                );
                await RunPowerShellScriptAsync(scriptPath, args, component);
                component.InstallationLogs.Add(
                    $".NET {option.Version} Hosting Bundle installation completed."
                );
            }
        }

        private static async Task ExecuteSQLServerInstallAsync(InstallableComponent component)
        {
            // Get the SQLServerConfig from the component
            var config =
                component.Config as SQLServerConfig
                ?? throw new ArgumentException("Invalid SQLServerConfig");

            // Build the configuration file content based on user inputs
            string configContent =
                $@"[OPTIONS]
ACTION=""Install""
ENU=""True""
ROLE=""AllFeatures_WithDefaults""
QUIET=""True""
UpdateEnabled=""True""
FEATURES=SQLENGINE
INDICATEPROGRESS=""True""

; Instance Identification
INSTANCENAME=""{config.InstanceName}""
INSTANCEID=""{config.InstanceId}""

; Installation Directories
INSTALLSHAREDDIR=""{config.InstallSharedDir}""
INSTALLSHAREDWOWDIR=""{config.InstallSharedWowDir}""
INSTANCEDIR=""{config.InstanceDir}""
INSTALLSQLDATADIR=""{config.SqlUserDbDir}""

; Database Directories
SQLUSERDBDIR=""C:\\Program Files\\Microsoft SQL Server\\MSSQL16.{config.InstanceId}\\MSSQL\\DATA""
SQLUSERDBLOGDIR=""C:\\Program Files\\Microsoft SQL Server\\MSSQL16.{config.InstanceId}\\MSSQL\\DATA""
SQLTEMPDBDIR=""C:\\Program Files\\Microsoft SQL Server\\MSSQL16.{config.InstanceId}\\MSSQL\\DATA""
SQLTEMPDBLOGDIR=""C:\\Program Files\\Microsoft SQL Server\\MSSQL16.{config.InstanceId}\\MSSQL\\DATA""

; Security Settings
ADDCURRENTUSERASSQLADMIN=""True""
ADDCURRENTUSERASSQLADMIN=""True""
SECURITYMODE=""SQL""
SAPWD=""{config.SaPassword}""

; Networking and Service Startup Options
TCPENABLED=""1""
NPENABLED=""0""
BROWSERSVCSTARTUPTYPE=""Automatic""
FILESTREAMLEVEL=""3""
FILESTREAMSHARENAME=""{config.InstanceId}""";

            // Ensure the Scripts directory exists before writing the file
            string scriptsDirectory = Path.Combine(AppContext.BaseDirectory, "Scripts");
            if (!Directory.Exists(scriptsDirectory))
            {
                Directory.CreateDirectory(scriptsDirectory);
            }

            // Save the generated configuration file.
            // File.WriteAllText replaces the file if it exists or creates a new file if it doesn't.
            string configFilePath = Path.Combine(scriptsDirectory, "ConfigurationFile.ini");
            File.WriteAllText(configFilePath, configContent);

            // Set the paths for the PowerShell script and the setup files directory
            string scriptPath = Path.Combine(scriptsDirectory, "Install-SQL.ps1");
            string setupFilesDirectory = Path.Combine(
                AppContext.BaseDirectory,
                "Setup Files",
                "SQLSERVER"
            );

            // Validate that the PowerShell script exists
            if (!File.Exists(scriptPath))
            {
                component.InstallationLogs.Add(
                    $"Error: SQL Server installation script not found at {scriptPath}."
                );
                return;
            }

            // Validate that the setup files directory exists
            if (!Directory.Exists(setupFilesDirectory))
            {
                component.InstallationLogs.Add(
                    $"Error: SQL Server setup files directory not found at {setupFilesDirectory}."
                );
                return;
            }

            // Build the command line arguments to pass the parameters to the script
            var args =
                $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\" "
                + $"-ConfigFile \"{configFilePath}\" "
                + $"-SetupFilesDirectory \"{setupFilesDirectory}\"";

            component.InstallationLogs.Add("Starting SQL Server installation...");
            await RunPowerShellScriptAsync(scriptPath, args, component);
            component.InstallationLogs.Add("SQL Server installation completed.");
        }

        private static async Task RunPowerShellScriptAsync(
            string scriptPath,
            string args,
            InstallableComponent component
        )
        {
            await Task.Run(() =>
            {
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

                using var process = new Process { StartInfo = startInfo };
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
                        component.InstallationLogs.Add($"Failed to start process: {ex.Message}");
                        MessageBox.Show(
                            "This operation requires administrative privileges.",
                            "Administrator Rights Required",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning
                        );
                    });
                }
            });
        }
    }
}
