using Jotunn.Managers;
using System;
using UnityEngine;

namespace UndeadBits.ValheimMods.PortalStation {
    
    /// <summary>
    /// Logic for portal station objects.
    /// </summary>
    public class PortalStation : MonoBehaviour, Interactable, Hoverable {
        public static readonly int PROP_STATION_NAME = "stationName".GetStableHashCode();
        public const float USE_DISTANCE = 5.0f;

        private const float PORTAL_EXIT_DISTANCE = 1.0f;

        private ZNetView view;
        

        #region Inner Types
        
        public class Destination : IEquatable<Destination> {
            
            /// <summary>
            /// The ZDOID of the portal which this destination represents.
            /// </summary>
            public readonly ZDOID id;
        
            /// <summary>
            /// The station name.
            /// </summary>
            public string stationName;
        
            /// <summary>
            /// The teleportation position.
            /// </summary>
            public Vector3 position;

            /// <summary>
            /// The teleportation rotation.
            /// </summary>
            public Quaternion rotation;

            /// <summary>
            /// The stations ZDO object.
            /// Only available on the server.
            /// </summary>
            public ZDO stationZDO;

            /// <summary>
            /// Initializes a new instance of the <see cref="Destination"/> type.
            /// </summary>
            /// <param name="id">The id to set</param>
            public Destination(ZDOID id) {
                this.id = id;
            }
            
            /// <summary>
            /// Initializes a new instance of the <see cref="Destination"/> type.
            /// </summary>
            /// <param name="stationZDO">The ZDO to copy from</param>
            public Destination(ZDO stationZDO) {
                this.id = stationZDO.m_uid;
                this.stationZDO = stationZDO;

                UpdateInternal(true);
            }

            /// <summary>
            /// Updates position, rotation and name from the given ZDO.
            /// </summary>
            /// <param name="stationZDO">The ZDO to copy from</param>
            /// <returns>Whether changes where detected</returns>
            public bool UpdateFromZDO() {
                if (this.stationZDO == null) {
                    return false;
                }

                return UpdateInternal(false);
            }
            
            private bool UpdateInternal(bool force) {
                var updated = false;
                var newStationName = this.stationZDO.GetString(PROP_STATION_NAME);
                if (force || newStationName != this.stationName) {
                    this.stationName = newStationName;
                    updated = true;
                }

                if (force || Quaternion.Angle(this.rotation, stationZDO.GetRotation()) > 0.1f) {
                    this.rotation = stationZDO.GetRotation();
                    updated = true;
                }

                var teleportPos = this.stationZDO.GetPosition() + this.rotation * Vector3.forward * PORTAL_EXIT_DISTANCE + Vector3.up;
                if (force || Vector3.Distance(this.position, teleportPos) > 0.1f) {
                    this.position = teleportPos;
                    updated = true;
                }

                return updated;
            }

            #region Auto Generated Code
            
            public bool Equals(Destination other)
            {
                if (ReferenceEquals(null, other)) {
                    return false;
                }

                if (ReferenceEquals(this, other)) {
                    return true;
                }

                return this.id.Equals(other.id);
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) {
                    return false;
                }

                if (ReferenceEquals(this, obj)) {
                    return true;
                }

                if (obj.GetType() != this.GetType()) {
                    return false;
                }

                return Equals((Destination)obj);
            }

            public override int GetHashCode()
            {
                return this.id.GetHashCode();
            }

            public static bool operator ==(Destination left, Destination right)
            {
                return Equals(left, right);
            }

            public static bool operator !=(Destination left, Destination right)
            {
                return !Equals(left, right);
            }

            #endregion

        }

        #endregion
        
        /// <summary>
        /// Gets the stations id.
        /// </summary>
        public ZDOID StationId {
            get { return this.view ? this.view.m_zdo.m_uid : ZDOID.None; }
        }

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
        /// <param name="user">The user who interacts with the station</param>
        /// <param name="hold">Whether the use key is being held down</param>
        /// <param name="alt">Whether the alt key is being held down</param>
        /// <returns></returns>
        public bool Interact(Humanoid user, bool hold, bool alt) {
            if (hold) {
                return false;
            }

            if (user != Player.m_localPlayer) {
                return false;
            }

            return InUseDistance(user) && OpenGUI(user);
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

            if (!this.view.IsValid()) {
                return false;
            }

            this.view.InvokeRPC(nameof(this.RPC_SetStationName), value);
            return true;
        }
        
        /// <summary>
        /// Gets a destination for this station.
        /// </summary>
        public Destination AsDestination() {
            var destination = PortalStationPlugin.Instance.GetPortalStation(StationId);
            if (destination == null && this.view != null) {
                Jotunn.Logger.LogWarning("Creating temporary destination");
                destination = new Destination(this.view.m_zdo);
            }

            return destination;
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

            if (zdo.IsOwner() && String.IsNullOrEmpty(zdo.GetString(PROP_STATION_NAME, null))) {
                zdo.Set(PROP_STATION_NAME, PortalStationPlugin.Instance.CreateStationName());
            }

            this.view.Register<string>(nameof(RPC_SetStationName), RPC_SetStationName);
        }
        
        /// <summary>
        /// Handles the "SetStationName" RPC.
        /// </summary>
        private void RPC_SetStationName(long sender, string value) {
            if (!this.view.IsValid() || !this.view.IsOwner() || GetStationName() == value) {
                return;
            }

            this.view.GetZDO().Set(PROP_STATION_NAME, value);

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
