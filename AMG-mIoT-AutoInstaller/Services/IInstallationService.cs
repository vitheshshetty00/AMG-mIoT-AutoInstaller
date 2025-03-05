using System.Collections.ObjectModel;
using AMG_mIoT_AutoInstaller.Models;

namespace AMG_mIoT_AutoInstaller.Services
{
    public interface IInstallationService
    {
        void StartInstallation(
            ObservableCollection<InstallableComponent> components,
            ObservableCollection<string> log
        );
    }
}
