using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BL3Tools.GameData.Items
{
    public static class InventorySerialDatabase
    {
        /// <summary>
        /// The maximum version acceptable for the inventory DB
        /// </summary>
        public static long MaximumVersion { get; private set; } = long.MinValue;

        /// <summary>
        /// A <c>JObject</c> representing the InventoryDB as loaded from JSON
        /// </summary>
        public static JObject InventoryDatabase { get; private set; } = null;

        /// <summary>
        /// A list containing all of the valid InventoryDatas for <b>ANY</b> item
        /// </summary>
        public static List<string> InventoryDatas { get; private set; } = null;

        /// <summary>
        /// A list containing all of the valid Manufacturers (<c>ManufacturerData</c>) for <b>ANY</b> item
        /// </summary>
        public static List<string> Manufacturers { get; private set; } = null;

        /// <summary>
        /// A <c>JObject</c> representing the balance to inventory data mapping as loaded from JSON.
        /// </summary>
        public static JObject InventoryDataDatabase { get; private set; } = null;

        /// <summary>
        /// A dictionary mapping a balance to an inventory data string
        /// </summary>
        public static Dictionary<string, string> BalanceToData { get; private set; } = null;

        /// <summary>
        /// A <c>JObject</c> representing the valid part database (excluders/dependencies) as loaded from JSON
        /// </summary>
        public static JObject ValidPartDatabase { get; private set; } = null;

        /// <summary>
        /// A dictionary mapping a part to its dependencies/excluders (key in the second dictionary)
        /// </summary>
        public static Dictionary<string, Dictionary<string, List<string>>> PartDatabase { get; private set; } = null;

        /// <summary>
        /// A <c>JObject</c> representing the valid anointment database as loaded from JSON
        /// </summary>
        public static JObject ValidGenericDatabase { get; private set; } = null;

        /// <summary>
        /// A dictionary mapping a balance to the given list of valid anointments (excludes other parts in validity).
        /// </summary>
        public static Dictionary<string, List<string>> GenericsDatabase { get; private set; } = null;

        private static readonly string embeddedDatabasePath = "BL3Tools.GameData.Items.SerialDB.Inventory Serial Number Database.json";
        private static readonly string embeddedInvDataDBPath = "BL3Tools.GameData.Items.Mappings.balance_to_inv_data.json";
        private static readonly string embeddedPartDBPath = "BL3Tools.GameData.Items.Mappings.valid_part_database.json";
        private static readonly string embeddedGenericsPath = "BL3Tools.GameData.Items.Mappings.valid_generics.json";

        private static Dictionary<string, List<string>> partsCache = new Dictionary<string, List<string>>();
        private static List<string> balanceCache = null;
        private static bool isInitialized = false;

        static InventorySerialDatabase()
        {
            InitializeDatabases();
        }

        /// <summary>
        /// Initializes databases only when needed (lazy loading).
        /// </summary>
        public static void InitializeDatabases()
        {
            if (!isInitialized)
            {
                Console.WriteLine("Initializing InventorySerialDatabase...");

                Assembly me = typeof(BL3Tools).Assembly;

                using (Stream stream = me.GetManifestResourceStream(embeddedDatabasePath))
                using (StreamReader reader = new StreamReader(stream))
                {
                    string result = reader.ReadToEnd();
                    LoadInventorySerialDatabase(result);
                }

                using (Stream stream = me.GetManifestResourceStream(embeddedInvDataDBPath))
                using (StreamReader reader = new StreamReader(stream))
                {
                    string result = reader.ReadToEnd();
                    LoadInventoryDataDatabase(result);
                }

                using (Stream stream = me.GetManifestResourceStream(embeddedPartDBPath))
                using (StreamReader reader = new StreamReader(stream))
                {
                    string result = reader.ReadToEnd();
                    LoadPartDatabase(result);
                }

                using (Stream stream = me.GetManifestResourceStream(embeddedGenericsPath))
                using (StreamReader reader = new StreamReader(stream))
                {
                    string result = reader.ReadToEnd();
                    LoadGenericsDatabase(result);
                }

                isInitialized = true;
            }
        }

        public static int GetBitsToEat(string category, long version)
        {
            JArray versionArray = ((JArray)InventoryDatabase[category]["versions"]);
            var minimumBits = versionArray.First["bits"].Value<int>();

            foreach (JObject versionData in versionArray.Children())
            {
                int arrVer = versionData["version"].Value<int>();
                if (arrVer > version)
                    return minimumBits;
                else if (version >= arrVer)
                {
                    minimumBits = versionData["bits"].Value<int>();
                }
            }

            return minimumBits;
        }

        public static string GetPartByIndex(string category, int index)
        {
            if (index < 0) return null;
            JArray assets = ((JArray)InventoryDatabase[category]["assets"]);
            if (index > assets.Count) return null;

            return assets[index - 1].Value<string>();
        }

        public static int GetIndexByPart(string category, string part)
        {
            var assets = ((JArray)InventoryDatabase[category]["assets"]).Children().Select(x => x.Value<string>()).ToList();
            int index = assets.IndexOf(part);
            if (index == -1) index = assets.IndexOf(part.ToLowerInvariant());

            return index == -1 ? -1 : index + 1;
        }

        public static string GetShortNameFromBalance(string balance)
        {
            if (balanceCache == null)
            {
                balanceCache = ((JArray)InventoryDatabase["InventoryBalanceData"]["assets"])
                    .Children()
                    .Select(x => x.Value<string>())
                    .ToList();
            }

            string serializedName = balanceCache
                .FirstOrDefault(x => x.Contains(balance) || x.ToLowerInvariant().Contains(balance));

            return !string.IsNullOrEmpty(serializedName) ? serializedName.Split('.').Last() : null;
        }

        public static string GetBalanceFromShortName(string shortName)
        {
            string balance = balanceCache.FirstOrDefault(x => x.EndsWith(shortName));
            return string.IsNullOrEmpty(balance) ? null : balance;
        }

        public static string GetPartFromShortName(string category, string shortName)
        {
            if (category == null || shortName == null) return null;

            var parts = ((JArray)InventoryDatabase[category]["assets"]).Children().Select(x => x.Value<string>());
            string part = parts.FirstOrDefault(x => x.EndsWith(shortName));

            return string.IsNullOrEmpty(part) ? null : part;
        }

        public static List<string> GetManufacturers(bool bShortName = true)
        {
            if (bShortName) return Manufacturers.Select(x => x.Split('.').Last()).ToList();
            return Manufacturers;
        }

        public static List<string> GetInventoryDatas(bool bShortName = true)
        {
            if (bShortName) return InventoryDatas.Select(x => x.Split('.').Last()).ToList();
            return InventoryDatas;
        }

        public static string GetInventoryDataByBalance(string balance, bool bShortName = true, bool bSelfCorrect = true)
        {
            string longName = GetBalanceFromShortName(balance);
            if (longName == null) longName = balance;

            if (BalanceToData.ContainsKey(longName))
            {
                string data = BalanceToData[longName];
                return bShortName ? data.Split('.').Last() : data;
            }

            if (!bSelfCorrect) return null;

            string shortName = balance.Split('.').Last();
            string str = "WT_" + shortName.Substring(shortName.IndexOf('_') + 1);
            str = str.Substring(0, str.LastIndexOf('_'));
            List<int> distances = new List<int>();
            foreach (string data in GetInventoryDatas(true))
            {
                distances.Add(LevenshteinDistance.Compute(str, data.Split('.').Last()));
            }
            return GetInventoryDatas(bShortName)[distances.IndexOf(distances.Min())];
        }

        public static List<string> GetPartsForInvKey(string invKey)
        {
            if (string.IsNullOrEmpty(invKey))
            {
                // Log the issue or return a default empty list
                Console.WriteLine("Invalid or empty inventory key. Returning an empty list.");
                return new List<string>(); // Returning empty parts list instead of throwing an exception
            }

            if (partsCache.TryGetValue(invKey, out var cachedParts))
            {
                return cachedParts;
            }

            if (!InventoryDatabase.ContainsKey(invKey))
            {
                throw new KeyNotFoundException($"The inventory key '{invKey}' was not found in the InventoryDatabase.");
            }

            var parts = ((JArray)InventoryDatabase[invKey]["assets"])
                .Children()
                .Select(x => x.Value<string>())
                .ToList();

            partsCache[invKey] = parts;
            return parts;
        }

        public static List<string> GetValidPartsForParts(string category, List<string> parts)
        {
            var allParts = GetPartsForInvKey(category);
            foreach (string part in parts)
            {
                if (part == null) continue;
                if (!PartDatabase.ContainsKey(part)) continue;

                var dict = PartDatabase[part];
                var dependencies = dict["Dependencies"];
                var excluders = dict["Excluders"];
                allParts.RemoveAll(x => excluders.Contains(x));
            }
            return allParts;
        }

        public static List<string> GetValidGenericsForBalance(string balance)
        {
            if (!GenericsDatabase.ContainsKey(balance)) return null;
            return GenericsDatabase[balance];
        }

        public static List<string> GetDependenciesForPart(string part)
        {
            if (!PartDatabase.ContainsKey(part)) return null;
            var dict = PartDatabase[part];
            return dict["Dependencies"];
        }

        public static List<string> GetExcludersForPart(string part)
        {
            if (!PartDatabase.ContainsKey(part)) return null;
            var dict = PartDatabase[part];
            return dict["Excluders"];
        }

        public static bool LoadInventorySerialDatabase(string JSONString)
        {
            var originalDatabase = InventoryDatabase;
            try
            {
                InventoryDatabase = JObject.FromObject(JsonConvert.DeserializeObject(JSONString));
                InventoryDatas = ((JArray)InventoryDatabase["InventoryData"]["assets"]).Children().Select(x => x.Value<string>()).ToList();
                Manufacturers = ((JArray)InventoryDatabase["ManufacturerData"]["assets"]).Children().Select(x => x.Value<string>()).ToList();

                foreach (JProperty token in InventoryDatabase.Children<JProperty>())
                {
                    var versions = token.Value.First;
                    var assets = token.Value.Last;
                    foreach (JObject versionData in versions.First.Children())
                    {
                        long bitAmt = versionData["bits"].Value<long>();
                        long versionNum = versionData["version"].Value<long>();

                        if (versionNum > MaximumVersion) MaximumVersion = versionNum;
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                Console.WriteLine(ex.StackTrace);
                InventoryDatabase = originalDatabase;
            }
            return false;
        }

        public static bool LoadInventoryDataDatabase(string JSONString)
        {
            var originalDB = InventoryDataDatabase;
            try
            {
                InventoryDataDatabase = JObject.FromObject(JsonConvert.DeserializeObject(JSONString));
                BalanceToData = InventoryDataDatabase.ToObject<Dictionary<string, string>>();
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                Console.WriteLine(ex.StackTrace);
                InventoryDataDatabase = originalDB;
            }
            return false;
        }

        public static bool LoadPartDatabase(string JSONString)
        {
            var originalDB = ValidPartDatabase;
            try
            {
                ValidPartDatabase = JObject.FromObject(JsonConvert.DeserializeObject(JSONString));
                PartDatabase = new Dictionary<string, Dictionary<string, List<string>>>();
                foreach (JProperty token in ValidPartDatabase.Children<JProperty>())
                {
                    string part = token.Name;
                    JObject value = token.Value as JObject;
                    var dependencies = (value["Dependencies"] as JArray).ToObject<List<string>>();
                    var excluders = (value["Excluders"] as JArray).ToObject<List<string>>();
                    PartDatabase.Add(part, new Dictionary<string, List<string>>() {
                        { "Dependencies", dependencies },
                        { "Excluders", excluders }
                    });
                }
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                Console.WriteLine(ex.StackTrace);
                ValidPartDatabase = originalDB;
            }
            return false;
        }

        public static bool LoadGenericsDatabase(string JSONString)
        {
            var originalDB = ValidGenericDatabase;
            try
            {
                ValidGenericDatabase = JObject.FromObject(JsonConvert.DeserializeObject(JSONString));
                GenericsDatabase = new Dictionary<string, List<string>>();
                foreach (JProperty token in ValidGenericDatabase.Children<JProperty>())
                {
                    string part = token.Name;
                    var value = (token.Value as JArray).ToObject<List<string>>();
                    GenericsDatabase.Add(part, value);
                }
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                Console.WriteLine(ex.StackTrace);
                ValidGenericDatabase = originalDB;
            }
            return false;
        }
    }
}
