using BepInEx;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using Jotunn.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.Events;

namespace UndeadBits.ValheimMods.PortalStation {
    [BepInPlugin(PLUGIN_GUID, PLUGIN_NAME, PLUGIN_VERSION)]
    [BepInDependency(Jotunn.Main.ModGuid)]
    [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Minor)]
    internal class PortalStationPlugin : BaseUnityPlugin {
        private const string PLUGIN_GUID = "com.undeadbits.valheimmods.portalstation";
        private const string PLUGIN_NAME = "PortalStation";
        public const string PLUGIN_VERSION = "0.4.2";

        // Use this class to add your own localization to the game
        // https://valheim-modding.github.io/Jotunn/tutorials/localization.html
        private static readonly CustomLocalization Localization = LocalizationManager.Instance.GetLocalization();

        private readonly List<PortalStation.Destination> availableDestinations = new List<PortalStation.Destination>();
        private readonly Dictionary<ZDO, PortalStation.Destination> serverPortalStations = new Dictionary<ZDO, PortalStation.Destination>(new ZDOComparer());
        private readonly List<ZDO> tempZDOList = new List<ZDO>();
        private AssetBundle assetBundle;
        private GameObject portalStationPrefab;
        private PieceConfig portalStationPieceConfig;
        private CustomPiece portalStationPiece;

        private GameObject portalStationGUIPrefab;
        private GameObject portalStationDestinationItemPrefab;
        private PortalStationGUI portalStationGUIInstance;
        
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
        /// Generates a name for a station based on other stations in the world.
        ///
        /// FIXME: This must be done on the server side
        /// </summary>
        public string CreateStationName() {
            var stationNames = new HashSet<string>(GetPortalStationZDOs().Select(x => x.GetString(PortalStation.PROP_STATION_NAME)));
            var stationNumber = 1;

            while (true) {
                var stationName = $"PS_{stationNumber++:000}";
                if (stationNames.Contains(stationName)) {
                    continue;
                }

                return stationName;
            }
        }

        /// <summary>
        /// Creates a new teleport destination item.
        /// </summary>
        public DestinationItem CreateDestinationItem(RectTransform parent) {
            var instance = Instantiate(this.portalStationDestinationItemPrefab);
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
        /// Requests current list of portal station destinations from the server,
        /// </summary>
        public void RequestPortalStationDestinationsFromServer() {
            Jotunn.Logger.LogInfo($"RequestPortalStations");
            ZRoutedRpc.instance.InvokeRoutedRPC(nameof(RPC_RequestStationList));
        }
        
        /// <summary>
        /// Adds localizations.
        /// </summary>
        static PortalStationPlugin() {
            AddLocalizations();
        }

        /// <summary>
        /// Adds hard-coded localizations.
        /// </summary>
        private static void AddLocalizations() {
            Localization.AddTranslation("English", new Dictionary<string, string> {
                {
                    "$piece_portal_station", "Portal Station"
                }
            });
        }

        /// <summary>
        /// Initializes the plugin.
        /// </summary>
        private void Awake() {
            Jotunn.Logger.LogInfo($"Initializing PortalStations v{PLUGIN_VERSION}");
            
            Instance = this;
            PrefabManager.OnPrefabsRegistered += InitDestinationSync;

            AddPortalStationPiece();
            
            InvokeRepeating(nameof(SyncAvailablePortalStations), 0, 0.2f);
        }

        /// <summary>
        /// Tries to set up destination sync.
        /// </summary>
        private void Start() {
            InitDestinationSync();
        }

        /// <summary>
        /// Gets all portal station ZDOs.
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
        /// Adds the portal station piece.
        /// </summary>
        private void AddPortalStationPiece() {
            if (PieceManager.Instance.GetPiece("$piece_portal_station") != null) {
                return;
            }

            Jotunn.Logger.LogInfo("Adding portal station piece..");

            this.assetBundle = AssetUtils.LoadAssetBundleFromResources("Assets.portal_station_assets");
            this.portalStationPrefab = this.assetBundle.LoadAsset<GameObject>("assets/prefabs/portalstation.prefab");
            this.portalStationPrefab.AddComponent<PortalStation>();

            this.portalStationGUIPrefab = this.assetBundle.LoadAsset<GameObject>("assets/prefabs/portalstation_gui.prefab");
            this.portalStationGUIPrefab.AddComponent<PortalStationGUI>();

            this.portalStationDestinationItemPrefab = this.assetBundle.LoadAsset<GameObject>("assets/prefabs/portalstation_gui_stationitem.prefab");
            this.portalStationDestinationItemPrefab.AddComponent<DestinationItem>();

            this.portalStationPieceConfig = new PieceConfig {
                PieceTable = "Hammer",
                CraftingStation = "piece_workbench",
            };

            this.portalStationPieceConfig.AddRequirement(new RequirementConfig("Stone", 20, 0, true));
            this.portalStationPieceConfig.AddRequirement(new RequirementConfig("SurtlingCore", 4, 0, true));
            this.portalStationPieceConfig.AddRequirement(new RequirementConfig("GreydwarfEye", 20, 0, true));

            this.portalStationPiece = new CustomPiece(this.portalStationPrefab.gameObject, true, this.portalStationPieceConfig);
            PieceManager.Instance.AddPiece(portalStationPiece);
        }

        /// <summary>
        /// Initializes the destination synchronization routine.
        /// </summary>
        private void InitDestinationSync() {
            try {
                if (ZRoutedRpc.instance == null) {
                    Jotunn.Logger.LogInfo("ZRoutedRpc instance not ready.");
                    return;
                }

                ZRoutedRpc.instance.m_onNewPeer = (Action<long>)Delegate.Combine(ZRoutedRpc.instance.m_onNewPeer, new Action<long>(OnNewPeer));

                if (ZNet.instance.IsServer()) {
                    ZRoutedRpc.instance.Register(nameof(RPC_RequestStationList), RPC_RequestStationList);
                } else {
                    ZRoutedRpc.instance.Register<ZPackage>(nameof(RPC_SyncStationList), RPC_SyncStationList);
                }
                
                Jotunn.Logger.LogInfo("Added routed RPCs.");
            } catch (Exception ex) {
                Jotunn.Logger.LogError($"Unable to register RPCs: {ex.Message}");
            }
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
            if (this.serverPortalStations.Count == 0 && available.Count == 0) {
                return;
            }
            
            var current = new HashSet<ZDO>(this.serverPortalStations.Keys, new ZDOComparer());
            var removeCount = 0;
            var addCount = 0;
            var updateCount = 0;
            
            // Check for removed stations
            foreach (var item in current) {
                if (available.Contains(item)) {
                    continue;
                }

                this.serverPortalStations.Remove(item);
                removeCount++;
            }
            
            // Check for added stations
            foreach (var item in available) {
                if (current.Contains(item)) {
                    continue;
                }

                this.serverPortalStations.Add(item, new PortalStation.Destination(item));

                addCount++;
            }
            
            // Check for updated stations
            foreach (var item in this.serverPortalStations) {
                if (item.Value.UpdateFromZDO(item.Key)) {
                    updateCount++;
                }
            }

            if (removeCount > 0 || addCount > 0 || updateCount > 0) {
                Jotunn.Logger.LogInfo($"Update stations (count: {this.serverPortalStations.Count}, new: {addCount}, removed: {removeCount}, updated: {updateCount})");
                SendDestinationsToClient(ZRoutedRpc.Everybody);
            }
        }

        /// <summary>
        /// Sends the current list of portal station destinations to the given client.
        /// </summary>
        private void SendDestinationsToClient(long target) {
            var destinations = this.serverPortalStations.Keys.Select(x => new PortalStation.Destination(x)).ToList();

            Jotunn.Logger.LogInfo($"Sync stations to ({target})");

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

