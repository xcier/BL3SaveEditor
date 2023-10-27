using Microsoft.Win32;
using System;
using System.Linq;
using System.Windows;
using BL3Tools;
using BL3Tools.GameData;
using AdonisUI;
using System.Windows.Controls;
using System.Windows.Data;
using System.Collections.Generic;
using BL3SaveEditor.Helpers;
using System.Collections.ObjectModel;
using BL3Tools.GameData.Items;
using MessageBox = AdonisUI.Controls.MessageBox;
using MessageBoxResult = AdonisUI.Controls.MessageBoxResult;
using MessageBoxButton = AdonisUI.Controls.MessageBoxButton;
using MessageBoxImage = AdonisUI.Controls.MessageBoxImage;
using System.Windows.Input;
using System.IO.Compression;
using System.IO;
using AutoUpdaterDotNET;
using System.Windows.Navigation;
using System.Diagnostics;
using System.Reflection;
using System.ComponentModel;
using CsvHelper;
using System.Globalization;
using CsvHelper.Configuration;
using OakSave;
using System.Threading.Tasks;

namespace BL3SaveEditor {

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : INotifyPropertyChanged {

        #region Databinding Data
        public static string Version { get; private set; } = Assembly.GetExecutingAssembly().GetName().Version.ToString();

        public static RoutedCommand DuplicateCommand { get; } = new RoutedCommand();
        public static RoutedCommand DeleteCommand { get; } = new RoutedCommand();
        public bool SaveGameLoaded => saveGame != null;

        private Task _lastSearch;

        private string _partsSearch;
        public string PartsSearch
        {
            get => _partsSearch;
            set { _partsSearch = value; UpdateParts(); RaisePropertyChanged(nameof(PartsSearch)); }
        }

        private string _partsSearchIncluded;
        public string PartsSearchIncluded
        {
            get => _partsSearchIncluded;
            set { _partsSearchIncluded = value; SearchConv.Search = value; RaisePropertyChanged(nameof(PartsSearchIncluded)); RaisePropertyChanged(nameof(SelectedSerial)); }
        }

        private string _searchTerm;
        public string SearchTerm
        {
            get => _searchTerm;
            set { _searchTerm = value; UpdateSearchedParts(); RaisePropertyChanged(nameof(SearchTerm)); }
        }

        private void UpdateSearchedParts()
        {
            if(_lastSearch == null)
            {
                _lastSearch = Task.Run(async () =>
                {
                    await Task.Delay(60);
                    var items = SlotItems;
                    await Dispatcher.BeginInvoke(new Action(() => {
                        isSearch = true;
                        isExpanded = false;
                        if(LootlemonView.IsVisible)
                        {
                            LootlemonItems = ConvertLootlemon(_lootlemonSerialItems, _searchTerm);
                        }
                        else
                        {
                            UpdateSearch(items);
                        }
                        _lastSearch = null;
                        isExpanded = true;
                        RaisePropertyChanged(nameof(isExpanded));
                    }));
                });
            }
        }

        public static Dictionary<string, ItemInfo> ItemsInfo { get; private set; }

        ListCollectionView _lootlemonItems;
        public ListCollectionView LootlemonItems
        {
            get => _lootlemonItems;
            set { _lootlemonItems = value; RaisePropertyChanged(nameof(LootlemonItems)); }
        }

        public int maximumXP { get; } = PlayerXP._XPMaximumLevel;
        public int minimumXP { get; } = PlayerXP._XPMinimumLevel;
        public int maximumMayhemLevel { get; } = MayhemLevel.MaximumLevel;
        public bool bSaveLoaded { get; set; } = false;
        public bool showDebugMaps { get; set; } = false;
        public bool IsReorder { get; set; }

        private bool _ForceLegitParts = true;
        public bool ForceLegitParts { 
            get { return _ForceLegitParts; }
            set {
                _ForceLegitParts = value;
                RefreshBackpackView();
                ValidParts.Refresh();
                ValidGenerics.Refresh();
            }
        }

        public ListCollectionView ValidPlayerClasses { 
            get {
                return new ListCollectionView(BL3Save.ValidClasses.Keys.ToList());
            }
        }
        public ListCollectionView ValidPlayerHeads {
            get {
                // Hasn't loaded a save yet
                if (saveGame == null) return new ListCollectionView(new List<string>() { "" });
                
                string characterClassPath = saveGame.Character.PlayerClassData.PlayerClassPath;
                var kvp = BL3Save.ValidClasses.Where(x => x.Value.PlayerClassPath == characterClassPath);
                
                // Unknown character?
                if(!kvp.Any()) return new ListCollectionView(new List<string>() { "" });
                string characterName = kvp.First().Key;

                var headAssetPaths = DataPathTranslations.HeadNamesDictionary[characterName];
                List<string> headNames = new List<string>();
                foreach(string assetPath in headAssetPaths) {
                    string headName = DataPathTranslations.headAssetPaths[assetPath];
                    headNames.Add(headName);
                }

                return new ListCollectionView(headNames);
            }
        }
        public ListCollectionView ValidPlayerSkins {
            get {
                // Hasn't loaded a save yet
                if (saveGame == null) return new ListCollectionView(new List<string>() { "" });

                string characterClassPath = saveGame.Character.PlayerClassData.PlayerClassPath;
                var kvp = BL3Save.ValidClasses.Where(x => x.Value.PlayerClassPath == characterClassPath);

                // Unknown character?
                if (!kvp.Any()) return new ListCollectionView(new List<string>() { "" });
                string characterName = kvp.First().Key;

                var skinAssetPaths = DataPathTranslations.SkinNamesDictionary[characterName];
                List<string> skinNames = new List<string>();
                foreach (string assetPath in skinAssetPaths) {
                    string headName = DataPathTranslations.skinAssetPaths[assetPath];
                    skinNames.Add(headName);
                }

                return new ListCollectionView(skinNames);
            }
        }
        private IEnumerable<StringSerialPair> cachedItems;
        public ListCollectionView SlotItems {
            get {
                var px = FilteredSlots;
                if(px.Count == 0 && (cachedItems == null || string.IsNullOrEmpty(SearchTerm)))
                {
                    // Hasn't loaded a save/profile yet
                    if (saveGame == null && profile == null) return null;
                    List<int> usedIndexes = new List<int>();
                    List<Borderlands3Serial> itemsToSearch = null;

                    if (saveGame != null) {
                        var equippedItems = saveGame.Character.EquippedInventoryLists;
                        foreach (var item in equippedItems) {
                            if (!item.Enabled || item.InventoryListIndex < 0 || item.InventoryListIndex > saveGame.InventoryItems.Count - 1) continue;
                            usedIndexes.Add(item.InventoryListIndex);

                            var itemSerial = saveGame.InventoryItems[item.InventoryListIndex];
                            ItemsInfo.TryGetValue(itemSerial.UserFriendlyName.ToLower(), out var itemInfo);
                            px.Add(new StringSerialPair("Equipped", itemSerial, itemInfo ?? new ItemInfo()));
                        }
                        itemsToSearch = saveGame.InventoryItems;
                    }
                    else {
                        itemsToSearch = profile.BankItems;
                    }

                    for (int i = 0; i < itemsToSearch.Count; i++) {
                        // Ignore already used (equipped) indexes
                        if (usedIndexes.Contains(i)) continue;
                        var serial = itemsToSearch[i];

                        // Split the items out into groups, assume weapons because they're the most numerous and different
                        string itemType = "Weapon";

                        if (serial.InventoryKey == null) itemType = "Other";
                        else if (serial.InventoryKey.Contains("_ClassMod")) itemType = "Class Mods";
                        else if (serial.InventoryKey.Contains("_Artifact")) itemType = "Artifacts";
                        else if (serial.InventoryKey.Contains("_Shield")) itemType = "Shields";
                        else if (serial.InventoryKey.Contains("_Customization")) itemType = "Customizations";
                        else if (serial.InventoryKey.Contains("_GrenadeMod_")) itemType = "Grenades";

                        ItemsInfo.TryGetValue(serial.UserFriendlyName.ToLower(), out var itemInfo);
                        px.Add(new StringSerialPair(itemType, serial, itemInfo ?? new ItemInfo()));
                    }
                    cachedItems = px;
                }
                ListCollectionView vx = new ListCollectionView(px);
                vx.GroupDescriptions.Add(new PropertyGroupDescription("Val1"));
                return vx;
            }
        }

        public List<StringSerialPair> FilteredSlots
        {
            get {
                if (!string.IsNullOrWhiteSpace(SearchTerm) && cachedItems != null)
                {
                    var searchTerm = SearchTerm.ToLowerInvariant();
                    return cachedItems.Where(p => p.ToString().ToLowerInvariant().Contains(searchTerm)).ToList();
                }
                return new List<StringSerialPair>();
                
            }
        }
        public ListCollectionView ValidBalances {
            get {
                if (SelectedSerial == null) return null;

                string inventoryKey = SelectedSerial.InventoryKey;
                var balances = InventoryKeyDB.KeyDictionary.Where(x => x.Value.Equals(inventoryKey) && !x.Key.Contains("partset")).Select(x => InventorySerialDatabase.GetShortNameFromBalance(x.Key)).Where(x => !string.IsNullOrEmpty(x)).ToList();

                return new ListCollectionView(balances);
            }
        }
        public string SelectedBalance {
            get {
                if (SelectedSerial == null) return null;                
                return InventorySerialDatabase.GetShortNameFromBalance(SelectedSerial.Balance);
            }
            set {
                if (SelectedSerial == null) return;
                SelectedSerial.Balance = InventorySerialDatabase.GetBalanceFromShortName(value);
            }
        }
        public ListCollectionView ValidManufacturers {
            get {
                return new ListCollectionView(InventorySerialDatabase.GetManufacturers());
            }
        }
        public string SelectedManufacturer {
            get {
                if (SelectedSerial == null) return null;
                string Manufacturer = SelectedSerial.Manufacturer;
                
                List<string> shortNames = InventorySerialDatabase.GetManufacturers();
                List<string> longNames = InventorySerialDatabase.GetManufacturers(false);
                try {
                    return shortNames[longNames.IndexOf(Manufacturer)];
                }
                catch {
                    return Manufacturer;
                }

            }
            set {
                if (SelectedSerial == null) return;
                
                List<string> shortNames = InventorySerialDatabase.GetManufacturers();
                List<string> longNames = InventorySerialDatabase.GetManufacturers(false);

                SelectedSerial.Manufacturer = longNames[shortNames.IndexOf(value)];
            }
        }
        public ListCollectionView InventoryDatas {
            get {
                return new ListCollectionView(InventorySerialDatabase.GetInventoryDatas());
            }
        }
        public string SelectedInventoryData {
            get {
                return SelectedSerial?.InventoryData.Split('.').LastOrDefault();
            }
            set {
                if (SelectedSerial == null) return;

                List<string> shortNames = InventorySerialDatabase.GetInventoryDatas();
                List<string> longNames = InventorySerialDatabase.GetInventoryDatas(false);
                SelectedSerial.InventoryData = longNames[shortNames.IndexOf(value)];
            }
        }
        public Borderlands3Serial SelectedSerial { get; set; }

        public ListCollectionView ValidParts {
            get {
                if (SelectedSerial == null) return null;
                List<string> validParts = new List<string>();

                if(!ForceLegitParts) validParts = InventorySerialDatabase.GetPartsForInvKey(SelectedSerial.InventoryKey);
                else {
                    validParts = InventorySerialDatabase.GetValidPartsForParts(SelectedSerial.InventoryKey, SelectedSerial.Parts);
                }
                var validConstructedParts = validParts.Select(x => {
                    var part = x.Split('.').Last();
                    if(ItemsInfo.TryGetValue(part.ToLower(), out var itemInfo))
                    {
                        return new ItemInfo { Part = part, Effects = itemInfo.Effects, Negatives = itemInfo.Negatives, Positives = itemInfo.Positives };
                    }
                    else
                    {
                        return new ItemInfo { Part = part, Effects = null, Positives = null, Negatives = null };
                    }
                }).ToList();
                validConstructedParts.OrderBy(p => p.Part);
                if(!string.IsNullOrWhiteSpace(PartsSearch))
                {
                    var partsSearchTerm = PartsSearch.ToLowerInvariant();
                    validConstructedParts = validConstructedParts.Where(p => p.Part.ToLowerInvariant().Contains(partsSearchTerm) || (p.SubEffect != null && p.SubEffect.ToLowerInvariant().Contains(partsSearchTerm))).ToList();
                }
                return new ListCollectionView(validConstructedParts);
            }
        }

        public ListCollectionView ValidGenerics {
            get {
                if (SelectedSerial == null) return null;
                List<string> validParts = new List<string>();

                // In this case, balances are what actually restrict the items from their anointments.


                // Currently no generic parts actually have any excluders/dependencies
                // but in the future they might so let's still enforce legit parts on them
                if (!ForceLegitParts) validParts = InventorySerialDatabase.GetPartsForInvKey("InventoryGenericPartData");
                else {
                    validParts = InventorySerialDatabase.GetValidPartsForParts("InventoryGenericPartData", SelectedSerial.GenericParts);
                    var vx = InventorySerialDatabase.GetValidPartsForParts("InventoryGenericPartData", SelectedSerial.Parts);
                    var validGenerics = InventorySerialDatabase.GetValidGenericsForBalance(SelectedSerial.Balance);

                    var itemType = InventoryKeyDB.ItemTypeToKey.LastOrDefault(x => x.Value.Contains(SelectedSerial.InventoryKey)).Key;
                    bool bHasMayhem = (itemType == null);
                    if (itemType != null) {
                        // Only certain item types can be anointed...
                        bHasMayhem = (itemType != "Grenades" && itemType != "Shields" && itemType != "Class Mods" && itemType != "Artifacts" && itemType != "Eridian Fabricator" && itemType != "Customizations");
                    }

                    // Filter out all parts that can't be contained from the balance
                    validParts = validParts.Where(x => validGenerics.Contains(x) || (bHasMayhem && x.Contains("WeaponMayhemLevel_"))).ToList();

                    // Filter out all the other invalid parts that can't be contained based off of the non-generic parts
                    validParts = validParts.Where(x => vx.Contains(x) || (bHasMayhem && x.Contains("WeaponMayhemLevel_"))).ToList();

                }

                var validConstructedParts = validParts.Select(x => {
                    var part = x.Split('.').Last();
                    if (ItemsInfo.TryGetValue(part.ToLower(), out var itemInfo))
                    {
                        return new ItemInfo { Part = part, Effects = itemInfo.Effects, Negatives = itemInfo.Negatives, Positives = itemInfo.Positives };
                    }
                    else
                    {
                        return new ItemInfo { Part = part, Effects = null, Positives = null, Negatives = null };
                    }
                }).ToList();
                validConstructedParts.OrderBy(p => p.Part);
                if (!string.IsNullOrWhiteSpace(PartsSearch))
                {
                    var partsSearchTerm = PartsSearch.ToLowerInvariant();
                    validConstructedParts = validConstructedParts.Where(p => p.Part.ToLowerInvariant().Contains(partsSearchTerm)).ToList();
                }
                return new ListCollectionView(validConstructedParts);
            }
        }

        public int MaximumBankSDUs { get { return SDU.MaximumBankSDUs;  } }
        public int MaximumLostLootSDUs { get { return SDU.MaximumLostLoot;  } }
        #endregion

        private static string UpdateURL = "https://raw.githubusercontent.com/FromDarkHell/BL3SaveEditor/main/BL3SaveEditor/AutoUpdater.xml";

        private static Debug.DebugConsole dbgConsole;
        private bool bLaunched = false;

        public SearchVisibilityConverter SearchConv { get; set; }

        private bool disableEvents;
        private bool isSearch;
        private List<Borderlands3Serial> _lootlemonSerialItems;
        private StringSerialPair _itemToImport;

        public event PropertyChangedEventHandler PropertyChanged;
        private void RaisePropertyChanged(string propertyName)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        /// <summary>
        /// The current profile object; will be null if we haven't loaded a profile
        /// </summary>
        public BL3Profile profile { get; set; } = null;

        /// <summary>
        /// The current save game object; will be null if we loaded a profile instead of a save game
        /// </summary>
        public BL3Save saveGame { get; set; } = null;
        public bool isExpanded { get; set; } = true;

        public MainWindow() {
            this.profile = null;
            this.saveGame = null;

            using (var reader = new StreamReader("INVENTORY_PARTS_INFO_ALL.csv"))
            {
                using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
                {
                    csv.Context.TypeConverterOptionsCache.GetOptions<string>().NullValues.Add("");
                    ItemsInfo = csv.GetRecords<ItemInfo>()
                        .GroupBy(r => r.Part)
                        .Select(r => r.First())
                        .ToDictionary(r => r.Part.ToLower());
                }
            }
            using (var reader = new StreamReader("LOOTLEMON_BL3_ITEMS.csv"))
            {
                using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
                {
                    csv.Context.TypeConverterOptionsCache.GetOptions<string>().NullValues.Add("");
                    _lootlemonSerialItems = csv.GetRecords<LootlemonItem>().Select(item => Borderlands3Serial.DecryptSerial(item.Code)).ToList();
                    LootlemonItems = ConvertLootlemon(_lootlemonSerialItems, null);
                }
            }
            InitializeComponent();
            SearchConv = (SearchVisibilityConverter)FindResource("SearchConverter");
            DataContext = this;

            // Restore the dark mode state from last run
            bLaunched = true;
            CheckBox darkBox = (CheckBox)FindName("DarkModeBox");
            darkBox.IsChecked = Properties.Settings.Default.bDarkModeEnabled;
            DarkModeBox_Checked(darkBox, null);

            dbgConsole = new Debug.DebugConsole();

            ((TabControl)FindName("TabCntrl")).SelectedIndex = ((TabControl)FindName("TabCntrl")).Items.Count - 1;
            AutoUpdater.CheckForUpdateEvent += AutoUpdaterOnCheckForUpdateEvent;
            AutoUpdater.RunUpdateAsAdmin = true;
#if !DEBUG
            AutoUpdater.Start(UpdateURL);
#endif
        }

        #region Toolbar Interaction
        private void NewSaveBtn_Click(object sender, RoutedEventArgs e) {
            Console.WriteLine("New save made...");
        }

        private void OpenSaveBtn_Click(object sender, RoutedEventArgs e) {

            Dictionary<Platform, string> PlatformFilters = new Dictionary<Platform, string>() {
                { Platform.PC, "PC BL3 Save/Profile (*.sav)|*.sav|JSON Save (*.json)|*.json" },
                { Platform.PS4, "PS4 BL3 Save/Profile (*.*)|*.*" }
            };

            OpenFileDialog fileDialog = new OpenFileDialog {
                Title = "Select BL3 Save/Profile",
                Filter = string.Join("|", PlatformFilters.Values),
                InitialDirectory = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "My Games", "Borderlands 3", "Saved", "SaveGames"),
            };

            if (fileDialog.ShowDialog() == true) {
                Platform platform = fileDialog.FilterIndex <= 2 ? Platform.PC : Platform.PS4;
                OpenSave(fileDialog.FileName, platform);
            }
        }

        private void OpenSave(string filePath, Platform platform = Platform.PC) {
            try {
                // Reload the save just for safety, this way we're getting the "saved" version on a save...
                object saveObj = BL3Tools.BL3Tools.LoadFileFromDisk(filePath, platform);
                Console.WriteLine($"Reading a save of type: {saveObj.GetType()}");

                if (saveObj.GetType() == typeof(BL3Profile)) {
                    profile = (BL3Profile)saveObj;
                    saveGame = null;
                    bSaveLoaded = false;
                    // Profile tab
                    TabCntrl.SelectedIndex = 5;

                }
                else {
                    saveGame = (BL3Save)saveObj;
                    profile = null;
                    bSaveLoaded = true;
                    // General tab
                    TabCntrl.SelectedIndex = 0;
                }

            ((TabItem)FindName("RawTabItem")).IsEnabled = true;
                ((TabItem)FindName("InventoryTabItem")).IsEnabled = true;

                ((Button)FindName("SaveSaveBtn")).IsEnabled = true;
                ((Button)FindName("SaveAsSaveBtn")).IsEnabled = true;

                // Refresh the bindings on the GUI
                DataContext = null;
                DataContext = this;

                BackpackListView.ItemsSource = null;
                BackpackListView.ItemsSource = SlotItems;
                RefreshBackpackView();
            }
            catch(Exception ex) {
                Console.WriteLine("Failed to load save ({0}) :: {1}", filePath, ex.Message);
                Console.WriteLine(ex.StackTrace);

                MessageBox.Show($"Error parsing save: {ex.Message}", "Save Parse Exception", AdonisUI.Controls.MessageBoxButton.OK, AdonisUI.Controls.MessageBoxImage.Error);
            }
        }

        private void SaveOpenedFile() {
            if (saveGame != null) BL3Tools.BL3Tools.WriteFileToDisk(saveGame);
            else if (profile != null) {
                BL3Tools.BL3Tools.WriteFileToDisk(profile);
                DirectoryInfo saveFiles = new DirectoryInfo(Path.GetDirectoryName(profile.filePath));
                InjectGuardianRank(saveFiles.EnumerateFiles("*.sav").Select(x => x.FullName).ToArray());
            }

#if DEBUG
            BL3Tools.BL3Tools.Reload = true;
            OpenSave(saveGame == null ? profile.filePath : saveGame.filePath);
            BL3Tools.BL3Tools.Reload = false;
#endif
        }

        private void SaveSaveBtn_Click(object sender, RoutedEventArgs e) {
            Console.WriteLine("Saving save...");
            SaveOpenedFile();
        }

        private void SaveAsSaveBtn_Click(object sender, RoutedEventArgs e)
        {
            Console.WriteLine("Saving save as...");
            SaveFileDialog saveFileDialog = new SaveFileDialog()
            {
                Title = "Save BL3 Save/Profile",
                Filter = "BL3 Save/Profile (*.sav)|*.sav|BL3 JSON Save (*.json)|*.json|BL3 PS4 Save/Profile (*.*)|*.*",
                InitialDirectory = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "My Games", "Borderlands 3", "Saved", "SaveGames")
            };

            // Update the file like this so that way once you do a save as, it still changes the saved-as file instead of the originally opened file.
            if (saveFileDialog.ShowDialog() == true) {
                if (saveGame != null)
                {
                    saveGame.filePath = saveFileDialog.FileName;
                }
                else if (profile != null) 
                {
                    profile.filePath = saveFileDialog.FileName; 
                }
            }

            SaveOpenedFile();
        }

        private void DbgBtn_Click(object sender, RoutedEventArgs e) {
            dbgConsole.Show();
        }

        #endregion

        private void AdonisWindow_Closed(object sender, EventArgs e) {
            Console.WriteLine("Closing program...");

            // Release the console writer on close to avoid memory issues
            dbgConsole.consoleRedirectWriter.Release();

            // Need to set this boolean in order to actually close the program
            dbgConsole.bClose = true;
            dbgConsole.Close();
        }

        #region Theme Toggling
        private void DarkModeBox_Checked(object sender, RoutedEventArgs e) {
            if(bLaunched) {
                bool bChecked = (bool)((CheckBox)sender).IsChecked;
                ResourceLocator.SetColorScheme(Application.Current.Resources, bChecked ? ResourceLocator.DarkColorScheme : ResourceLocator.LightColorScheme);

                // Update the settings now
                Properties.Settings.Default.bDarkModeEnabled = bChecked;
                Properties.Settings.Default.Save();

            }
        }
        #endregion

        #region Interactions

        #region General
        private void RandomizeGUIDBtn_Click(object sender, RoutedEventArgs e) {
            Guid newGUID = Guid.NewGuid();
            GUIDTextBox.Text = newGUID.ToString().Replace("-","").ToUpper();
        }

        private void AdjustSaveLevelsBtn_Click(object sender, RoutedEventArgs e) {
            OpenFileDialog fileDialog = new OpenFileDialog {
                Title = "Select BL3 Saves",
                Filter = "BL3 Save (*.sav)|*.sav",
                InitialDirectory = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "My Games", "Borderlands 3", "Saved", "SaveGames"),
                Multiselect = true
            };

            if (fileDialog.ShowDialog() != true) return;

            int level = 0;
            var msgBox = new Controls.IntegerMessageBox("Enter a level to sync saves to: ", "Level: ", minimumXP, maximumXP, maximumXP);
            msgBox.Owner = this;
            msgBox.ShowDialog();
            if (!msgBox.Succeeded) return;
            level = msgBox.Result;

            foreach(string file in fileDialog.FileNames) {
                try {
                    if (!(BL3Tools.BL3Tools.LoadFileFromDisk(file) is BL3Save save)) {
                        Console.WriteLine("Read in file from \"{0}\"; Incorrect type: {1}");
                        continue;
                    }
                    save.Character.ExperiencePoints = PlayerXP.GetPointsForXPLevel(level);
                    BL3Tools.BL3Tools.WriteFileToDisk(save, false);
                }
                catch(Exception ex) {
                    Console.WriteLine("Failed to adjust level of save: \"{0}\"\n{1}", ex.Message, ex.StackTrace);
                }
            }

        }


        private void BackupAllSavesBtn_Click(object sender, RoutedEventArgs e) {
            // Ask the user for all the saves to backup
            OpenFileDialog fileDialog = new OpenFileDialog {
                Title = "Backup BL3 Saves/Profiles",
                Filter = "BL3 Save/Profile (*.sav)|*.sav",
                InitialDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "My Games", "Borderlands 3", "Saved", "SaveGames"),
                Multiselect = true
            };
            if (fileDialog.ShowDialog() != true) return;

            // Ask the user for a zip output
            SaveFileDialog outDialog = new SaveFileDialog {
                Title = "Backup Outputs",
                Filter = "ZIP file|*.zip",
                InitialDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "My Games", "Borderlands 3", "Saved", "SaveGames"),
                RestoreDirectory = true,
            };
            if (outDialog.ShowDialog() != true) return;

            Mouse.OverrideCursor = Cursors.Wait;
            try {
                // Finally back up all of the saves (using a ZIP because meh)
                using (FileStream ms = new FileStream(outDialog.FileName, FileMode.Create))
                using (ZipArchive archive = new ZipArchive(ms, ZipArchiveMode.Create)) {
                    foreach (string path in fileDialog.FileNames) {
                        string fileName = Path.GetFileName(path);
                        ZipArchiveEntry saveEntry = archive.CreateEntry(fileName, CompressionLevel.Optimal);

                        using (BinaryWriter writer = new BinaryWriter(saveEntry.Open())) {
                            byte[] data = File.ReadAllBytes(path);
                            writer.Write(data);
                        }
                    }
                }

                Console.WriteLine("Backed up all saves: {0} to ZIP: {1}", string.Join(",", fileDialog.FileNames), outDialog.FileName);
            }
            finally {
                // Make sure that in the event of an exception, that the mouse cursor gets restored (:
                Mouse.OverrideCursor = null;
            }
        }

        #endregion

        #region Character
        private void CharacterClass_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            var str = e.AddedItems.OfType<string>().FirstOrDefault();
            if (str == null || str == default) return;
        }
        #endregion

        #region Fast Travel
        private void DbgMapBox_StateChange(object sender, RoutedEventArgs e) {
            VisitedTeleportersGrpBox.DataContext = null;
            VisitedTeleportersGrpBox.DataContext = this;
        }

        private void FastTravelChkBx_StateChanged(object sender, RoutedEventArgs e) {
            if (sender == null || saveGame == null) return;
            CheckBox senderBx = (CheckBox)sender;
            if (senderBx.Content.GetType() != typeof(TextBlock)) return;

            bool bFastTravelEnabled = senderBx.IsChecked == true;
            string fastTravelToChange = ((senderBx.Content as TextBlock).Text);
            string assetPath = DataPathTranslations.FastTravelTranslations.FirstOrDefault(x => x.Value == fastTravelToChange).Key;
            
            Console.WriteLine("Changed state of {0} ({2}) to {1}", fastTravelToChange, bFastTravelEnabled, assetPath);
            int amtOfPlaythroughs = saveGame.Character.ActiveTravelStationsForPlaythroughs.Count - 1;
            int playthroughIndex = SelectedPlaythroughBox.SelectedIndex;

            if (amtOfPlaythroughs < SelectedPlaythroughBox.SelectedIndex) {
                saveGame.Character.ActiveTravelStationsForPlaythroughs.Add(new OakSave.PlaythroughActiveFastTravelSaveData());
            }

            var travelStations = saveGame.Character.ActiveTravelStationsForPlaythroughs[playthroughIndex].ActiveTravelStations;
            if(bFastTravelEnabled) {
                travelStations.Add(new OakSave.ActiveFastTravelSaveData() {
                    ActiveTravelStationName = assetPath,
                    Blacklisted = false
                });
            }
            else {
                travelStations.RemoveAll(x => x.ActiveTravelStationName == assetPath);
            }

            return;
        }

        private void EnableAllTeleportersBtn_Click(object sender, RoutedEventArgs e) {
            foreach (BoolStringPair bsp in TeleportersItmCntrl.Items) {
                ContentPresenter presenter = (ContentPresenter)TeleportersItmCntrl.ItemContainerGenerator.ContainerFromItem(bsp);
                presenter.ApplyTemplate();
                CheckBox chkBox = presenter.ContentTemplate.FindName("FastTravelChkBx", presenter) as CheckBox;
                chkBox.IsChecked = true;
            }
        }

        private void DisableAllTeleportersBtn_Click(object sender, RoutedEventArgs e) {
            foreach (BoolStringPair bsp in TeleportersItmCntrl.Items) {
                ContentPresenter presenter = (ContentPresenter)TeleportersItmCntrl.ItemContainerGenerator.ContainerFromItem(bsp);
                presenter.ApplyTemplate();
                CheckBox chkBox = presenter.ContentTemplate.FindName("FastTravelChkBx", presenter) as CheckBox;
                chkBox.IsChecked = false;
            }
        }

        #endregion

        #region Backpack / Bank
        private void UpdateParts()
        {
            RaisePropertyChanged(nameof(ValidParts));
        }

        private void UpdateIncludedParts()
        {

        }

        private void UpdateSearch(ListCollectionView items)
        {
            BackpackListView.ItemsSource = items;
            //RefreshBackpackView();
        }
        private void RefreshBackpackView() {
            disableEvents = true;
            // Need to change the data context real quick to make the GUI update
            var grid = ((Grid)FindName("SerialContentsGrid"));
            grid.DataContext = null;
            grid.DataContext = this;
            disableEvents = false;
            //var partsLabel = ((Label)FindName("PartsLabel"));
            //partsLabel.DataContext = null;
            //partsLabel.DataContext = this;
            //partsLabel = ((Label)FindName("GenericPartsLabel"));
            //partsLabel.DataContext = null;
            //partsLabel.DataContext = this;

            //var addPartBtn = ((Button)FindName("GenericPartsAddBtn"));
            //addPartBtn.DataContext = null;
            //addPartBtn.DataContext = this;
            //addPartBtn = ((Button)FindName("PartsAddBtn"));
            //addPartBtn.DataContext = null;
            //addPartBtn.DataContext = this;

        }
        private void IntegerUpDown_ValueChanged(object sender, RoutedPropertyChangedEventArgs<object> e) {
            if (e.NewValue == null || e.OldValue == null) return;
            RefreshBackpackView();
        }
        private void BackpackListView_Selected(object sender, EventArgs e) {
            if (BackpackListView.Items.Count <= 1 || BackpackListView.SelectedValue == null) return;
            ListView listView = (sender as ListView);
            StringSerialPair svp = (StringSerialPair)listView.SelectedValue;
            SelectedSerial = svp.Val2;
            
            // Scroll to the selected item (in case of duplication / etc)
            if(!isSearch)
            {
                listView.ScrollIntoView(listView.SelectedItem);
            }
            isSearch = false;

            RefreshBackpackView();
        }
        private void BackpackListView_MouseWheel(object sender, MouseWheelEventArgs e) {
            if (e.Handled) return;

            // This janky bit of logic allows us to scroll on hover over the items of the ListView as well :/
            var listview = (sender as ListView);
            var scrollViewer = listview.FindVisualChildren<ScrollViewer>().First();
            // Multiply the value by 0.7 because just the delta value can be a bit much tbh
            scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset - (e.Delta * 0.7) );

            // Make sure no other elements can handle the events
            e.Handled = true;
        }
        private void NewItemBtn_Click(object sender, RoutedEventArgs e) {
            Controls.ItemBalanceChanger changer = new Controls.ItemBalanceChanger() { Owner = this };
            changer.ShowDialog();

            // The user actually hit the save button and we have data about the item
            if (changer.SelectedInventoryData != null) {
                var serial = Borderlands3Serial.CreateSerialFromBalanceData(changer.SelectedBalance);
                if (serial == null) return;

                serial.InventoryData = changer.SelectedInventoryData;
                // Set a manufacturer so that way the bindings don't lose their mind
                serial.Manufacturer = InventorySerialDatabase.GetManufacturers().FirstOrDefault();

                if (profile == null) saveGame.AddItem(serial);
                else profile.BankItems.Add(serial);

                BackpackListView.ItemsSource = null;
                BackpackListView.ItemsSource = SlotItems;
                RefreshBackpackView();
            }
        }
        private void PasteCodeBtn_Click(object sender, RoutedEventArgs e) {
            string serialCode = Clipboard.GetText();
            Console.WriteLine("Pasting serial code: {0}", serialCode);
            try {
                Borderlands3Serial item = Borderlands3Serial.DecryptSerial(serialCode);
                if (item == null) return;

                if (profile == null) saveGame.AddItem(item);
                else profile.BankItems.Add(item);

                BackpackListView.ItemsSource = null;
                BackpackListView.ItemsSource = SlotItems;
                BackpackListView.Items.Refresh();
                RefreshBackpackView();

                var selectedValue = BackpackListView.Items.Cast<StringSerialPair>().Where(x => ReferenceEquals(x.Val2, item)).LastOrDefault();
                BackpackListView.SelectedValue = selectedValue;
            }
            catch(BL3Tools.BL3Tools.BL3Exceptions.SerialParseException ex) {
                string message = ex.Message;
                Console.WriteLine($"Exception ({message}) parsing serial: {ex.ToString()}");
                if (ex.knowCause)
                    MessageBox.Show($"Error parsing serial: {ex.Message}", "Serial Parse Exception", AdonisUI.Controls.MessageBoxButton.OK, AdonisUI.Controls.MessageBoxImage.Error);
            }
            catch(Exception ex) {
                string message = ex.Message;
                Console.WriteLine($"Exception ({message}) parsing serial: {ex.ToString()}");
                MessageBox.Show($"Error parsing serial: {ex.Message}", "Serial Parse Exception", AdonisUI.Controls.MessageBoxButton.OK, AdonisUI.Controls.MessageBoxImage.Error);
            }
        }
        private void SyncEquippedBtn_Click(object sender, RoutedEventArgs e) {
            if (saveGame == null) return;
            int levelToSync = PlayerXP.GetLevelForPoints(saveGame.Character.ExperiencePoints);
            foreach(var equipData in saveGame.Character.EquippedInventoryLists) {
                if (!equipData.Enabled || equipData.InventoryListIndex < 0 || equipData.InventoryListIndex > saveGame.InventoryItems.Count - 1) continue;
                
                // Sync the level onto the item
                saveGame.InventoryItems[equipData.InventoryListIndex].Level = levelToSync;
            }
            RefreshBackpackView();
        }
        private void SyncAllBtn_Click(object sender, RoutedEventArgs e) {
            int levelToSync = -1;
            if (profile != null) {
                var msgBox = new Controls.IntegerMessageBox("Please enter a level to sync your items for syncing", "Level: ", 0, maximumXP, maximumXP);
                msgBox.Owner = this;
                msgBox.ShowDialog();
                if (!msgBox.Succeeded) return;

                levelToSync = msgBox.Result;
            }
            else 
                levelToSync = PlayerXP.GetLevelForPoints(saveGame.Character.ExperiencePoints);
            
            foreach (Borderlands3Serial item in (profile == null ? saveGame.InventoryItems : profile.BankItems)) {
                Console.WriteLine($"Syncing level for item ({item.UserFriendlyName}) from {item.Level} to {levelToSync}");
                item.Level = levelToSync;
            }
            RefreshBackpackView();
        }
        
        private void CopyItem_Executed(object sender, ExecutedRoutedEventArgs e) {
            StringSerialPair svp = (StringSerialPair)BackpackListView.SelectedValue;
            SelectedSerial = svp.Val2;

            // Be nice and copy the code with a 0 seed (:
            string serialString = SelectedSerial.EncryptSerial(0);
            Console.WriteLine("Copying selected item code: {0}", serialString);

            // Copy it to the clipboard
            Clipboard.SetDataObject(serialString);
        }
        private void PasteItem_Executed(object sender, ExecutedRoutedEventArgs e) {
            PasteCodeBtn.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        }
        private void DuplicateItem_Executed(object sender, ExecutedRoutedEventArgs e) {
            // This basically just clicks both the copy and paste button
            CopyItem_Executed(null, e);
            PasteCodeBtn.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        }
        private void DeleteBinding_Executed(object sender, ExecutedRoutedEventArgs e) {
            StringSerialPair svp = BackpackListView.SelectedValue as StringSerialPair;
            if (svp == null) return;

            Console.WriteLine("Deleting item: {0} ({1})", svp.Val1, svp.Val2.UserFriendlyName);
            int idx = (saveGame == null ? profile.BankItems.FindIndex(x => ReferenceEquals(x, svp.Val2)) :
                saveGame.InventoryItems.FindIndex(x => ReferenceEquals(x, svp.Val2)));
            if (saveGame == null)
                profile.BankItems.RemoveAt(idx);
            else {

                // We need to preemptively adjust the equipped inventory lists so that way the equipped items stay consistent with the removed items.
                //? Consider putting this into BL3Tools instead?
                int eilIndex = saveGame.InventoryItems.FindIndex(x => ReferenceEquals(x, svp.Val2));
                foreach (var vx in saveGame.Character.EquippedInventoryLists) {
                    if (vx.InventoryListIndex == eilIndex)
                        vx.InventoryListIndex = -1;
                    else if (vx.InventoryListIndex > eilIndex)
                        vx.InventoryListIndex -= 1;
                }

                saveGame.DeleteItem(svp.Val2);
                if (saveGame.InventoryItems.Count <= 0) {
                    SelectedSerial = null;
                }
            }

            BackpackListView.ItemsSource = null;
            BackpackListView.ItemsSource = SlotItems;
            BackpackListView.Items.Refresh();
            RefreshBackpackView();
        }


        private void ChangeTypeBtn_Click(object sender, RoutedEventArgs e) {
            var itemKey = InventoryKeyDB.GetKeyForBalance(InventorySerialDatabase.GetBalanceFromShortName(SelectedBalance));
            var itemType = InventoryKeyDB.ItemTypeToKey.Where(x => x.Value.Contains(itemKey)).Select(x => x.Key).FirstOrDefault();

            Controls.ItemBalanceChanger changer = new Controls.ItemBalanceChanger(itemType, SelectedBalance) { Owner = this };

            changer.ShowDialog();

            // The user actually hit the save button and we have data about the item
            if (changer.SelectedInventoryData != null) {
                SelectedInventoryData = changer.SelectedInventoryData;
                SelectedBalance = changer.SelectedBalance;

                RefreshBackpackView();
            }
        }
        private void AddItemPartBtn_Click(object sender, RoutedEventArgs e) {
            if (SelectedSerial == null) return;

            var btn = (Button)sender;
            ListView obj = ((ListView)FindName(btn.Name.Replace("AddBtn", "") + "ListView"));


            string propertyName = obj.Name.Split(new string[] { "ListView" }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            if (propertyName == default) return;

            List<string> parts = (List<string>)SelectedSerial.GetType().GetProperty(propertyName).GetValue(SelectedSerial, null);

            parts.Add(InventorySerialDatabase.GetPartFromShortName(
                (propertyName == "Parts" ? SelectedSerial.InventoryKey : "InventoryGenericPartData"),
                (propertyName == "Parts" ? ValidParts : ValidGenerics).SourceCollection.Cast<string>().FirstOrDefault())
            );

            // Update the valid parts
            ValidParts.Refresh();
            ValidGenerics.Refresh();

            obj.GetBindingExpression(ListView.ItemsSourceProperty).UpdateTarget();
            RefreshBackpackView();
        }
        private void DeleteItemPartBtn_Click(object sender, RoutedEventArgs e) {
            var btn = (Button)sender;
            ListView obj = ((ListView)FindName(btn.Name.Replace("DelBtn", "") + "ListView"));

            string propertyName = obj.Name.Split(new string[] { "ListView" }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            if (propertyName == default) return;

            List<string> parts = (List<string>)SelectedSerial.GetType().GetProperty(propertyName).GetValue(SelectedSerial, null);

            if (obj.SelectedIndex != -1) {
                var longName = parts[obj.SelectedIndex];
                if (ForceLegitParts) {
                    foreach (string part in parts) {
                        List<string> dependencies = InventorySerialDatabase.GetDependenciesForPart(part);
                        if (part != longName && dependencies.Contains(longName)) {
                            var result = MessageBox.Show("Are you sure you want to delete this part? If you do that, you'll make the item illegitimate.", "Are you sure?", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No);

                            if(result == MessageBoxResult.No) return;
                            else {
                                // Update the force legit text box because they clearly don't want legit items :P
                                ForceLegitParts = false;
                                ForceLegitPartsChkBox.DataContext = null;
                                ForceLegitPartsChkBox.DataContext = this;
                                break;
                            }
                        }
                    }
                }
                // Remove the part
                parts.RemoveAt(obj.SelectedIndex);
            }

            // Update the valid parts
            ValidParts.Refresh();
            ValidGenerics.Refresh();

            obj.GetBindingExpression(ListView.ItemsSourceProperty).UpdateTarget();
            RefreshBackpackView();
        }

        // This bit of logic is here so that way the ListView's selected value stays up to date with the combobox's selected value :/
        private void ComboBox_DropDownChanged(object sender, EventArgs e) {
            ComboBox box = ((ComboBox)sender);
            ListView parent = box.FindParent<ListView>();
            if (parent == null) return;
            parent.SelectedValue = box.SelectedValue;
        }
        private string GetSelectedPart(string type, object sender, SelectionChangedEventArgs e) {
            if (e.Handled || e.RemovedItems.Count < 1) return null;
            ComboBox box = ((ComboBox)sender);

            // Get the last changed part and the new part
            // Old part is useful so that way we don't end up doing weird index updating shenanigans when the combobox updates
            var newPart = e.AddedItems.Cast<string>().FirstOrDefault();
            var oldPart = e.RemovedItems.Cast<string>().FirstOrDefault();
            if (newPart == default || oldPart == default) return null;

            Console.WriteLine($"Changed \"{oldPart}\" to \"{newPart}\"");
            ListView parent = box.FindParent<ListView>();
            if (parent.SelectedIndex == -1) return null;

            string assetCat = (type == "Parts" ? SelectedSerial.InventoryKey : "InventoryGenericPartData");
            string fullName = InventorySerialDatabase.GetPartFromShortName(assetCat, newPart);
            if (fullName == default) fullName = newPart;

            return fullName;
        }
        private void ItemPart_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            ListView parent = ((ComboBox)sender).FindParent<ListView>();
            if (parent == null) return;
            string propertyName = parent.Name.Split(new string[] { "ListView" }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            if (propertyName == default) return;

            string fullName = GetSelectedPart(propertyName, sender, e);
            if (fullName == null) return;

            // Do some weird jank in order to get the list of the value we've changed, so that way we can set the index
            List<string> parts = (List<string>)SelectedSerial.GetType().GetProperty(propertyName).GetValue(SelectedSerial, null);
            // The selected index stays updated with the current combobox because of "ComboBox_DropDownChanged".
            parts[parent.SelectedIndex] = fullName;

            if(ForceLegitParts) {
                List<string> dependantParts = InventorySerialDatabase.GetDependenciesForPart(fullName);
                if (dependantParts == null || dependantParts?.Count == 0) return;
                if (parts.Any(x => dependantParts.Contains(x))) return;
                else {
                    // Pick the first dependant part; This might not be what the user actually wants but ssh
                    parts.Add(dependantParts.FirstOrDefault());
                    RefreshBackpackView();
                }
            }
        }
        #endregion

        #region Profile
        /// <summary>
        /// When modifying a profile (specifically guardian rank), the saves also store data about the guardian rank in case a profile gets corrupted.
        /// We need to modify *all* of these save's guardian ranks just to be safe.
        /// This was way more of an issue in earlier releases of BL3 but we're keeping to be safe.
        /// </summary>
        /// <param name="files">A list of all of the save files to modify / inject into</param>
        private void InjectGuardianRank(string[] files) {
            foreach (string file in files) {
                try {
                    if (!(BL3Tools.BL3Tools.LoadFileFromDisk(file) is BL3Save save)) {
                        Console.WriteLine("Reading in file from \"{0}\"; Incorrect type: {1}");
                        continue;
                    }
                    var grcd = save.Character.GuardianRankCharacterData;
                    grcd.GuardianAvailableTokens = profile.Profile.GuardianRank.AvailableTokens;
                    grcd.GuardianExperience = profile.Profile.GuardianRank.GuardianExperience;
                    grcd.NewGuardianExperience = profile.Profile.GuardianRank.NewGuardianExperience;
                    grcd.GuardianRewardRandomSeed = profile.Profile.GuardianRank.GuardianRewardRandomSeed;
                    List<OakSave.GuardianRankRewardCharacterSaveGameData> zeroedGRRanks = new List<OakSave.GuardianRankRewardCharacterSaveGameData>();
                    foreach (var grData in grcd.RankRewards) {
                        bool bFoundMatch = false;
                        foreach (var pGRData in profile.Profile.GuardianRank.RankRewards) {
                            if (pGRData.RewardDataPath.Equals(grData.RewardDataPath)) {
                                grData.NumTokens = pGRData.NumTokens;
                                if (grData.NumTokens == 0)
                                    zeroedGRRanks.Add(grData);
                                bFoundMatch = true;
                            }
                        }

                        if (!bFoundMatch) zeroedGRRanks.Add(grData);
                    }
                    zeroedGRRanks = zeroedGRRanks.Distinct().ToList();

                    // In order to properly save zero-ed or missing GR ranks, we've got to remove them from the list (:
                    grcd.RankRewards.RemoveAll(x => zeroedGRRanks.Contains(x));

                    BL3Tools.BL3Tools.WriteFileToDisk(save, false);
                }
                catch (Exception ex) {
                    Console.WriteLine("Failed to inject guardian rank into save: \"{0}\"\n{1}", ex.Message, ex.StackTrace);
                }
                finally {
                    Console.WriteLine("Completed injecting guardian rank into saves...");
                }
            }
        }
        private void InjectGRBtn_Click(object sender, RoutedEventArgs e) {
            // Ask the user for all the saves to inject into
            OpenFileDialog fileDialog = new OpenFileDialog {
                Title = "Select saves to inject into",
                Filter = "BL3 Save/Profile (*.sav)|*.sav",
                InitialDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "My Games", "Borderlands 3", "Saved", "SaveGames"),
                Multiselect = true
            };
            if (fileDialog.ShowDialog() != true) return;

            InjectGuardianRank(fileDialog.FileNames);
        }
        private void ClearLLBtn_Click(object sender, RoutedEventArgs e) {
            if (profile == null) return;
            profile.LostLootItems.Clear();
        }
        private void ClearBankBtn_Click(object sender, RoutedEventArgs e) {
            if (profile == null) return;
            profile.BankItems.Clear();
        }

        #region Customization Unlockers/Lockers

        private void UnlockRoomDeco_Click(object sender, RoutedEventArgs e) {
            List<string> decos = DataPathTranslations.decoAssetPaths.Keys.ToList();
            foreach(string assetPath in decos) {
                // Only add asset paths that we don't already have unlocked
                if(!profile.Profile.UnlockedCrewQuartersDecorations.Any(x => x.DecorationItemAssetPath.Equals(assetPath))) {
                    var d = new OakSave.CrewQuartersDecorationItemSaveGameData() {
                        DecorationItemAssetPath = assetPath,
                        IsNew = true
                    };
                    profile.Profile.UnlockedCrewQuartersDecorations.Add(d);
                    Console.WriteLine("Profile doesn't contain room deco: {0}", assetPath);
                }
            }
        }
        private void UnlockCustomizations_Click(object sender, RoutedEventArgs e) {
            List<string> customizations = new List<string>();
            customizations.AddRange(DataPathTranslations.headAssetPaths.Keys.ToList());
            customizations.AddRange(DataPathTranslations.skinAssetPaths.Keys.ToList());
            customizations.AddRange(DataPathTranslations.echoAssetPaths.Keys.ToList());

            foreach(string assetPath in customizations) {
                string lowerAsset = assetPath.ToLower();
                if (lowerAsset.Contains("default") || (lowerAsset.Contains("emote") && (lowerAsset.Contains("wave") || lowerAsset.Contains("cheer") || lowerAsset.Contains("laugh") || lowerAsset.Contains("point")))) continue;

                if (!profile.Profile.UnlockedCustomizations.Any(x => x.CustomizationAssetPath.Equals(assetPath))) {
                    var d = new OakSave.OakCustomizationSaveGameData {
                        CustomizationAssetPath = assetPath,
                        IsNew = true
                    };
                    profile.Profile.UnlockedCustomizations.Add(d);
                    Console.WriteLine("Profile doesn't contain customization: {0}", assetPath);
                }
            }

            List<uint> assetHashes = new List<uint>();
            assetHashes.AddRange(DataPathTranslations.weaponSkinHashes);
            assetHashes.AddRange(DataPathTranslations.trinketHashes);
            foreach (uint assetHash in assetHashes) {
                if(!profile.Profile.UnlockedInventoryCustomizationParts.Any(x => x.CustomizationPartHash == assetHash)) {
                    profile.Profile.UnlockedInventoryCustomizationParts.Add(new OakSave.OakInventoryCustomizationPartInfo {
                        CustomizationPartHash = assetHash,
                        IsNew = true
                    });
                }
            }
        }

        private void LockRoomDeco_Click(object sender, RoutedEventArgs e) {
            // Remove all of the customizations in order to "lock" them.
            profile.Profile.UnlockedCrewQuartersDecorations.Clear();
        }
        private void LockCustomizations_Click(object sender, RoutedEventArgs e) {
            profile.Profile.UnlockedCustomizations.Clear();
            profile.Profile.UnlockedInventoryCustomizationParts.Clear();
        }
        #endregion

        #endregion

        #region About
        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e) {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri));
            e.Handled = true;
        }
        #endregion

        #endregion

        private void AutoUpdaterOnCheckForUpdateEvent(UpdateInfoEventArgs args) {
            if(args.Error == null) {
                if(args.IsUpdateAvailable) {
                    MessageBoxResult result;
                    if(args.Mandatory.Value) {
                        result = MessageBox.Show($@"There is a new version {args.CurrentVersion} available. This update is required. Press OK to begin updating.", "Update Available", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else {
                        result = MessageBox.Show($@"There is a new version {args.CurrentVersion} available. You're using version {args.InstalledVersion}. Do you want to update now?", "Update Available", MessageBoxButton.YesNo, MessageBoxImage.Information);
                    }

                    if (result.Equals(MessageBoxResult.Yes) || result.Equals(MessageBoxResult.OK)) {
                        try {
#if !SINGLE_FILE
                            // Change what we're doing depending on whether or not we're built in single file (1 exe in a zip) or "release" (distributed as a zip with multiple files & folders).
                            args.DownloadURL = args.DownloadURL.Replace("-Portable", "");
#endif
                            if (AutoUpdater.DownloadUpdate(args)) {
                                Application.Current.Shutdown();
                            }
                        }
                        catch (Exception exception) {
                            MessageBox.Show(exception.Message, exception.GetType().ToString(), MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                }
            }
            else {
                if (args.Error is System.Net.WebException) {
                    MessageBox.Show("There is a problem reaching update server. Please check your internet connection and try again later.", "Update Check Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                else {
                    MessageBox.Show(args.Error.Message, args.Error.GetType().ToString(), MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void UpdateButton_Click(object sender, RoutedEventArgs e) {
            //AutoUpdater.Start(UpdateURL);
        }


        private void AddPart(string part, bool isGeneric = false)
        {
            if (SelectedSerial == null) return;
            
            var parts = isGeneric ? SelectedSerial.GenericParts : SelectedSerial.Parts;
            if (isGeneric && parts.Count == 15)
                return;
            else if (!isGeneric && parts.Count == 63)
                return;
            parts.Add(InventorySerialDatabase.GetPartFromShortName(isGeneric ? "InventoryGenericPartData" : SelectedSerial.InventoryKey, part));

            // Update the valid parts
            if(isGeneric)
            {
                RaisePropertyChanged(nameof(ValidGenerics));
            }
            else
            {
                RaisePropertyChanged(nameof(ValidParts));
            }
            
            RefreshBackpackView();
        }

        private void RemovePart(int index, bool isGeneric = false)
        {
            var parts = isGeneric ? SelectedSerial.GenericParts : SelectedSerial.Parts;
            parts.RemoveAt(index);

            if (isGeneric)
            {
                RaisePropertyChanged(nameof(ValidGenerics));
            }
            else
            {
                RaisePropertyChanged(nameof(ValidParts));
            }

            RefreshBackpackView();
        }

        private void PartsOnClick(object sender, MouseButtonEventArgs e)
        {
            var allowEvent = !disableEvents;
            if (sender is ListView view && allowEvent)
            {
                disableEvents = true;
                if (view.SelectedIndex != -1)
                {
                    AddPart(view.SelectedValue.ToString(), sender != PartsListView);
                    view.SelectedIndex = -1;
                }
                disableEvents = false;
            }
        }

        private void PartsOnRemove(object sender, MouseButtonEventArgs e)
        {
            if(!IsReorder)
            {
                var allowEvent = !disableEvents;
                if (sender is ListView view && allowEvent)
                {
                    disableEvents = true;
                    if (view.SelectedIndex != -1)
                    {
                        RemovePart(view.SelectedIndex, sender != ListViewSelectedParts);
                        view.SelectedIndex = -1;
                    }
                    disableEvents = false;
                }
            }
        }

        private void ListView_Initialized(object sender, EventArgs e)
        {
            ((ListView)sender).SelectedIndex = -1;
            //if(sender == ListViewSelectedGenerics)
            //{
            //    var items = ListViewSelectedGenerics.Items;
            //}
        }

        private void upBtn_Click(object sender, RoutedEventArgs e)
        {

            var isGeneric = true;
            var index = -1;
            ListView view;
            if(ListViewSelectedParts.IsVisible)
            {
                isGeneric = false;
                index = ListViewSelectedParts.SelectedIndex;
                view = ListViewSelectedParts;
            }
            else
            {
                index = ListViewSelectedGenerics.SelectedIndex;
                view = ListViewSelectedGenerics;
            }
            var parts = isGeneric ? SelectedSerial.GenericParts : SelectedSerial.Parts;
            if (index > 0)
            {
                var previous = parts[index - 1];
                parts[index - 1] = view.SelectedValue.ToString();
                parts[index] = previous;
                RefreshBackpackView();
                view.SelectedIndex = index - 1;
            }

        }

        private void downBtn_Click(object sender, RoutedEventArgs e)
        {
            var isGeneric = true;
            var index = -1;
            ListView view;
            if (ListViewSelectedParts.IsVisible)
            {
                isGeneric = false;
                index = ListViewSelectedParts.SelectedIndex;
                view = ListViewSelectedParts;
            }
            else
            {
                index = ListViewSelectedGenerics.SelectedIndex;
                view = ListViewSelectedGenerics;
            }
            var parts = isGeneric ? SelectedSerial.GenericParts : SelectedSerial.Parts;
            if (index < (parts.Count-1))
            {
                var previous = parts[index + 1];
                parts[index + 1] = view.SelectedValue.ToString();
                parts[index] = previous;
                RefreshBackpackView();
                view.SelectedIndex = index + 1;
            }

        }

        private void topBtn_Click(object sender, RoutedEventArgs e)
        {
            var isGeneric = true;
            var index = -1;
            ListView view;
            if (ListViewSelectedParts.IsVisible)
            {
                isGeneric = false;
                index = ListViewSelectedParts.SelectedIndex;
                view = ListViewSelectedParts;
            }
            else
            {
                index = ListViewSelectedGenerics.SelectedIndex;
                view = ListViewSelectedGenerics;
            }
            var parts = isGeneric ? SelectedSerial.GenericParts : SelectedSerial.Parts;
            if (index > 0)
            {
                var previous = parts[0];
                parts[0] = view.SelectedValue.ToString();
                parts[index] = previous;
                RefreshBackpackView();
                view.SelectedIndex = 0;
            }


        }

        private void bottomBtn_Click(object sender, RoutedEventArgs e)
        {
            var isGeneric = true;
            var index = -1;
            ListView view;
            if (ListViewSelectedParts.IsVisible)
            {
                isGeneric = false;
                index = ListViewSelectedParts.SelectedIndex;
                view = ListViewSelectedParts;
            }
            else
            {
                index = ListViewSelectedGenerics.SelectedIndex;
                view = ListViewSelectedGenerics;
            }
            var parts = isGeneric ? SelectedSerial.GenericParts : SelectedSerial.Parts;
            if (index < (parts.Count - 1))
            {
                var previous = parts[parts.Count - 1];
                parts[parts.Count - 1] = view.SelectedValue.ToString();
                parts[index] = previous;
                RefreshBackpackView();
                view.SelectedIndex = parts.Count - 1;
            }

        }

        public static ListCollectionView ConvertLootlemon(List<Borderlands3Serial> itemsToSearch, string SearchTerm)
        {
            var px = new List<StringSerialPair>();

            for (int i = 0; i < itemsToSearch.Count; i++)
            {
                var serial = itemsToSearch[i];

                // Split the items out into groups, assume weapons because they're the most numerous and different
                string itemType = "Weapon";

                if (serial.InventoryKey == null) itemType = "Other";
                else if (serial.InventoryKey.Contains("_ClassMod")) itemType = "Class Mods";
                else if (serial.InventoryKey.Contains("_Artifact")) itemType = "Artifacts";
                else if (serial.InventoryKey.Contains("_Shield")) itemType = "Shields";
                else if (serial.InventoryKey.Contains("_Customization")) itemType = "Customizations";
                else if (serial.InventoryKey.Contains("_GrenadeMod_")) itemType = "Grenades";

                var name = serial.UserFriendlyName.ToLower();
                if (!string.IsNullOrWhiteSpace(SearchTerm) && !name.Contains(SearchTerm))
                    continue;
                ItemsInfo.TryGetValue(name, out var itemInfo);
                px.Add(new StringSerialPair(itemType, serial, itemInfo ?? new ItemInfo()));
            }

            ListCollectionView vx = new ListCollectionView(px);
            vx.GroupDescriptions.Add(new PropertyGroupDescription("Val1"));
            return vx;
        }

        private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }

        private void PropertyGrid_PreparePropertyItem(object sender, Xceed.Wpf.Toolkit.PropertyGrid.PropertyItemEventArgs e)
        {
            
        }

        private void LootlemonView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _itemToImport = LootlemonView.SelectedItem as StringSerialPair;
        }

        private ICommand _importCommand;


        public ICommand ImportCommand
        {
            get
            {
                if (_importCommand == null) _importCommand = new DelegateCommand(ImportBtn_Click);

                return _importCommand;
            }
        }
        private void ImportBtn_Click(object itemToImport)
        {
            _itemToImport = itemToImport as StringSerialPair;
            if (_itemToImport != null)
            {
                if (profile == null) saveGame.AddItem(_itemToImport.Val2);
                else profile.BankItems.Add(_itemToImport.Val2);

                BackpackListView.ItemsSource = null;
                BackpackListView.ItemsSource = SlotItems;
            }
        }
    }

    public class DelegateCommand : ICommand
    {
        public event EventHandler CanExecuteChanged;
        private Action<object> action;

        public DelegateCommand(Action<object> action)
        {
            this.action = action;
        }
        public bool CanExecute(object parameter)
        {
            return true;
        }

        public void Execute(object parameter)
        {
            action.Invoke(parameter);
        }
    }
}
