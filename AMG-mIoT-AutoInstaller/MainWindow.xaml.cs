using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace AMG_mIoT_AutoInstaller
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        // Allow dragging the window by clicking anywhere on the title bar.
        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            // Double-click to toggle maximize/restore
            if (e.ChangedButton == MouseButton.Left)
            {
                if (e.ClickCount == 2)
                {
                    ToggleMaximizeRestore();
                }
                else
                {
                    DragMove();
                }
            }
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void MaximizeRestoreButton_Click(object sender, RoutedEventArgs e)
        {
            ToggleMaximizeRestore();
        }

        private void ToggleMaximizeRestore()
        {
            WindowState = (WindowState == WindowState.Normal) ? WindowState.Maximized : WindowState.Normal;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        // Update the clipping geometry so the window always appears rounded.
        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // Create a rounded rectangle geometry matching the current window size.
            MainGrid.Clip = new RectangleGeometry(new Rect(0, 0, ActualWidth, ActualHeight), 10, 10);
        }
    }
}
