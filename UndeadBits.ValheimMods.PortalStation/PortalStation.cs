using Jotunn.Managers;
using System;
using UnityEngine;

namespace UndeadBits.ValheimMods.PortalStation {
    
    /// <summary>
    /// Logic for portal station objects.
    /// </summary>
    public class PortalStation : MonoBehaviour, Interactable, Hoverable {
        public const string PROP_STATION_NAME = "stationName";
        private const float USE_DISTANCE = 5.0f;
        
        private ZNetView view;

        /// <summary>
        /// Unused.
        /// </summary>
        public bool UseItem(Humanoid user, ItemDrop.ItemData item) {
            return false;
        }
        
        /// <summary>
        /// Gets the hover text for the portal station.
        /// </summary>
        public string GetHoverText() {
            return Localization.instance.Localize("[<color=yellow><b>$KEY_Use</b></color>] Use station");
        }
        
        /// <summary>
        /// Unused.
        /// </summary>
        public string GetHoverName() {
            return "";
        }
        
        /// <summary>
        /// Notifies the portal station about a user which wants to interact with it.
        /// </summary>
        /// <param name="user"></param>
        /// <param name="hold"></param>
        /// <param name="alt"></param>
        /// <returns></returns>
        public bool Interact(Humanoid user, bool hold, bool alt) {
            if (hold) {
                return false;
            }

            if (user == Player.m_localPlayer) {
                if (!InUseDistance(user)) {
                    Jotunn.Logger.LogInfo($"Portal station not in range.");
                    return false;
                }
                
                return OpenGUI(user);
            }
            
            return false;
        }

        /// <summary>
        /// Determines whether a station is in range to be used.
        /// </summary>
        /// <param name="human">The human which wants to interact with the station.</param>
        public bool InUseDistance(Humanoid human) {
            return Vector3.Distance(human.transform.position, this.transform.position) <= USE_DISTANCE;
        }

        /// <summary>
        /// Gets the station name.
        /// </summary>
        public string GetStationName() {
            var zdo = this.view == null ? null : this.view.GetZDO();
            return zdo == null ? "" : zdo.GetString(PROP_STATION_NAME);
        }
        
        /// <summary>
        /// Renames the station.
        /// </summary>
        public bool Rename(string value) {
            if (String.IsNullOrWhiteSpace(value)) {
                return false;
            }

            if (this.view.IsValid()) {
                this.view.InvokeRPC("SetStationName", value);
                return true;
            }

            return false;
        }
        
        /// <summary>
        /// Initializes the portal station.
        /// </summary>
        private void Awake() {
            this.view = GetComponent<ZNetView>();

            var zdo = this.view == null ? null : this.view.GetZDO();
            if (zdo == null) {
                this.enabled = false;
                return;
            }

            if (this.view.IsOwner() && zdo.GetString(PROP_STATION_NAME, null) == null) {
                zdo.Set(PROP_STATION_NAME, PortalStationPlugin.Instance.CreateStationName());
                Jotunn.Logger.LogInfo($"Portal station \"{zdo.GetString(PROP_STATION_NAME)}\" created.");
            } else {
                Jotunn.Logger.LogInfo($"Portal station \"{zdo.GetString(PROP_STATION_NAME)}\" loaded.");
            }
            
            this.view.Register<string>("SetStationName", RPC_SetStationName);
        }

        /// <summary>
        /// Handles the "SetStationName" RPC.
        /// </summary>
        private void RPC_SetStationName(long sender, string value) {
            if (this.view.IsValid() && this.view.IsOwner() && GetStationName() != value) {
                this.view.GetZDO().Set(PROP_STATION_NAME, value);
            }
            
            if (GUIManager.IsHeadless()) {
                return;
            }
            
            var stationGUI = PortalStationPlugin.Instance.GetPortalStationGUI();
            if (stationGUI) {
                stationGUI.OnChangeStationName(this);
            }
        }
        
        /// <summary>
        /// Destroys the station.
        /// </summary>
        private void OnDestroy() {
            CloseGUI();
        }

        /// <summary>
        /// Opens the stations GUI.
        /// </summary>
        /// <param name="user">The user which interacts with the station.</param>
        private bool OpenGUI(Humanoid user) {
            if (GUIManager.IsHeadless()) {
                return false;
            }
            
            var stationGUI = PortalStationPlugin.Instance.GetPortalStationGUI();
            if (stationGUI) {
                Jotunn.Logger.LogInfo($"Opening GUI of station {GetStationName()}");

                stationGUI.Open(user, this);
            }
            
            return true;
        }
        
        /// <summary>
        /// Closes the stations GUI.
        /// </summary>
        private void CloseGUI() {
            var stationGUI = PortalStationPlugin.Instance.GetPortalStationGUI();
            if (stationGUI) {
                stationGUI.Close(this);
            }
        }

    }
}
