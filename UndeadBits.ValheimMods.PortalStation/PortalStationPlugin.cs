using BepInEx;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using Jotunn.Utils;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace UndeadBits.ValheimMods.PortalStation {
    [BepInPlugin(PLUGIN_GUID, PLUGIN_NAME, PLUGIN_VERSION)]
    [BepInDependency(Jotunn.Main.ModGuid)]
    [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Minor)]
    internal class PortalStationPlugin : BaseUnityPlugin {
        private const string PLUGIN_GUID = "com.undeadbits.valheimmods.portalstation";
        private const string PLUGIN_NAME = "PortalStation";
        public const string PLUGIN_VERSION = "0.1.0";

        private AssetBundle assetBundle;
        private GameObject portalStationPrefab;
        private PieceConfig portalStationPieceConfig;
        private CustomPiece portalStationPiece;
        
        // Use this class to add your own localization to the game
        // https://valheim-modding.github.io/Jotunn/tutorials/localization.html
        private static readonly CustomLocalization Localization = LocalizationManager.Instance.GetLocalization();

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
        /// Generates a name for a station based on other stations in the world.
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
        /// Gets all portal station ZDOs.
        /// </summary>
        public IEnumerable<ZDO> GetPortalStationZDOs() {
            var prefabId = this.portalStationPrefab.name.GetStableHashCode();
            
            // ZoneSystem.instance.
            
            foreach (var zdo in ZDOMan.instance.GetSaveClone()) {
                if (zdo.m_prefab != prefabId) {
                    continue;
                }

                yield return zdo;
            }
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
                { "$piece_portal_station", "Portal Station" }
            });
        }
        
        /// <summary>
        /// Initializes the plugin.
        /// </summary>
        private void Awake() {
            Jotunn.Logger.LogInfo("Initializing PortalStation mod");
            
            Instance = this;
            
            AddPortalStationPiece();
            CreatePortalStationGUI();
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

            this.portalStationPieceConfig.AddRequirement(new RequirementConfig("Stone", 25, 0, true));
            this.portalStationPieceConfig.AddRequirement(new RequirementConfig("SurtlingCore", 3, 0, true));
            this.portalStationPieceConfig.AddRequirement(new RequirementConfig("GreydwarfEye", 20, 0, true));
              
            this.portalStationPiece = new CustomPiece(this.portalStationPrefab.gameObject, true, this.portalStationPieceConfig);
            PieceManager.Instance.AddPiece(portalStationPiece);
        }
    }
}

