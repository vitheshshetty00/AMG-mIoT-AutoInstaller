using System.Windows.Input;

namespace AMG_mIoT_AutoInstaller.Models
{
    public class NavigationItem
    {
        public string Label { get; set; }
        public ICommand Command
        {
            get; set;
        }
    }
}
