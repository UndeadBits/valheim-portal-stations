using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using Jotunn.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace UndeadBits.ValheimMods.PortalStation {
    
    /// <summary>
    /// Portal station plugin - main entry point.
    /// </summary>
    [BepInPlugin(PLUGIN_GUID, PLUGIN_NAME, PLUGIN_VERSION)]
    [BepInDependency(Jotunn.Main.ModGuid)]
    [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Minor)]
    internal class PortalStationPlugin : BaseUnityPlugin {
        private const string PLUGIN_GUID = "com.undeadbits.valheimmods.portalstation";
        private const string PLUGIN_NAME = "PortalStation";
        public const string PLUGIN_VERSION = "0.12.0";

        private const float STATION_SYNC_INTERVAL = 1.0f;

        #region Configuration
        
        private const string CONFIG_SECTION_GENERAL = "General";
        private const string CONFIG_SECTION_PERSONAL_TELEPORTATION_DEVICE = "PersonalTeleportationDevice";
        
        private const string CONFIG_KEY_IGNORE_TELEPORTATION_RESTRICTIONS = "ignoreTeleportationRestrictions";
        private const string CONFIG_KEY_IGNORE_TELEPORTATION_RESTRICTIONS_DESC = "Whether vanilla teleportation restrictions will be ignored by portal stations and personal teleportation devices.";
        private const bool CONFIG_KEY_IGNORE_TELEPORTATION_RESTRICTIONS_DEFAULT_VALUE = false;
        
        private const string CONFIG_KEY_PERSONAL_TELEPORTATION_DEVICE_FUEL_ITEM_NAME = "fuelItemName";
        private const string CONFIG_KEY_PERSONAL_TELEPORTATION_DEVICE_FUEL_ITEM_NAME_DESC = "The fuel item to use.\nLeave empty to disable fuel consumption.";
        private const string CONFIG_KEY_PERSONAL_TELEPORTATION_DEVICE_FUEL_ITEM_NAME_DEFAULT_VALUE = "GreydwarfEye";

        private const string CONFIG_KEY_TELEPORTATION_DISTANCE_PER_FUEL_ITEM = "teleportationDistancePerFuelItem";
        private const string CONFIG_KEY_TELEPORTATION_DISTANCE_PER_FUEL_ITEM_DESC = "The teleportation distance per fuel item.";
        private const float CONFIG_KEY_TELEPORTATION_DISTANCE_PER_FUEL_ITEM_DEFAULT_VALUE = 1000;

        private const string CONFIG_KEY_ADDITIONAL_TELEPORTATION_DISTANCE_PER_UPGRADE = "additionalTeleportationDistancePerUpgrade";
        private const string CONFIG_KEY_ADDITIONAL_TELEPORTATION_DISTANCE_PER_UPGRADE_DESC = "The additional teleportation distance per device upgrade.";
        private const float CONFIG_KEY_ADDITIONAL_TELEPORTATION_DISTANCE_PER_UPGRADE_DEFAULT_VALUE = 1000;
        
        #endregion

        public static readonly CustomLocalization kLocalization = LocalizationManager.Instance.GetLocalization();
        
        private readonly Harmony harmony = new(PLUGIN_GUID); 

        private readonly Dictionary<ZDOID, PortalStation.Destination> destinationCache = new();
        private readonly Stack<ZDOID> removedZDOs = new();
        private readonly List<PortalStation.Destination> sortedDestinations = new();
        private readonly ZPackage cachedDestinationPackage = new();
        private readonly List<ZDO> tempZDOList = new();
        private readonly HashSet<string> tempStationNames = new();
        private readonly List<ZDO> tempSyncList = new();
        private readonly Dictionary<ZDOID, ZDO> tempAvailablePortalStationZDOs = new();

        private AssetBundle assetBundle;
        private GameObject portalStationPrefab;
        private CustomPiece portalStationPiece;
        
        private GameObject portalStationGUIPrefab;
        private GameObject portalStationDestinationItemPrefab;

        private GameObject personalTeleportationDevicePrefab;
        private CustomItem personalTeleportationDeviceItem;
        
        private GameObject personalTeleportationDeviceGUIPrefab;
        private GameObject personalTeleportationDeviceGUIDestinationItemPrefab;

        private PortalStationGUI portalStationGUIInstance;
        private PersonalTeleportationDeviceGUI personalTeleportationDeviceGUIInstance;

        private ConfigEntry<bool> ignoreTeleportationRestrictionsConfigEntry;
        private ConfigEntry<string> fuelItemNameConfigEntry;
        private ConfigEntry<float> teleportationDistancePerFuelItemConfigEntry;
        private ConfigEntry<float> additionalTravelDistancePerUpgradeConfigEntry;

        private string fuelItemName;
        private ItemDrop fuelItem;
        private bool useFuel;
        private bool destroyed;

        
        [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.UseItem))]
        // ReSharper disable once InconsistentNaming
        private class Humanoid_UseItem_Patch {
            [HarmonyPrefix]
            // ReSharper disable once InconsistentNaming
            // ReSharper disable once UnusedMember.Local
            // ReSharper disable once UnusedParameter.Local
            private static bool UseItem(Inventory inventory, ItemDrop.ItemData item, /* ReSharper disable once UnusedParameter.Local */ bool fromInventoryGui, Humanoid __instance) {
                if (item.m_shared.m_name != PersonalTeleportationDevice.ITEM_NAME) {
                    return true;
                }

                PersonalTeleportationDevice.UseItem(__instance, item);

                return false;
            }
        }
        
        [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.Awake))]
        // ReSharper disable once InconsistentNaming
        private class Humanoid_Awake_Patch {
            [HarmonyPrefix]
            // ReSharper disable once InconsistentNaming
            // ReSharper disable once UnusedMember.Local
            // ReSharper disable once SuggestBaseTypeForParameter
            private static void Awake(Humanoid __instance) {
                __instance.gameObject.AddComponent<PortalStationUser>();
                __instance.gameObject.AddComponent<PersonalTeleportationDeviceUser>();
            }
        }
        
        /*
        /// <summary>
        /// Patches the ItemDrop.UseItem method so custom logic can be injected.
        /// </summary>
        [HarmonyPatch(typeof(ItemDrop), nameof(ItemDrop.UseItem))]
        private class ItemDrop_UseItem_Patch {

            [HarmonyPrefix]
            private static bool UseItem(Humanoid user, ItemDrop.ItemData item, ItemDrop __instance, ref bool __result) {
                var customInteractable = __instance.GetComponentInChildren<ICustomInteractable>();
                if (customInteractable != null) {
                    __result = customInteractable.UseItem(user, item, __result);
                    return true;
                }

                return false;
            }
        }
        */

        /// <summary>
        /// Gets the current plugin instance.
        /// </summary>
        public static PortalStationPlugin Instance {
            get;
            private set;
        }

        /// <summary>
        /// Raised every time when a client received a new destination list from the server.
        /// </summary>
        public readonly UnityEvent ChangeDestinations = new();

        /// <summary>
        /// Gets the available destinations to teleport to.
        /// </summary>
        public IEnumerable<PortalStation.Destination> AvailableDestinations {
            get { return this.sortedDestinations; }
        }
        
        /// <summary>
        /// Gets the name of the fuel item to use for the personal teleportation device.
        /// </summary>
        public string FuelItemId {
            get { return this.fuelItem ? this.fuelItem.m_itemData.m_shared.m_name : null; }
        }

        /// <summary>
        /// Gets the fuel item to use.
        /// </summary>
        public ItemDrop FuelItem {
            get { return this.fuelItem; }
        }

        /// <summary>
        /// Determines whether to use fuel for the personal teleportation device or not.
        /// </summary>
        public bool UseFuel {
            get { return this.useFuel; }
        }

        /// <summary>
        /// Gets the travel distance per fuel item.
        /// </summary>
        public float TravelDistancePerFuelItem {
            get { return this.teleportationDistancePerFuelItemConfigEntry?.Value ?? CONFIG_KEY_TELEPORTATION_DISTANCE_PER_FUEL_ITEM_DEFAULT_VALUE; }
        }

        /// <summary>
        /// Gets the additional travel distance per device upgrade.
        /// </summary>
        public float AdditionalTeleportationDistancePerUpgrade {
            get { return this.additionalTravelDistancePerUpgradeConfigEntry?.Value ?? CONFIG_KEY_ADDITIONAL_TELEPORTATION_DISTANCE_PER_UPGRADE_DEFAULT_VALUE; }
        }
        
        /// <summary>
        /// Generates a name for a station based on other stations in the world.
        ///
        /// FIXME: This must be done on the server side
        /// </summary>
        public string CreateStationName() {
            var stationNumber = 1;
            var template = kLocalization.TryTranslate("$auto_portal_station_name");
            if (!template.Contains("PS_NUM")) {
                Jotunn.Logger.LogWarning("Localization value for $auto_portal_station_name must contain a placeholder for the station number (PS_NUM)");
                template = $"{template} PS_NUM";
            }
            
            if (!TryGetStationNames(this.tempStationNames)) {
                return template.Replace("PS_NUM", stationNumber.ToString("D2"));
            }

            while (true) {
                var stationName = template.Replace("PS_NUM", (stationNumber++).ToString("D2"));
                if (this.tempStationNames.Contains(stationName)) {
                    continue;
                }

                return stationName;
            }
        }

        /// <summary>
        /// Creates a new teleport destination item.
        /// </summary>
        public DestinationItem CreatePSDestinationItem(RectTransform parent) {
            if (this.destroyed) {
                return null;
            }

            var instance = Instantiate(this.portalStationDestinationItemPrefab);
            instance.GetComponent<RectTransform>().SetParent(parent, false);

            return instance.GetComponent<DestinationItem>();
        }

        /// <summary>
        /// Creates a new teleport destination item.
        /// </summary>
        public DestinationItem CreatePTDDestinationItem(RectTransform parent) {
            if (this.destroyed) {
                return null;
            }

            var instance = Instantiate(this.personalTeleportationDeviceGUIDestinationItemPrefab);
            instance.GetComponent<RectTransform>().SetParent(parent, false);

            return instance.GetComponent<DestinationItem>();
        }

        /// <summary>
        /// Gets the portal station GUI instance.
        /// </summary>
        public PortalStationGUI GetPortalStationGUI() {
            if (this.destroyed) {
                return null;
            }
            
            if (!this.portalStationGUIInstance) {
                this.portalStationGUIInstance = CreatePortalStationGUI();
            }

            return this.portalStationGUIInstance;
        }

        /// <summary>
        /// Gets the personal teleportation device GUI instance.
        /// </summary>
        public PersonalTeleportationDeviceGUI GetPersonalTeleportationDeviceGUIInstance() {
            if (this.destroyed) {
                return null;
            }

            if (!this.personalTeleportationDeviceGUIInstance) {
                this.personalTeleportationDeviceGUIInstance = CreatePersonalTeleportationDeviceGUIInstance();
            }

            return this.personalTeleportationDeviceGUIInstance;
        }

        /// <summary>
        /// Requests current list of portal station destinations from the server,
        /// </summary>
        public void RequestPortalStationDestinationsFromServer() {
            if (this.destroyed) {
                return;
            }

            ZRoutedRpc.instance.InvokeRoutedRPC(nameof(RPC_RequestStationList));
        }

        /// <summary>
        /// Gets a portal station by the given station Id.
        /// </summary>
        /// <param name="stationId">The stations id</param>
        /// <returns>The station or null if not found</returns>
        public PortalStation.Destination GetPortalStation(ZDOID stationId) {
            if (this.destroyed) {
                return null;
            }

            if (this.destinationCache.TryGetValue(stationId, out var station)) {
                return station;
            }
            
            // ReSharper disable once ForeachCanBeConvertedToQueryUsingAnotherGetEnumerator no LINQ in frequently called functions
            foreach (var destination in this.sortedDestinations) {
                if (destination.ID == stationId) {
                    return destination;
                }
            }
            
            return null;
        }

        /// <summary>
        /// Determines whether the player can teleport.
        /// </summary>
        /// <param name="player">The player</param>
        /// <returns>Whether teleportation is allowed or not</returns>
        public bool CanTeleportPlayer(Humanoid player) {
            if (this.destroyed) {
                return false;
            }

            if (ZoneSystem.instance.GetGlobalKey("noportals")) {
                if (player) {
                    player.Message(MessageHud.MessageType.Center, "$msg_blocked");
                }

                return false;
            }

            // ReSharper disable once InvertIf it's better readable this way around
            if (!player.IsTeleportable() && !this.ignoreTeleportationRestrictionsConfigEntry.Value) {
                if (player) {
                    player.Message(MessageHud.MessageType.Center, "$msg_noteleport");
                }

                return false;
            }

            return true;
        }

        /// <summary>
        /// Initializes the plugin.
        /// </summary>
        private void Awake() {
            this.harmony.PatchAll();
            
            Instance = this;

            ItemManager.OnItemsRegistered += UpdateFuelItem;
            PrefabManager.OnPrefabsRegistered += InitDestinationSync;

            this.assetBundle = AssetUtils.LoadAssetBundleFromResources("Assets.portal_station_assets");

            AddConfigKeys();
            AddPortalStationPiece();
            AddPersonalTeleportationDevice();
            // InvokeRepeating(nameof(SyncAvailablePortalStations), STATION_SYNC_INTERVAL, STATION_SYNC_INTERVAL);
        }

        /// <summary>
        /// Tries to set up destination sync.
        /// </summary>
        private void Start() {
            InitDestinationSync();
        }

        /// <summary>
        /// Destroys the plugin.
        /// </summary>
        private void OnDestroy() {
            this.harmony.UnpatchSelf();
            this.destroyed = true;
            this.ClearCachedData();
        }

        /// <summary>
        /// Resets the plugin data.
        /// </summary>
        private void ClearCachedData() {
            this.destinationCache.Clear();
            this.sortedDestinations.Clear();
            this.tempZDOList.Clear();

            if (this.portalStationGUIInstance) {
                Destroy(this.portalStationGUIInstance.gameObject);
            }

            if (this.personalTeleportationDeviceGUIInstance) {
                Destroy(this.personalTeleportationDeviceGUIInstance.gameObject);
            }
        }

        /// <summary>
        /// Tries to gets all portal station names.
        /// </summary>
        /// <remarks>
        /// This will only yield all available portal stations on the server because
        /// the client does not sync the whole world.
        /// </remarks>
        private bool TryGetStationNames(ISet<string> set) {
            if (!TryGetPortalStationZDOs(this.tempZDOList)) {
                return false;
            }

            set.Clear();
            
            foreach (var zdo in this.tempZDOList) {
                set.Add(zdo.GetString(PortalStation.kPropStationName));
            }
            
            return true;
        }

        /// <summary>
        /// Tries to gets all portal station ZDOs.
        /// </summary>
        /// <remarks>
        /// This will only yield all available portal stations on the server because
        /// the client does not sync the whole world.
        /// </remarks>
        private bool TryGetPortalStationZDOs(ICollection<ZDO> list) {
            list.Clear();

            if (!this.portalStationPrefab || ZDOMan.instance == null) {
                return false;
            }

            var index = 0;
            while (ZDOMan.instance.GetAllZDOsWithPrefabIterative(this.portalStationPrefab.name, this.tempZDOList, ref index)) {
                // repeat until all portal station ZDOs are retrieved
            }
            return true;

        }

        /// <summary>
        /// Creates the portal station GUI.
        /// </summary>
        private PortalStationGUI CreatePortalStationGUI() {
            var instance = Instantiate(this.portalStationGUIPrefab);
            var rootTransform = instance.GetComponent<RectTransform>();
            rootTransform.SetParent(GUIManager.CustomGUIFront.transform, false);

            instance.gameObject.SetActive(false);

            return instance.GetComponent<PortalStationGUI>();
        }

        /// <summary>
        /// Creates the personal teleportation device GUI.
        /// </summary>
        private PersonalTeleportationDeviceGUI CreatePersonalTeleportationDeviceGUIInstance() {
            var instance = Instantiate(this.personalTeleportationDeviceGUIPrefab);
            var rootTransform = instance.GetComponent<RectTransform>();
            rootTransform.SetParent(GUIManager.CustomGUIFront.transform, false);

            instance.gameObject.SetActive(false);

            return instance.GetComponent<PersonalTeleportationDeviceGUI>();
        }

        /// <summary>
        /// Adds configuration keys.
        /// </summary>
        private void AddConfigKeys() {
            Config.SaveOnConfigSet = true;
            Config.ConfigReloaded += (_, _) => UpdateFuelItem();
            Config.SettingChanged += (_, _) => UpdateFuelItem();
            
            this.ignoreTeleportationRestrictionsConfigEntry = Config.Bind(
                CONFIG_SECTION_GENERAL,
                CONFIG_KEY_IGNORE_TELEPORTATION_RESTRICTIONS,
                CONFIG_KEY_IGNORE_TELEPORTATION_RESTRICTIONS_DEFAULT_VALUE,
                new ConfigDescription(CONFIG_KEY_IGNORE_TELEPORTATION_RESTRICTIONS_DESC, null, new ConfigurationManagerAttributes {
                    IsAdminOnly = true
                }));
            this.fuelItemNameConfigEntry = Config.Bind(
                CONFIG_SECTION_PERSONAL_TELEPORTATION_DEVICE,
                CONFIG_KEY_PERSONAL_TELEPORTATION_DEVICE_FUEL_ITEM_NAME,
                CONFIG_KEY_PERSONAL_TELEPORTATION_DEVICE_FUEL_ITEM_NAME_DEFAULT_VALUE,
                new ConfigDescription(CONFIG_KEY_PERSONAL_TELEPORTATION_DEVICE_FUEL_ITEM_NAME_DESC, null, new ConfigurationManagerAttributes {
                    IsAdminOnly = true
                }));
            this.fuelItemNameConfigEntry.SettingChanged += (_, _) => UpdateFuelItem();
            
            this.teleportationDistancePerFuelItemConfigEntry = Config.Bind(
                CONFIG_SECTION_PERSONAL_TELEPORTATION_DEVICE,
                CONFIG_KEY_TELEPORTATION_DISTANCE_PER_FUEL_ITEM,
                CONFIG_KEY_TELEPORTATION_DISTANCE_PER_FUEL_ITEM_DEFAULT_VALUE,
                new ConfigDescription(CONFIG_KEY_TELEPORTATION_DISTANCE_PER_FUEL_ITEM_DESC, null, new ConfigurationManagerAttributes {
                    IsAdminOnly = true
                }));
            this.additionalTravelDistancePerUpgradeConfigEntry = Config.Bind(
                CONFIG_SECTION_PERSONAL_TELEPORTATION_DEVICE,
                CONFIG_KEY_ADDITIONAL_TELEPORTATION_DISTANCE_PER_UPGRADE,
                CONFIG_KEY_ADDITIONAL_TELEPORTATION_DISTANCE_PER_UPGRADE_DEFAULT_VALUE,
                new ConfigDescription(CONFIG_KEY_ADDITIONAL_TELEPORTATION_DISTANCE_PER_UPGRADE_DESC, null, new ConfigurationManagerAttributes {
                    IsAdminOnly = true
                }));
        }
        
        /// <summary>
        /// Adds and configures the portal station piece.
        /// </summary>
        private void AddPortalStationPiece() {
            if (PieceManager.Instance.GetPiece("$piece_portal_station") != null) {
                return;
            }
            
            this.portalStationPrefab = this.assetBundle.LoadAsset<GameObject>("assets/portalstationassets/prefabs/portalstation.prefab");
            this.portalStationPrefab.AddComponent<PortalStation>();

            this.portalStationGUIPrefab = this.assetBundle.LoadAsset<GameObject>("assets/portalstationassets/prefabs/portalstation_gui.prefab");
            this.portalStationGUIPrefab.AddComponent<PortalStationGUI>();

            this.portalStationDestinationItemPrefab = this.assetBundle.LoadAsset<GameObject>("assets/portalstationassets/prefabs/portalstation_gui_stationitem.prefab");
            this.portalStationDestinationItemPrefab.AddComponent<DestinationItem>();

            var config = new PieceConfig {
                PieceTable = "Hammer",
                CraftingStation = "piece_workbench"
            };

            config.AddRequirement(new RequirementConfig("Stone", 20, 0, true));
            config.AddRequirement(new RequirementConfig("SurtlingCore", 4, 0, true));
            config.AddRequirement(new RequirementConfig("GreydwarfEye", 20, 0, true));

            this.portalStationPiece = new CustomPiece(this.portalStationPrefab.gameObject, true, config);
            PieceManager.Instance.AddPiece(portalStationPiece);
        }

        /// <summary>
        /// Adds and configures the personal teleportation device item.
        /// </summary>
        private void AddPersonalTeleportationDevice() {
            this.personalTeleportationDevicePrefab = this.assetBundle.LoadAsset<GameObject>("assets/portalstationassets/prefabs/item_personalteleportationdevice.prefab");
            
            this.personalTeleportationDeviceGUIPrefab = this.assetBundle.LoadAsset<GameObject>("assets/portalstationassets/prefabs/personalteleportationdevice_gui.prefab");
            this.personalTeleportationDeviceGUIPrefab.AddComponent<PersonalTeleportationDeviceGUI>();

            this.personalTeleportationDeviceGUIDestinationItemPrefab = this.assetBundle.LoadAsset<GameObject>("assets/portalstationassets/prefabs/personalteleportationdevice_gui_stationitem.prefab");
            this.personalTeleportationDeviceGUIDestinationItemPrefab.AddComponent<DestinationItem>();

            var config = new ItemConfig {
                CraftingStation = "forge",
                RepairStation = "forge",
                MinStationLevel = 3
            };
            
            config.AddRequirement(new RequirementConfig("SurtlingCore", 3, 1, true));
            config.AddRequirement(new RequirementConfig("LeatherScraps", 10, 15, true));
            config.AddRequirement(new RequirementConfig("IronNails", 10, 15, true));

            this.personalTeleportationDeviceItem = new CustomItem(this.personalTeleportationDevicePrefab.gameObject, true, config);
            ItemManager.Instance.AddItem(this.personalTeleportationDeviceItem);
        }

        /// <summary>
        /// Initializes the destination synchronization routine.
        /// </summary>
        private void InitDestinationSync() {
            try {
                if (ZRoutedRpc.instance == null) {
                    return;
                }

                ZRoutedRpc.instance.m_onNewPeer = (Action<long>)Delegate.Combine(ZRoutedRpc.instance.m_onNewPeer, new Action<long>(OnNewPeer));

                if (ZNet.instance.IsServer()) {
                    ZRoutedRpc.instance.Register(nameof(RPC_RequestStationList), RPC_RequestStationList);
                }
                
                ZRoutedRpc.instance.Register<ZPackage>(nameof(RPC_SyncStationList), RPC_SyncStationList);

                ClearCachedData();
                UpdateFuelItem();

                if (!ZNet.instance.IsServer()) {
                    return;
                }

                this.StopCoroutine(nameof(this.SyncAvailablePortalStationsIterative));
                this.StartCoroutine(nameof(this.SyncAvailablePortalStationsIterative));
            } catch (Exception ex) {
                Jotunn.Logger.LogError($"Unable to register RPCs: {ex.Message}");
            }
        }

        /// <summary>
        /// Updates the fuel item to use.
        /// </summary>
        private void UpdateFuelItem() {
            this.fuelItemName = this.fuelItemNameConfigEntry?.Value;
            
            if (String.IsNullOrEmpty(this.fuelItemName)) {
                this.fuelItem = null;
                this.useFuel = false;
                return;            
            }

            this.useFuel = true;

            if (ObjectDB.instance == null) {
                return;
            }

            var fuelItemGameObject = ObjectDB.instance.GetItemPrefab(this.fuelItemName);
            if (!fuelItemGameObject) {
                Jotunn.Logger.LogWarning($"Can't resolve fuel item with name \"{this.fuelItemName}\" - teleportation will not be possible.");
                this.fuelItem = null;
                return;
            }
            
            this.fuelItem = fuelItemGameObject ? fuelItemGameObject.GetComponentInChildren<ItemDrop>(true) : null;
            if (!this.fuelItem) {
                Jotunn.Logger.LogWarning($"Can't resolve fuel item drop with name \"{this.fuelItemName}\" - teleportation will not be possible.");
            }
            
            Jotunn.Logger.LogInfo($"Using {this.fuelItemName} as fuel item");
        }

        /// <summary>
        /// Sends the current destination list to new peers.
        /// </summary>
        private void OnNewPeer(long peer) {
            SendDestinationsToClient(peer);
        }

        /// <summary>
        /// Requests the server to send the current list of destinations to the sender.
        /// </summary>
        private void RPC_RequestStationList(long sender) {
            SendDestinationsToClient(sender);
        }

        /// <summary>
        /// Updates available destinations sent from server to a client.
        /// </summary>
        private void RPC_SyncStationList(long sender, ZPackage package) {
            if (ZNet.instance.IsServer()) {
                return;
            }
            
            this.sortedDestinations.Clear();
            this.destinationCache.Clear();

            var count = package.ReadInt();

            for (var i = 0; i < count; i++) {
                var destination = new PortalStation.Destination(package.ReadZDOID()) {
                    StationName = package.ReadString(),
                    Position = package.ReadVector3(),
                    Rotation = package.ReadQuaternion()
                };

                this.destinationCache[destination.ID] = destination;
                this.sortedDestinations.Add(destination);
            }
            
            ChangeDestinations.Invoke();
        }

        private IEnumerator SyncAvailablePortalStationsIterative() {
            while (!this.destroyed) {
                if (ZNet.instance != null && ZNet.instance.IsServer()) {
                    var index = 0;
                    bool done;

                    this.tempSyncList.Clear();
                    
                    do {
                        done = ZDOMan.instance.GetAllZDOsWithPrefabIterative(this.portalStationPrefab.name, this.tempSyncList, ref index);
                        yield return null;
                    } while (!done);

                    this.tempAvailablePortalStationZDOs.Clear();
                    foreach (var zdo in this.tempSyncList) {
                        this.tempAvailablePortalStationZDOs[zdo.m_uid] = zdo;
                    }

                    if (this.destinationCache.Count > 0 || this.tempAvailablePortalStationZDOs.Count > 0) {
                        var removeCount = 0;
                        var addCount = 0;
                        var updateCount = 0;

                        // Check for removed stations
                        // ReSharper disable once ForeachCanBePartlyConvertedToQueryUsingAnotherGetEnumerator
                        foreach (var item in this.destinationCache) {
                            if (this.tempAvailablePortalStationZDOs.ContainsKey(item.Key)) {
                                continue;
                            }

                            this.sortedDestinations.Remove(item.Value);
                            this.removedZDOs.Push(item.Key);
                            removeCount++;
                        }

                        // Remove items 
                        while (this.removedZDOs.Count > 0) {
                            var key = this.removedZDOs.Pop();
                            this.destinationCache.Remove(key);
                        }

                        // Check for added stations and update existing ones
                        foreach (var item in this.tempAvailablePortalStationZDOs) {
                            if (this.destinationCache.TryGetValue(item.Key, out var destination)) {
                                // Check for updated stations
                                if (destination.UpdateFromZDO()) {
                                    updateCount++;
                                }

                                continue;
                            }

                            destination = new PortalStation.Destination(item.Value);

                            this.destinationCache.Add(item.Key, destination);
                            this.sortedDestinations.Add(destination);

                            addCount++;
                        }

                        if (removeCount > 0 || addCount > 0 || updateCount > 0) {
                            // sort destination list
                            this.sortedDestinations.Sort(SortByStationName);

                            // update destination data
                            this.cachedDestinationPackage.Clear();
                            this.cachedDestinationPackage.Write(this.sortedDestinations.Count);

                            foreach (var destination in this.sortedDestinations) {
                                this.cachedDestinationPackage.Write(destination.ID);
                                this.cachedDestinationPackage.Write(destination.StationName);
                                this.cachedDestinationPackage.Write(destination.Position);
                                this.cachedDestinationPackage.Write(destination.Rotation);
                            }

                            // notify all peers about the change
                            SendDestinationsToClient(ZRoutedRpc.Everybody);

                            // notify ourself if we are client and server
                            ChangeDestinations.Invoke();
                        }
                    }
                }

                yield return new WaitForSeconds(STATION_SYNC_INTERVAL);
            }
        }

        /// <summary>
        /// Sorting method for sorting destinations by station name.
        /// </summary>
        private static int SortByStationName(PortalStation.Destination x, PortalStation.Destination y) {
            return String.CompareOrdinal(x.StationName, y.StationName);
        }

        /// <summary>
        /// Sends the current list of portal station destinations to the given client.
        /// </summary>
        private void SendDestinationsToClient(long target) {
            if (this.cachedDestinationPackage.Size() == 0) {
                return;
            }
            
            ZRoutedRpc.instance.InvokeRoutedRPC(target, nameof(RPC_SyncStationList), this.cachedDestinationPackage);
        }
    }
}

