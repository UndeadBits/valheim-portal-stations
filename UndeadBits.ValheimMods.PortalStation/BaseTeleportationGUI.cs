using Jotunn.Managers;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace UndeadBits.ValheimMods.PortalStation {
    
    public abstract class BaseTeleportationGUI : UIElementBase {
        private const float GUI_UPDATE_INTERVAL = 0.1f;
        
        private readonly LinkedList<DestinationItem> destinationItems = new LinkedList<DestinationItem>();
        private Humanoid currentUser;
        private RectTransform destinationItemListRoot;
        private bool blockUserInput;
        private bool noTeleportPrevention = true;

        /// <summary>
        /// Gets the current user.
        /// </summary>
        public Humanoid CurrentUser {
            get { return this.currentUser; }
        }

        /// <summary>
        /// Gets the amount of fuel available.
        /// </summary>
        public virtual int GetFuelAmount() {
            return Int32.MaxValue;
        }

        /// <summary>
        /// Calculates the fuel cost to travel to the given destination.
        /// </summary>
        /// <returns></returns>
        public virtual int CalculateFuelCost(PortalStation.Destination destination) {
            return 0;
        }

        /// <summary>
        /// Shows the GUI.
        /// </summary>
        protected void OpenGUI(Humanoid user) {
            Jotunn.Logger.LogInfo($"Opening teleportation GUI");

            this.currentUser = user;
            
            this.gameObject.SetActive(true);

            foreach (var item in this.destinationItems) {
                Destroy(item.gameObject);
            }
            
            this.destinationItems.Clear();

            PortalStationPlugin.Instance.RequestPortalStationDestinationsFromServer();
            
            UpdateDestinationItems();

            if (!this.blockUserInput) {
                this.blockUserInput = true;
                GUIManager.BlockInput(true);
            }
        }
        
        /// <summary>
        /// Closes the GUI
        /// </summary>
        protected virtual void Close() {
            this.currentUser = null;
            this.gameObject.SetActive(false);

            if (this.blockUserInput) {
                this.blockUserInput = false;
                GUIManager.BlockInput(false);
            }
        }

        /// <summary>
        /// Initializes the component.
        /// </summary>
        protected virtual void Awake() {
            Jotunn.Logger.LogInfo($"Initializing PortalStationGUI");

            this.destinationItemListRoot = RequireComponentByName<RectTransform>("$part_Content");

            var closeButton = RequireComponentByName<Button>("$part_CloseButton");
            if (closeButton) {
                closeButton.onClick.AddListener(this.Close);
            }

            PortalStationPlugin.Instance.ChangeDestinations.AddListener(OnChangeDestinations);
            InvokeRepeating(nameof(UpdateGUIVisibility), GUI_UPDATE_INTERVAL, GUI_UPDATE_INTERVAL);
        }

        /// <summary>
        /// Updates the GUI visibility.
        /// </summary>
        protected virtual void UpdateGUIVisibility() {
            if (!this.currentUser) {
                Close();
                return;
            }
        }

        /// <summary>
        /// Can be overriden to filter destinations out.
        /// </summary>
        protected virtual bool FilterDestination(PortalStation.Destination destination) {
            return true;
        }

        /// <summary>
        /// Creates a new teleport destination item.
        /// </summary>
        protected abstract DestinationItem CreateDestinationItem(RectTransform parent);

        /// <summary>
        /// Updates GUI visibility and destinations
        /// </summary>
        protected void UpdateDestinationItems() {
            if (!this.currentUser) {
                return;
            }

            var stationCount = 0;
            var itemListHead = this.destinationItems.First;

            foreach (var destination in PortalStationPlugin.Instance.AvailableDestinations) {
                if (!FilterDestination(destination)) {
                    continue;
                }
                
                var destinationItem = itemListHead?.Value;
                if (destinationItem == null) {
                    destinationItem = CreateDestinationItem(this.destinationItemListRoot);
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
        /// Prepares teleportation to the given destination.
        /// </summary>
        /// <param name="destination">The destination to teleport to</param>
        /// <returns>Whether teleportation is possible or not</returns>
        protected virtual bool PrepareTeleportation(PortalStation.Destination destination) {
            if (!this.currentUser) {
                return false;
            }
            
            if (ZoneSystem.instance.GetGlobalKey("noportals")) {
                this.currentUser.Message(MessageHud.MessageType.Center, "$msg_blocked");
                return false;
            }
            
            if (!this.noTeleportPrevention && !this.currentUser.IsTeleportable()) {
                this.currentUser.Message(MessageHud.MessageType.Center, "$msg_noteleport");
                return false;
            }
            
            return true;
        }
        
        /// <summary>
        /// Teleports the given user to this portal.
        /// </summary>
        /// <param name="destination">The destination to teleport to</param>
        private void OnRequestTeleportation(PortalStation.Destination destination) {
            var distance = Vector3.Distance(this.currentUser.transform.position, destination.position);
            var distant = distance >= ZoneSystem.instance.m_zoneSize;

            if (!PrepareTeleportation(destination)) {
                return;
            }

            this.currentUser.TeleportTo(destination.position, destination.rotation, distant);

            Close();
        }

        private void OnChangeDestinations() {
            if (!this.currentUser || !this.gameObject.activeInHierarchy) {
                return;
            }
            
            UpdateDestinationItems();
        }

    }
    
}
