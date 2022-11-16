using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using Jotunn.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
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
        public const string PLUGIN_VERSION = "0.9.0";
        
        private const float STATION_SYNC_INTERVAL = 1.0f;

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
        
        public static readonly CustomLocalization Localization = LocalizationManager.Instance.GetLocalization();
        
        private readonly Harmony harmony = new Harmony(PLUGIN_GUID); 

        private readonly List<PortalStation.Destination> availableDestinations = new List<PortalStation.Destination>();
        private readonly Dictionary<ZDOID, PortalStation.Destination> serverPortalStations = new Dictionary<ZDOID, PortalStation.Destination>();

        private readonly List<ZDO> tempZDOList = new List<ZDO>();
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


        #region Harmony Patches
        
        [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.UseItem))]
        // ReSharper disable once InconsistentNaming
        private class Humanoid_UseItem_Patch {
            [HarmonyPrefix]
            // ReSharper disable once InconsistentNaming
            // ReSharper disable once UnusedMember.Local
            // ReSharper disable once UnusedParameter.Local
            private static bool UseItem(Inventory inventory, ItemDrop.ItemData item, bool fromInventoryGui, Humanoid __instance) {
                if (item.m_shared.m_name != PersonalTeleportationDevice.ITEM_NAME) {
                    return true;
                }

                PersonalTeleportationDevice.UseItem(__instance, inventory, item);

                return false;
            }
        }
        
        [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.Awake))]
        // ReSharper disable once InconsistentNaming
        private class Humanoid_Awake_Patch {
            [HarmonyPrefix]
            // ReSharper disable once InconsistentNaming
            // ReSharper disable once UnusedMember.Local
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

        #endregion
        
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
        public readonly UnityEvent ChangeDestinations = new UnityEvent();

        /// <summary>
        /// Gets the available destinations to teleport to.
        /// </summary>
        public IReadOnlyList<PortalStation.Destination> AvailableDestinations {
            get { return this.availableDestinations; }
        }

        /// <summary>
        /// Gets the name of the fuel item to use for the personal teleportation device.
        /// </summary>
        public string FuelItemName {
            get { return this.fuelItemName; }
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
            var stationNames = new HashSet<string>(GetPortalStationZDOs().Select(x => x.GetString(PortalStation.PROP_STATION_NAME)));
            var stationNumber = 1;

            while (true) {
                var stationName = String.Format(Localization.TryTranslate("$auto_portal_station_name"), stationNumber++);
                if (stationNames.Contains(stationName)) {
                    continue;
                }

                return stationName;
            }
        }

        /// <summary>
        /// Creates a new teleport destination item.
        /// </summary>
        public DestinationItem CreatePSDestinationItem(RectTransform parent) {
            var instance = Instantiate(this.portalStationDestinationItemPrefab);
            instance.GetComponent<RectTransform>().SetParent(parent, false);

            return instance.GetComponent<DestinationItem>();
        }

        /// <summary>
        /// Creates a new teleport destination item.
        /// </summary>
        public DestinationItem CreatePTDDestinationItem(RectTransform parent) {
            var instance = Instantiate(this.personalTeleportationDeviceGUIDestinationItemPrefab);
            instance.GetComponent<RectTransform>().SetParent(parent, false);

            return instance.GetComponent<DestinationItem>();
        }

        /// <summary>
        /// Gets the portal station GUI instance.
        /// </summary>
        public PortalStationGUI GetPortalStationGUI() {
            if (!this.portalStationGUIInstance) {
                this.portalStationGUIInstance = CreatePortalStationGUI();
            }

            return this.portalStationGUIInstance;
        }

        /// <summary>
        /// Gets the personal teleportation device GUI instance.
        /// </summary>
        public PersonalTeleportationDeviceGUI GetPersonalTeleportationDeviceGUIInstance() {
            if (!this.personalTeleportationDeviceGUIInstance) {
                this.personalTeleportationDeviceGUIInstance = CreatePersonalTeleportationDeviceGUIInstance();
            }

            return this.personalTeleportationDeviceGUIInstance;
        }

        /// <summary>
        /// Requests current list of portal station destinations from the server,
        /// </summary>
        public void RequestPortalStationDestinationsFromServer() {
            ZRoutedRpc.instance.InvokeRoutedRPC(nameof(RPC_RequestStationList));
        }

        /// <summary>
        /// Gets a portal station by the given station Id.
        /// </summary>
        /// <param name="stationId">The stations id</param>
        /// <returns>The station or null if not found</returns>
        public PortalStation.Destination GetPortalStation(ZDOID stationId) {
            return this.availableDestinations.FirstOrDefault(x => x.id == stationId);
        }

        /// <summary>
        /// Determines whether the player can teleport.
        /// </summary>
        /// <param name="player">The player</param>
        /// <returns>Whether teleportation is allowed or not</returns>
        public bool CanTeleportPlayer(Humanoid player) {
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

            InvokeRepeating(nameof(SyncAvailablePortalStations), STATION_SYNC_INTERVAL, STATION_SYNC_INTERVAL);
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
            this.ClearCachedData();
        }

        /// <summary>
        /// Resets the plugin data.
        /// </summary>
        private void ClearCachedData() {
            this.serverPortalStations.Clear();
            this.availableDestinations.Clear();
            this.tempZDOList.Clear();

            if (this.portalStationGUIInstance) {
                Destroy(this.portalStationGUIInstance.gameObject);
            }

            if (this.personalTeleportationDeviceGUIInstance) {
                Destroy(this.personalTeleportationDeviceGUIInstance.gameObject);
            }
        }

        /// <summary>
        /// Gets all portal station ZDOs.
        /// This will only yield all available portal stations on the server because on the client only the current region is known.
        /// </summary>
        private IEnumerable<ZDO> GetPortalStationZDOs() {
            tempZDOList.Clear();

            if (this.portalStationPrefab && ZDOMan.instance != null) {
                ZDOMan.instance.GetAllZDOsWithPrefab(this.portalStationPrefab.name, tempZDOList);
            }

            return this.tempZDOList;
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
            Config.ConfigReloaded += (s, e) => UpdateFuelItem();
            Config.SettingChanged += (s, e) => UpdateFuelItem();
            
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
            this.fuelItemNameConfigEntry.SettingChanged += (s, e) => UpdateFuelItem();
            
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

            this.portalStationPrefab = this.assetBundle.LoadAsset<GameObject>("assets/prefabs/portalstation.prefab");
            this.portalStationPrefab.AddComponent<PortalStation>();

            this.portalStationGUIPrefab = this.assetBundle.LoadAsset<GameObject>("assets/prefabs/portalstation_gui.prefab");
            this.portalStationGUIPrefab.AddComponent<PortalStationGUI>();

            this.portalStationDestinationItemPrefab = this.assetBundle.LoadAsset<GameObject>("assets/prefabs/portalstation_gui_stationitem.prefab");
            this.portalStationDestinationItemPrefab.AddComponent<DestinationItem>();

            var config = new PieceConfig {
                PieceTable = "Hammer",
                CraftingStation = "piece_workbench",
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
            this.personalTeleportationDevicePrefab = this.assetBundle.LoadAsset<GameObject>("assets/prefabs/item_personalteleportationdevice.prefab");
            
            this.personalTeleportationDeviceGUIPrefab = this.assetBundle.LoadAsset<GameObject>("assets/prefabs/personalteleportationdevice_gui.prefab");
            this.personalTeleportationDeviceGUIPrefab.AddComponent<PersonalTeleportationDeviceGUI>();

            this.personalTeleportationDeviceGUIDestinationItemPrefab = this.assetBundle.LoadAsset<GameObject>("assets/prefabs/personalteleportationdevice_gui_stationitem.prefab");
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
                Jotunn.Logger.LogWarning($"No fuel item configured, personal teleportation device will not cost anything.");
                this.fuelItem = null;
                this.useFuel = false;
                return;            
            }

            this.useFuel = true;

            if (ObjectDB.instance == null) {
                Jotunn.Logger.LogWarning($"ObjectDB not ready.");
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
            this.availableDestinations.Clear();

            var count = package.ReadInt();

            for (var i = 0; i < count; i++) {
                var destination = new PortalStation.Destination(package.ReadZDOID()) {
                    stationName = package.ReadString(),
                    position = package.ReadVector3(),
                    rotation = package.ReadQuaternion(),
                };

                this.availableDestinations.Add(destination);
            }
            
            ChangeDestinations.Invoke();
        }

        /// <summary>
        /// Synchronizes portal stations from server to clients if necessary.
        /// </summary>
        private void SyncAvailablePortalStations() {
            if (ZNet.instance == null || !ZNet.instance.IsServer() && !ZNet.instance.IsDedicated()) {
                return;
            }
            
            var available = new HashSet<ZDO>(GetPortalStationZDOs(), new ZDOComparer());
            var availableZDOIds = new HashSet<ZDOID>(available.Select(x => x.m_uid));
            if (this.serverPortalStations.Count == 0 && available.Count == 0) {
                return;
            }
            
            var current = new HashSet<ZDOID>(this.serverPortalStations.Keys);
            var removeCount = 0;
            var addCount = 0;
            var updateCount = 0;
            
            // Check for removed stations
            foreach (var item in current) {
                if (availableZDOIds.Contains(item)) {
                    continue;
                }

                this.serverPortalStations.Remove(item);
                removeCount++;
            }
            
            // Check for added stations
            foreach (var item in available) {
                if (current.Contains(item.m_uid)) {
                    continue;
                }

                this.serverPortalStations.Add(item.m_uid, new PortalStation.Destination(item));

                addCount++;
            }
            
            // Check for updated stations
            foreach (var item in this.serverPortalStations) {
                if (item.Value.UpdateFromZDO()) {
                    updateCount++;
                }
            }

            if (removeCount > 0 || addCount > 0 || updateCount > 0) {
                SendDestinationsToClient(ZRoutedRpc.Everybody);
            }
        }

        /// <summary>
        /// Sends the current list of portal station destinations to the given client.
        /// </summary>
        private void SendDestinationsToClient(long target) {
            var destinations = this.serverPortalStations.Values   
                .OrderBy(x => x.stationName ?? "")
                .ToList();
            
            var package = new ZPackage();
            package.Write(destinations.Count);

            foreach (var destination in destinations) {
                package.Write(destination.id);
                package.Write(destination.stationName);
                package.Write(destination.position);
                package.Write(destination.rotation);
            }
            
            ZRoutedRpc.instance.InvokeRoutedRPC(target, nameof(RPC_SyncStationList), package);
            
            this.availableDestinations.Clear();
            this.availableDestinations.AddRange(destinations);
            
            ChangeDestinations.Invoke();
        }
    }
}

