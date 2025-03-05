using System;
using System.Windows;
using AMG_mIoT_AutoInstaller.Services;
using AMG_mIoT_AutoInstaller.ViewModels;
using AutoUpdaterDotNET;
using Microsoft.Extensions.DependencyInjection;

namespace AMG_mIoT_AutoInstaller
{
    public partial class App : Application
    {
        private readonly IServiceProvider _serviceProvider;

        public App()
        {
            var services = new ServiceCollection();
            ConfigureServices(services);
            _serviceProvider = services.BuildServiceProvider();
        }

        private void ConfigureServices(IServiceCollection services)
        {
            _ = services.AddSingleton<IInstallationService, InstallationService>();
            _ = services.AddTransient<MainWindowViewModel>();
            _ = services.AddTransient<InstallWizardViewModel>();
        }

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            var mainWindow = new MainWindow
            {
                DataContext = _serviceProvider.GetService<MainWindowViewModel>(),
            };
            mainWindow.Show();
        }
    }
}
