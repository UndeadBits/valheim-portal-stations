using Jotunn.Managers;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace UndeadBits.ValheimMods.PortalStation {
    
    /// <summary>
    /// GUI which lets the user rename a station or teleport to other ones.
    /// </summary>
    public class PortalStationGUI : UIElementBase, ICancelHandler {
        private const float GUI_UPDATE_INTERVAL = 0.1f;

        private readonly LinkedList<DestinationItem> destinationItems = new LinkedList<DestinationItem>();

        private RectTransform destinationItemListRoot;
        private Humanoid currentUser;
        private PortalStation currentPortalStation;
        private InputField stationNameInput;


        /// <summary>
        /// Shows the GUI.
        /// </summary>
        public void Open(Humanoid user, PortalStation station) {
            Jotunn.Logger.LogInfo($"Opening GUI of station {station.GetStationName()}");

            this.currentUser = user;
            this.currentPortalStation = station;
            
            this.gameObject.SetActive(true);

            foreach (var item in this.destinationItems) {
                Destroy(item.gameObject);
            }
            
            this.destinationItems.Clear();

            PortalStationPlugin.Instance.RequestPortalStationDestinationsFromServer();
            
            ResetStationName();
            UpdateDestinationItems();

            GUIManager.BlockInput(true);
        }

        /// <summary>
        /// Closes the GUI.
        /// </summary>
        /// <param name="portalStation">The station which wants to close the GUI.</param>
        public void Close(PortalStation portalStation) {
            if (this.currentPortalStation != portalStation) {
                return;
            }

            Close();
        }

        private void Close() {
            GUIManager.BlockInput(false);
            
            this.currentUser = null;
            this.currentPortalStation = null;
            this.gameObject.SetActive(false);
        }
        
        /// <summary>
        /// Initializes the component.
        /// </summary>
        private void Awake() {
            Jotunn.Logger.LogInfo($"Initializing PortalStationGUI");

            this.destinationItemListRoot = RequireComponentByName<RectTransform>("$part_Content");

            var closeButton = RequireComponentByName<Button>("$part_CloseButton");
            if (closeButton) {
                closeButton.onClick.AddListener(this.Close);
            }

            this.stationNameInput = RequireComponentByName<InputField>("$part_PortalStationName");
            if (this.stationNameInput) {
                this.stationNameInput.onEndEdit.AddListener(OnEndEdit);
            }

            PortalStationPlugin.Instance.ChangeDestinations.AddListener(OnChangeDestinations);
            InvokeRepeating(nameof(UpdateGUIVisibility), GUI_UPDATE_INTERVAL, GUI_UPDATE_INTERVAL);
        }

        private void OnChangeDestinations() {
            if (!this.currentUser || !this.currentPortalStation || !this.gameObject.activeInHierarchy) {
                return;
            }
            
            UpdateDestinationItems();
        }

        /// <summary>
        /// Resets the station name input field.
        /// </summary>
        private void ResetStationName() {
            if (this.stationNameInput && this.currentPortalStation) {
                this.stationNameInput.text = this.currentPortalStation.GetStationName();
            }
        }

        /// <summary>
        /// Handles the "End Edit" event of the station name input field.
        /// </summary>
        /// <param name="value">The entered value</param>
        private void OnEndEdit(string value) {
            if (!this.currentUser || !this.currentPortalStation) {
                return;
            }

            if (!this.currentPortalStation.Rename(value)) {
                return;
            }

            UpdateDestinationItems();
        }

        /// <summary>
        /// Updates the GUI visibility.
        /// </summary>
        private void UpdateGUIVisibility() {
            if (!this.currentUser || !this.currentPortalStation || !this.currentPortalStation.InUseDistance(this.currentUser)) {
                Close();
                return;
            }
            
            var view = this.currentPortalStation.GetComponent<ZNetView>();
            if (!view || !view.IsValid()) {
                Close();
            }
        }
        
        /// <summary>
        /// Updates GUI visibility and destinations
        /// </summary>
        private void UpdateDestinationItems() {
            if (!this.currentUser || !this.currentPortalStation || !this.currentPortalStation.InUseDistance(this.currentUser)) {
                return;
            }

            var view = this.currentPortalStation.GetComponent<ZNetView>();
            if (!view || !view.IsValid()) {
                return;
            }
            
            var self = view.GetZDO();
            var stationCount = 0;
            var itemListHead = this.destinationItems.First;

            foreach (var destination in PortalStationPlugin.Instance.AvailableDestinations) {
                if (destination.id == self.m_uid) {
                    continue;
                }

                var destinationItem = itemListHead?.Value;
                if (destinationItem == null) {
                    destinationItem = PortalStationPlugin.Instance.CreateDestinationItem(this.destinationItemListRoot);
                    destinationItem.onClick.AddListener(OnRequestTeleportation);
                    
                    this.destinationItems.AddLast(destinationItem);
                } else {
                    itemListHead = itemListHead.Next;
                }

                stationCount++;
                destinationItem.Destination = destination;
            }

            while (this.destinationItems.Count > stationCount) {
                var item = this.destinationItems.Last;
                Destroy(item.Value.gameObject);
                this.destinationItems.RemoveLast();
            }
        }

        /// <summary>
        /// Teleports the given user to this portal.
        /// </summary>
        /// <param name="destination">The destination to teleport to</param>
        private void OnRequestTeleportation(PortalStation.Destination destination) {
            if (!this.currentUser) {
                return;
            }
            
            if (ZoneSystem.instance.GetGlobalKey("noportals")) {
                this.currentUser.Message(MessageHud.MessageType.Center, "$msg_blocked");
                return;
            }
            
            if (!this.currentUser.IsTeleportable()) {
                this.currentUser.Message(MessageHud.MessageType.Center, "$msg_noteleport");
                return;
            }

            var distance = Vector3.Distance(this.currentUser.transform.position, destination.position);
            var distant = distance >= ZoneSystem.instance.m_zoneSize;
            
            this.currentUser.TeleportTo(destination.position, destination.rotation, distant);
            Close();
        }

        public void OnCancel(BaseEventData eventData) {
            Close();
        }

        public void OnChangeStationName(PortalStation station) {
            if (this.currentPortalStation != station) {
                return;
            }
            
            ResetStationName();
            UpdateDestinationItems();
        }
    }
    
}
