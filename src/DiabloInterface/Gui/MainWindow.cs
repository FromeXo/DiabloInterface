namespace Zutatensuppe.DiabloInterface.Gui
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Windows.Forms;

    using Zutatensuppe.D2Reader.Models;
    using Zutatensuppe.DiabloInterface.Business.Plugin;
    using Zutatensuppe.DiabloInterface.Business.Services;
    using Zutatensuppe.DiabloInterface.Business.Settings;
    using Zutatensuppe.DiabloInterface.Core.Logging;
    using Zutatensuppe.DiabloInterface.Gui.Controls;
    using Zutatensuppe.DiabloInterface.Gui.Forms;

    public partial class MainWindow : WsExCompositedForm
    {
        static readonly ILogger Logger = LogServiceLocator.Get(MethodBase.GetCurrentMethod().DeclaringType);

        private readonly ISettingsService settingsService;
        private readonly IGameService gameService;
        private readonly List<IPlugin> plugins;

        Form debugWindow;
        AbstractLayout currentLayout;

        public MainWindow(
            ISettingsService settingsService,
            IGameService gameService,
            List<IPlugin> plugins
        ) {
            Logger.Info("Creating main window.");
            this.plugins = plugins;

            this.settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            this.gameService = gameService ?? throw new ArgumentNullException(nameof(gameService));
            
            RegisterServiceEventHandlers();
            InitializeComponent();
            PopulateSetingsFileListContextMenu(settingsService.SettingsFileCollection);
            SetTitleWithApplicationVersion();
            ApplySettings(settingsService.CurrentSettings);
        }

        void RegisterServiceEventHandlers()
        {
            settingsService.SettingsChanged += SettingsServiceOnSettingsChanged;
            settingsService.SettingsCollectionChanged += SettingsServiceOnSettingsCollectionChanged;

            gameService.CharacterCreated += GameService_CharacterCreated;
            gameService.DataRead += GameService_DataRead;
        }

        private void GameService_DataRead(object sender, D2Reader.DataReadEventArgs e)
        {
            foreach (IPlugin p in plugins)
                p.OnDataRead(e);
        }

        private void GameService_CharacterCreated(object sender, D2Reader.CharacterCreatedEventArgs e)
        {
            foreach (IPlugin p in plugins)
                p.OnCharacterCreated(e);
        }

        void SettingsServiceOnSettingsChanged(object sender, ApplicationSettingsEventArgs e)
        {
            if (InvokeRequired)
            {
                Invoke((Action)(() => SettingsServiceOnSettingsChanged(sender, e)));
                return;
            }

            ApplySettings(e.Settings);
        }

        void SettingsServiceOnSettingsCollectionChanged(object sender, SettingsCollectionEventArgs e)
        {
            if (InvokeRequired)
            {
                Invoke((Action)(() => SettingsServiceOnSettingsCollectionChanged(sender, e)));
                return;
            }

            PopulateSetingsFileListContextMenu(e.Collection);
        }

        void ApplySettings(ApplicationSettings settings)
        {
            ApplyLayoutChanges(settings);
        }

        void ApplyLayoutChanges(ApplicationSettings settings)
        {
            var isHorizontal = currentLayout is HorizontalLayout;
            if (isHorizontal != settings.DisplayLayoutHorizontal || currentLayout == null)
            {
                UpdateLayoutView(settings);
            }
        }

        void UpdateLayoutView(ApplicationSettings settings)
        {
            var nextLayout = CreateLayout(settings.DisplayLayoutHorizontal);
            if (currentLayout != null)
            {
                Controls.Remove(currentLayout);
                currentLayout.Dispose();
                currentLayout = null;
            }
            Controls.Add(nextLayout);
            currentLayout = nextLayout;
        }

        AbstractLayout CreateLayout(bool horizontal)
        {
            return horizontal
                ? new HorizontalLayout(settingsService, gameService) as AbstractLayout
                : new VerticalLayout(settingsService, gameService);
        }

        void SetTitleWithApplicationVersion()
        {
            Text = $@"Diablo Interface v{Application.ProductVersion}";
            Update();
        }

        void PopulateSetingsFileListContextMenu(IEnumerable<FileInfo> settingsFileCollection)
        {
            loadConfigMenuItem.DropDownItems.Clear();
            IEnumerable<ToolStripItem> items = settingsFileCollection.Select(CreateSettingsToolStripMenuItem);
            loadConfigMenuItem.DropDownItems.AddRange(items.ToArray());
        }

        ToolStripMenuItem CreateSettingsToolStripMenuItem(FileInfo fileInfo)
        {
            var item = new ToolStripMenuItem
            {
                Text = Path.GetFileNameWithoutExtension(fileInfo.Name),
                Tag = fileInfo.FullName
            };

            item.Click += LoadConfigFile;

            return item;
        }

        void LoadConfigFile(object sender, EventArgs e)
        {
            var fileName = ((ToolStripMenuItem)sender).Tag.ToString();

            // TODO: LoadSettings should throw a custom Exception with information about why this happened.
            if (!settingsService.LoadSettings(fileName))
            {
                Logger.Error($"Failed to load settings from file: {fileName}.");
                MessageBox.Show(
                    $@"Failed to load settings.{Environment.NewLine}See the error log for more details.",
                    @"Settings Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        void ExitMenuItemOnClick(object sender, EventArgs e)
        {
            Close();
        }

        void ResetMenuItemOnClick(object sender, EventArgs e)
        {
            foreach (IPlugin p in plugins)
                p.OnReset();
            currentLayout?.Reset();
        }

        void SettingsMenuItemOnClick(object sender, EventArgs e)
        {
            using (var settingsWindow = new SettingsWindow(settingsService, plugins))
            {
                settingsWindow.ShowDialog();
            }
        }

        void DebugMenuItemOnClick(object sender, EventArgs e)
        {
            if (debugWindow == null || debugWindow.IsDisposed)
            {
                debugWindow = new DebugWindow(settingsService, gameService, plugins);
            }

            debugWindow.Show();
        }

        void DifficultyNormalToolStripMenuItemOnClick(object sender, EventArgs e)
        {
            GameDifficultyClick(sender, GameDifficulty.Normal);
        }

        void NightmareToolStripMenuItemOnClick(object sender, EventArgs e)
        {
            GameDifficultyClick(sender, GameDifficulty.Nightmare);
        }

        void HellToolStripMenuItemOnClick(object sender, EventArgs e)
        {
            GameDifficultyClick(sender, GameDifficulty.Hell);
        }

        void GameDifficultyClick(object sender, GameDifficulty difficulty)
        {
            Logger.Info($"Setting target difficulty to {difficulty}.");

            UncheckDifficultyMenuItems();
            gameService.TargetDifficulty = difficulty;
            ((ToolStripMenuItem)sender).Checked = true;
        }

        void UncheckDifficultyMenuItems()
        {
            foreach (ToolStripMenuItem item in difficultyToolStripMenuItem.DropDownItems)
            {
                item.Checked = false;
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            UnregisterServiceEventHandlers();
            base.OnFormClosing(e);
        }

        void UnregisterServiceEventHandlers()
        {
            settingsService.SettingsChanged -= SettingsServiceOnSettingsChanged;
            settingsService.SettingsCollectionChanged -= SettingsServiceOnSettingsCollectionChanged;
        }
    }
}
