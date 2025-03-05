// File: ViewModels/MainWindowViewModel.cs
using System.Windows.Input;
using AMG_mIoT_AutoInstaller.Commands;
using AMG_mIoT_AutoInstaller.Views;

namespace AMG_mIoT_AutoInstaller.ViewModels
{
    /// <summary>
    /// Enum representing different sections of the application.
    /// </summary>
    public enum ApplicationSection
    {
        Install,
        Manage,
        Update,
        Settings,
    }

    /// <summary>
    /// ViewModel for the main window, managing application sections and current view.
    /// </summary>
    public class MainWindowViewModel : BaseViewModel
    {
        private ApplicationSection _selectedSection;
        private object _currentView;

        /// <summary>
        /// Initializes a new instance of the MainWindowViewModel class.
        /// </summary>
        /// <param name="installWizardViewModel">The install wizard view model injected via DI.</param>
        public MainWindowViewModel(InstallWizardViewModel installWizardViewModel)
        {
            // Set the default section to Install.
            SelectedSection = ApplicationSection.Install;
            _installWizardViewModel = installWizardViewModel;

            // Initialize dedicated navigation commands.
            NavigateToInstallCommand = new RelayCommand(_ =>
                SelectedSection = ApplicationSection.Install
            );
            NavigateToManageCommand = new RelayCommand(_ =>
                SelectedSection = ApplicationSection.Manage
            );
            NavigateToUpdateCommand = new RelayCommand(_ =>
                SelectedSection = ApplicationSection.Update
            );
            NavigateToSettingsCommand = new RelayCommand(_ =>
                SelectedSection = ApplicationSection.Settings
            );
           

            // Initialize current view based on default section.
            UpdateCurrentViewModel();
        }

        

        /// <summary>
        /// Gets or sets the currently selected application section.
        /// </summary>


        public ApplicationSection SelectedSection
        {
            get => _selectedSection;
            set
            {
                if (_selectedSection != value)
                {
                    _selectedSection = value;
                    OnPropertyChanged(nameof(SelectedSection));
                    UpdateCurrentViewModel();
                }
            }
        }

        /// <summary>
        /// Gets or sets the current view model to display in the main window.
        /// Renamed to CurrentView to match the binding in MainWindow.xaml.
        /// </summary>
        public object CurrentView
        {
            get => _currentView;
            set => SetProperty(ref _currentView, value, nameof(CurrentView));
        }

        // Dedicated navigation commands.
        public ICommand NavigateToInstallCommand { get; }
        public ICommand NavigateToManageCommand { get; }
        public ICommand NavigateToUpdateCommand { get; }
        public ICommand NavigateToSettingsCommand { get; }

        private readonly InstallWizardViewModel _installWizardViewModel;

        // TODO: Inject and store additional view models for Manage, Update, and Settings.

        /// <summary>
        /// Updates the CurrentView based on the selected section.
        /// </summary>
        private void UpdateCurrentViewModel()
        {
            switch (SelectedSection)
            {
                case ApplicationSection.Install:
                    // Create new WizardContainerView and set its DataContext
                    var wizardContainer = new WizardContainerView
                    {
                        DataContext = _installWizardViewModel,
                    };
                    CurrentView = wizardContainer;
                    break;
                case ApplicationSection.Manage:
                    // CurrentView = _manageViewModel; // To be implemented.
                    CurrentView = "Manage View (Coming Soon)";
                    break;
                case ApplicationSection.Update:
                    // CurrentView = _updateViewModel; // To be implemented.
                    CurrentView = "Update View (Coming Soon)";
                    break;
                case ApplicationSection.Settings:
                    // CurrentView = _settingsViewModel; // To be implemented.
                    CurrentView = "Settings View (Coming Soon)";
                    break;
                default:
                    break;
            }
        }
    }
}
