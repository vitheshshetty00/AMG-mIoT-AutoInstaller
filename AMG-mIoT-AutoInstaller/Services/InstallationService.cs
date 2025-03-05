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
            if (component.Config is not WindowsServiceConfig config)
                return;

            config.DisplayName ??= config.ServiceName;
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

            component.InstallationLogs.Add("Installing Windows Service...");
            await RunPowerShellScriptAsync(scriptPath, args, component);
            component.InstallationLogs.Add("Windows Service installation completed.");
        }

        private async Task ExecuteDotNetInstallAsync(InstallableComponent component)
        {
            var config =
                component.Config as DotNetConfig
                ?? throw new ArgumentException("Invalid DotNetConfig");
            string scriptPath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "Scripts",
                "Install-DotNet.ps1"
            );
            var args =
                $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\" -Version \"{config.Version}\" -InstallPath \"{config.InstallPath}\"";
            component.InstallationLogs.Add("Starting .NET runtime installation...");
            await RunPowerShellScriptAsync(scriptPath, args, component);
            component.InstallationLogs.Add(".NET runtime installation completed.");
        }

        private static async Task ExecuteSQLServerInstallAsync(InstallableComponent component)
        {
            var config =
                component.Config as SQLServerConfig
                ?? throw new ArgumentException("Invalid SQLServerConfig");
            string scriptPath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "Scripts",
                "Install-SQLServer.ps1"
            );
            string configFilePath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "ConfigurationFile.INI"
            );
            var args =
                $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\" -ConfigFilePath \"{configFilePath}\" ";
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
