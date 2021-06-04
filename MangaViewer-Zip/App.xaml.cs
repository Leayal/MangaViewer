using ControlzEx.Theming;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace MangaViewer_Zip
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public static App Item => ((App)Application.Current);

        private readonly List<string> _args;
        public IReadOnlyList<string> Args => this._args;

        public App() : base()
        {
            this.Startup += this.App_Startup;
            this.Exit += this.App_Exit;
            this._args = new List<string>();
        }

        private void App_Startup(object sender, StartupEventArgs e)
        {
            this.SyncTheme();
            this._args.Clear();
            this._args.Capacity = e.Args.Length + 1;
            this._args.AddRange(e.Args);
            SystemEvents.UserPreferenceChanged += this.SystemEvents_UserPreferenceChanged;
        }

        private void App_Exit(object sender, ExitEventArgs e)
        {
            SystemEvents.UserPreferenceChanged -= this.SystemEvents_UserPreferenceChanged;
        }

        private void SystemEvents_UserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
        {
            switch (e.Category)
            {
                case UserPreferenceCategory.Color:
                case UserPreferenceCategory.VisualStyle:
                case UserPreferenceCategory.Window:
                    this.SyncTheme();
                    break;
            }
        }

        public void SyncTheme()
        {
            var themeManager = ThemeManager.Current;
            themeManager.ThemeSyncMode = ThemeSyncMode.SyncAll;
            themeManager.SyncTheme();
        }
    }
}
