﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using Newtonsoft.Json;
using _1RM.Model.DAO;
using _1RM.Utils;
using Shawn.Utils;
using VariableKeywordMatcher.Provider.DirectMatch;

namespace _1RM.Service
{
    public class EngagementSettings
    {
        public DateTime InstallTime = DateTime.Today;
        public bool DoNotShowAgain = false;
        public string DoNotShowAgainVersionString = "";
        [JsonIgnore]
        public VersionHelper.Version DoNotShowAgainVersion => VersionHelper.Version.FromString(DoNotShowAgainVersionString);
        public DateTime LastRequestRatingsTime = DateTime.MinValue;
        public int ConnectCount = 0;
    }
    public class GeneralConfig
    {
        #region General
        public string CurrentLanguageCode = "en-us";
        public bool AppStartAutomatically = true;
        public bool AppStartMinimized = true;
        public bool ListPageIsCardView = false;
        public bool ConfirmBeforeClosingSession = false;
        #endregion
    }

    public class LauncherConfig
    {
        public bool LauncherEnabled = true;

#if DEBUG
        public HotkeyModifierKeys HotKeyModifiers = HotkeyModifierKeys.Shift;
#else
        public HotkeyModifierKeys HotKeyModifiers = HotkeyModifierKeys.Alt;
#endif

        public Key HotKeyKey = Key.M;

        public bool ShowNoteFieldInLauncher = true;
        public bool ShowNoteFieldInListView = true;
    }

    public class KeywordMatchConfig
    {
        /// <summary>
        /// name of the matchers
        /// </summary>
        public List<string> EnabledMatchers = new List<string>();
    }

    public class DatabaseConfig
    {
        public const DatabaseType DatabaseType = Model.DAO.DatabaseType.Sqlite;

        private string _sqliteDatabasePath = "./" + AppPathHelper.APP_NAME + ".db";
        public string SqliteDatabasePath
        {
            get
            {
                Debug.Assert(string.IsNullOrEmpty(_sqliteDatabasePath) == false);
                return _sqliteDatabasePath;
            }
            set => _sqliteDatabasePath = value.Replace(Environment.CurrentDirectory, ".");
        }
    }

    public class ThemeConfig
    {
        public string ThemeName = "Dark";

        public string PrimaryMidColor = "#323233";
        public string PrimaryLightColor = "#474748";
        public string PrimaryDarkColor = "#2d2d2d";
        public string PrimaryTextColor = "#cccccc";

        public string AccentMidColor = "#FF007ACC";
        public string AccentLightColor = "#FF32A7F4";
        public string AccentDarkColor = "#FF0061A3";
        public string AccentTextColor = "#FFFFFFFF";

        public string BackgroundColor = "#1e1e1e";
        public string BackgroundTextColor = "#cccccc";
    }

    public class Configuration
    {
        public GeneralConfig General { get; set; } = new GeneralConfig();
        public LauncherConfig Launcher { get; set; } = new LauncherConfig();
        public KeywordMatchConfig KeywordMatch { get; set; } = new KeywordMatchConfig();
        public DatabaseConfig Database { get; set; } = new DatabaseConfig();
        public ThemeConfig Theme { get; set; } = new ThemeConfig();
        public EngagementSettings Engagement { get; set; } = new EngagementSettings();
        public List<string> PinnedTags { get; set; } = new List<string>();
    }

    public class ConfigurationService
    {
        private readonly KeywordMatchService _keywordMatchService = new KeywordMatchService();

        public readonly List<MatchProviderInfo> AvailableMatcherProviders = new List<MatchProviderInfo>();
        private readonly Configuration _cfg;

        public GeneralConfig General => _cfg.General;
        public LauncherConfig Launcher => _cfg.Launcher;
        public KeywordMatchConfig KeywordMatch => _cfg.KeywordMatch;
        public DatabaseConfig Database => _cfg.Database;
        public ThemeConfig Theme => _cfg.Theme;
        public EngagementSettings Engagement => _cfg.Engagement;
        /// <summary>
        /// Tags that show on the tab bar of the main window
        /// </summary>
        public List<string> PinnedTags
        {
            set => _cfg.PinnedTags = value;
            get => _cfg.PinnedTags;
        }


        public ConfigurationService(Configuration cfg, KeywordMatchService keywordMatchService)
        {
            _keywordMatchService = keywordMatchService;
            _cfg = cfg;
            AvailableMatcherProviders = KeywordMatchService.GetMatchProviderInfos() ?? new List<MatchProviderInfo>();

            // init matcher
            if (KeywordMatch.EnabledMatchers.Count > 0)
            {
                foreach (var matcherProvider in AvailableMatcherProviders)
                {
                    matcherProvider.Enabled = false;
                }

                foreach (var enabledName in KeywordMatch.EnabledMatchers)
                {
                    var first = AvailableMatcherProviders.FirstOrDefault(x => x.Name == enabledName);
                    if (first != null)
                        first.Enabled = true;
                }
            }
            AvailableMatcherProviders.First(x => x.Name == DirectMatchProvider.GetName()).Enabled = true;
            AvailableMatcherProviders.First(x => x.Name == DirectMatchProvider.GetName()).IsEditable = false;
            KeywordMatch.EnabledMatchers = AvailableMatcherProviders.Where(x => x.Enabled).Select(x => x.Name).ToList();
            _keywordMatchService.Init(KeywordMatch.EnabledMatchers.ToArray());
            // register matcher change event
            foreach (var info in AvailableMatcherProviders)
            {
                info.PropertyChanged += OnMatchProviderChangedHandler;
            }

#if FOR_MICROSOFT_STORE_ONLY
            SimpleLogHelper.Debug($"SetSelfStartingHelper.SetSelfStartByStartupTask({General.AppStartAutomatically}, \"{AppPathHelper.APP_NAME}\")");
            SetSelfStartingHelper.SetSelfStartByStartupTask(General.AppStartAutomatically, AppPathHelper.APP_NAME);
#else
            SimpleLogHelper.Debug($"SetSelfStartingHelper.SetSelfStartByRegistryKey({General.AppStartAutomatically}, \"{AppPathHelper.APP_NAME}\")");
            SetSelfStartingHelper.SetSelfStartByRegistryKey(General.AppStartAutomatically, AppPathHelper.APP_NAME);
#endif
            Save();
        }

        private void OnMatchProviderChangedHandler(object? sender, PropertyChangedEventArgs args)
        {
            if (args.PropertyName == nameof(MatchProviderInfo.Enabled))
            {
                KeywordMatch.EnabledMatchers = AvailableMatcherProviders.Where(x => x.Enabled).Select(x => x.Name).ToList();
                Save();
                _keywordMatchService.Init(KeywordMatch.EnabledMatchers.ToArray());
            }
        }

        public bool CanSave = true;

        public void Save()
        {
            if (!CanSave) return;
            lock (this)
            {
                if (!CanSave) return;
                CanSave = false;
                var fi = new FileInfo(AppPathHelper.Instance.ProfileJsonPath);
                if (fi?.Directory?.Exists == false)
                    fi.Directory.Create();
                File.WriteAllText(AppPathHelper.Instance.ProfileJsonPath, JsonConvert.SerializeObject(this._cfg, Formatting.Indented), Encoding.UTF8);
                CanSave = true;
            }
#if FOR_MICROSOFT_STORE_ONLY
            SimpleLogHelper.Debug($"SetSelfStartingHelper.SetSelfStartByStartupTask({General.AppStartAutomatically}, \"{AppPathHelper.APP_NAME}\")");
            SetSelfStartingHelper.SetSelfStartByStartupTask(General.AppStartAutomatically, AppPathHelper.APP_NAME);
#else
            SimpleLogHelper.Debug($"SetSelfStartingHelper.SetSelfStartByRegistryKey({General.AppStartAutomatically}, \"{AppPathHelper.APP_NAME}\")");
            SetSelfStartingHelper.SetSelfStartByRegistryKey(General.AppStartAutomatically, AppPathHelper.APP_NAME);
#endif
        }
    }
}
