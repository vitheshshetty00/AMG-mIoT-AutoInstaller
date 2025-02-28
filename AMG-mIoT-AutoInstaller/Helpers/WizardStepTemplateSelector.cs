// File: Helpers/WizardStepTemplateSelector.cs
using System.Windows;
using System.Windows.Controls;
using AMG_mIoT_AutoInstaller.ViewModels;

namespace AMG_mIoT_AutoInstaller.Helpers
{
    /// <summary>
    /// Selects the appropriate DataTemplate based on the CurrentStep of the InstallWizardViewModel.
    /// </summary>
    public class WizardStepTemplateSelector : DataTemplateSelector
    {
        public DataTemplate Step1Template { get; set; }
        public DataTemplate Step2Template { get; set; }
        public DataTemplate Step3Template { get; set; }

        public override DataTemplate SelectTemplate(object item, DependencyObject container)
        {
            var viewModel = item as InstallWizardViewModel;
            if (viewModel == null)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"Template selector received: {item?.GetType().FullName ?? "null"}"
                );
                return null;
            }

            System.Diagnostics.Debug.WriteLine(
                $"Selecting template for step: {viewModel.CurrentStep}"
            );
            var template = viewModel.CurrentStep switch
            {
                WizardStep.Step1_SelectComponents => Step1Template,
                WizardStep.Step2_ConfigureComponents => Step2Template,
                WizardStep.Step3_Summary => Step3Template,
                _ => null,
            };

            System.Diagnostics.Debug.WriteLine(
                $"Selected template: {template?.GetType().FullName ?? "null"}"
            );
            return template;
        }
    }
}
