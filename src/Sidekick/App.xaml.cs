using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using Hardcodet.Wpf.TaskbarNotification;
using Microsoft.Extensions.DependencyInjection;
using Sidekick.Business.Leagues;
using Sidekick.Core.Initialization;
using Sidekick.Core.Natives;
using Sidekick.Core.Settings;
using Sidekick.Core.Update;
using Sidekick.Handlers;
using Sidekick.Localization.Initializer;
using Sidekick.Localization.Tray;
using Sidekick.Localization.Update;
using Sidekick.UI.Views;
using Sidekick.Windows.TrayIcon;

// Enables debug specific markup in XAML
// See: https://stackoverflow.com/a/19940157
#if DEBUG
[assembly: XmlnsDefinition("debug-mode", "Namespace")]
#endif

namespace Sidekick
{
    /// <summary>
    /// Entry point for the app
    /// </summary>
    public partial class App : Application
    {
        public static App Instance { get; private set; }

        private const string APPLICATION_PROCESS_GUID = "93c46709-7db2-4334-8aa3-28d473e66041";

        private ServiceProvider serviceProvider;
        private INativeProcess nativeProcess;
        private INativeBrowser nativeBrowser;
        private ILeagueService leagueService;
        private IViewLocator viewLocator;

        private TaskbarIcon trayIcon;

        protected override async void OnStartup(StartupEventArgs e)
        {
            Instance = this;

            base.OnStartup(e);

            ToolTipService.ShowDurationProperty.OverrideMetadata(
            typeof(DependencyObject), new FrameworkPropertyMetadata(int.MaxValue));       // Tooltip opened indefinitly until mouse is moved

            serviceProvider = Sidekick.Startup.InitializeServices(this);
            nativeProcess = serviceProvider.GetRequiredService<INativeProcess>();
            nativeBrowser = serviceProvider.GetRequiredService<INativeBrowser>();
            leagueService = serviceProvider.GetRequiredService<ILeagueService>();

            viewLocator = serviceProvider.GetService<IViewLocator>();
            viewLocator.Open<Windows.SplashScreen>();

            await RunAutoUpdate();

            EnsureSingleInstance();

            leagueService.OnNewLeagues += () =>
            {
                Dispatcher.Invoke(() =>
                {
                    AdonisUI.Controls.MessageBox.Show(InitializerResources.Warn_NewLeagues, buttons: AdonisUI.Controls.MessageBoxButton.OK);
                });
            };

            var initializer = serviceProvider.GetService<IInitializer>();

            initializer.OnProgress += (a) =>
            {
                if (!viewLocator.IsOpened<Windows.SplashScreen>())
                {
                    viewLocator.Open<Windows.SplashScreen>();
                }
            };

            initializer.OnError += (error) =>
            {
                AdonisUI.Controls.MessageBox.Show(InitializerResources.ErrorDuringInit, buttons: AdonisUI.Controls.MessageBoxButton.OK);
                base.Shutdown(1);
            };

            await initializer.Initialize();

            InitTrayIcon(serviceProvider.GetRequiredService<SidekickSettings>());

            serviceProvider.GetRequiredService<EventsHandler>();

        }

        private void InitTrayIcon(SidekickSettings settings)
        {
            trayIcon = (TaskbarIcon)FindResource("TrayIcon");
            trayIcon.DataContext = serviceProvider.GetRequiredService<TrayIconViewModel>();

            trayIcon.ShowBalloonTip(
                    TrayResources.Notification_Title,
                    string.Format(TrayResources.Notification_Message, settings.Key_CheckPrices.ToKeybindString(), settings.Key_CloseWindow.ToKeybindString()),
                    trayIcon.Icon,
                    largeIcon: true);
        }

        private async Task RunAutoUpdate()
        {
            var updateManagerService = serviceProvider.GetService<IUpdateManager>();
            if (await updateManagerService.NewVersionAvailable())
            {
                if (AdonisUI.Controls.MessageBox.Show(UpdateResources.UpdateAvailable, UpdateResources.Title, AdonisUI.Controls.MessageBoxButton.YesNo) == AdonisUI.Controls.MessageBoxResult.Yes)
                {
                    nativeBrowser.Open(new Uri("https://github.com/domialex/Sidekick/releases"));
                    Current.Shutdown();

                    //try
                    //{
                    //    if (await updateManagerService.UpdateSidekick())
                    //    {
                    //        nativeProcess.Mutex = null;
                    //        AdonisUI.Controls.MessageBox.Show(UpdateResources.UpdateCompleted, UpdateResources.Title, AdonisUI.Controls.MessageBoxButton.OK);

                    //        var startInfo = new ProcessStartInfo
                    //        {
                    //            FileName = Path.Combine(updateManagerService.InstallDirectory, "Sidekick.exe"),
                    //            UseShellExecute = false,
                    //        };
                    //        Process.Start(startInfo);
                    //    }
                    //    else
                    //    {
                    //        AdonisUI.Controls.MessageBox.Show(UpdateResources.UpdateFailed, UpdateResources.Title);
                    //        nativeBrowser.Open(new Uri("https://github.com/domialex/Sidekick/releases"));
                    //    }

                    //    Current.Shutdown();
                    //}
                    //catch (Exception)
                    //{
                    //    MessageBox.Show(UpdateResources.UpdateFailed, UpdateResources.Title);
                    //    nativeBrowser.Open(new Uri("https://github.com/domialex/Sidekick/releases"));
                    //}
                }
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            trayIcon?.Dispose();
            // Disposing the service provider also disposes registered all IDisposable services
            serviceProvider?.Dispose();
            base.OnExit(e);
        }

        private void EnsureSingleInstance()
        {
            nativeProcess.Mutex = new Mutex(true, APPLICATION_PROCESS_GUID, out var instanceResult);
            if (!instanceResult)
            {
                Current.Shutdown();
            }
        }
    }
}
