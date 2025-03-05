using System;
using System.IO;
using System.Net;
using System.Reflection;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using AutoUpdaterDotNET;

namespace AMG_mIoT_AutoInstaller
{
    public partial class MainWindow : Window
    {
        internal static Uri BaseUri;

        public MainWindow()
        {
            InitializeComponent();

            // using (WebClient WebClient = new WebClient())
            // {
            //     string text = WebClient.DownloadString(
            //         new Uri(
            //             "https://stgamitpl.blob.core.windows.net/amg-miot-installer/Latest/update.xml?sp=r&st=2025-02-27T12:17:58Z&se=2026-02-28T20:17:58Z&spr=https&sv=2022-11-02&sr=b&sig=Sh8GC3gbKnD2Jqi0M7WwFWEgGWhLyaokBqa7zp87FH4%3D"
            //         )
            //     );

            //     AutoUpdater.ShowSkipButton = false;
            //     AutoUpdater.ShowRemindLaterButton = true;
            //     AutoUpdater.CheckForUpdateEvent += AutoUpdater_CheckForUpdateEvent;

            //     // Uncomment following lines to periodically check for updates.
            //     StartUpdateCheckLoop();
            // }

            AutoUpdater.ShowSkipButton = true;
            AutoUpdater.ShowRemindLaterButton = true;
            AutoUpdater.CheckForUpdateEvent += AutoUpdater_CheckForUpdateEvent;
            AutoUpdater.DownloadPath = AppContext.BaseDirectory;
            AutoUpdater.RunUpdateAsAdmin = true;
            AutoUpdater.ReportErrors = true;
            string startupPath =AppContext.BaseDirectory;
            var currentDirectory = new DirectoryInfo(startupPath);

            if (currentDirectory.Parent != null)
            {
                AutoUpdater.InstallationPath = startupPath;
            }
            // Uncomment following lines to periodically check for updates.
            StartUpdateCheckLoop();
        }

        private void AutoUpdater_CheckForUpdateEvent(UpdateInfoEventArgs args)
        {
            if (args != null)
            {
                if (args.IsUpdateAvailable)
                {
                    Dispatcher.Invoke(() =>
                    {
                        AutoUpdater.ShowUpdateForm(args);
                    });
                }
            }
        }

        private async void StartUpdateCheckLoop()
        {
            while (true)
            {
                AutoUpdater.Start(
                    "https://stgamitpl.blob.core.windows.net/amg-miot-installer/Latest/update.xml?sp=r&st=2025-02-27T12:17:58Z&se=2026-02-28T20:17:58Z&spr=https&sv=2022-11-02&sr=b&sig=Sh8GC3gbKnD2Jqi0M7WwFWEgGWhLyaokBqa7zp87FH4%3D"
                );
                await Task.Delay(TimeSpan.FromMinutes(2));
            }
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
            WindowState =
                (WindowState == WindowState.Normal) ? WindowState.Maximized : WindowState.Normal;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        // Update the clipping geometry so the window always appears rounded.
        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // Create a rounded rectangle geometry matching the current window size.
            MainGrid.Clip = new RectangleGeometry(
                new Rect(0, 0, ActualWidth, ActualHeight),
                10,
                10
            );
        }
    }
}
