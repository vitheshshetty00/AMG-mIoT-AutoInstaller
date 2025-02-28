namespace AMG_mIoT_AutoInstaller.Services
{
    public interface IInstallationService
    {
        Task InstallComponentAsync(string component);
    }
}