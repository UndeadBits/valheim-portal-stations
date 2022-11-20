using Jotunn.Managers;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace UndeadBits.ValheimMods.PortalStation {
    
    /// <summary>
    /// Base class for teleportation GUIs.
    /// </summary>
    public abstract class BaseTeleportationGUI : UIElementBase {
        private const float GUI_UPDATE_INTERVAL = 1.0f;
        
        private readonly LinkedList<DestinationItem> destinationItems = new LinkedList<DestinationItem>();
        private readonly List<PortalStation.Destination> cachedDestinationList = new List<PortalStation.Destination>();

        private Humanoid currentUser;
        private ZNetView currentUserZNetView;
        private RectTransform destinationItemListRoot;
        private ScrollRect scrollRect;
        private bool blockUserInput;

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
        /// Gets the user which is currently using the GUI.
        /// </summary>
        protected Humanoid CurrentUser {
            get { return this.currentUser; }
        }

        /// <summary>
        /// Gets the ZNetView of the user which is currently using the GUI.
        /// </summary>
        protected ZNetView CurrentUserView {
            get { return this.currentUserZNetView; }
        }
        
        /// <summary>
        /// Shows the GUI.
        /// </summary>
        protected void OpenGUI(Humanoid user) {
            this.currentUser = user;
            this.currentUserZNetView = user.m_nview;

            this.gameObject.SetActive(true);

            foreach (var item in this.destinationItems) {
                Destroy(item.gameObject);
            }
            
            this.destinationItems.Clear();
            this.scrollRect.verticalNormalizedPosition = 1;
            this.scrollRect.horizontalNormalizedPosition = 1;

            PortalStationPlugin.Instance.RequestPortalStationDestinationsFromServer();

            UpdateDestinationList(this.cachedDestinationList);
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
            this.destinationItemListRoot = RequireComponentByName<RectTransform>("$part_Content");
            this.scrollRect = RequireComponentByName<ScrollRect>("$part_ScrollView");

            var closeButton = RequireComponentByName<Button>("$part_CloseButton");
            if (closeButton) {
                closeButton.onClick.AddListener(this.Close);
            }

            PortalStationPlugin.Instance.ChangeDestinations.AddListener(OnChangeDestinations);
            InvokeRepeating(nameof(this.UpdateGUI), GUI_UPDATE_INTERVAL, GUI_UPDATE_INTERVAL);
        }

        /// <summary>
        /// Updates the GUI visibility.
        /// </summary>
        protected virtual void UpdateGUI() {
            if (!this.currentUser) {
                Close();
                return;
            }

            foreach (var destinationItem in this.destinationItems) {
                destinationItem.UpdateGUI(this);
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
        /// Updates the list of destinations to which the user can travel.
        /// </summary>
        protected virtual void UpdateDestinationList(List<PortalStation.Destination> destinations) {
            destinations.Clear();
            destinations.AddRange(PortalStationPlugin.Instance.AvailableDestinations);
        }

        /// <summary>
        /// Updates GUI visibility and destinations
        /// </summary>
        protected void UpdateDestinationItems() {
            if (!this.currentUser) {
                return;
            }

            var stationCount = 0;
            var itemListHead = this.destinationItems.First;
            
            foreach (var destination in this.cachedDestinationList) {
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
                destinationItem.SetDestination(this, destination);
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
        protected virtual bool CanTeleport(PortalStation.Destination destination) {
            if (!this.currentUser) {
                return false;
            }

            return PortalStationPlugin.Instance.CanTeleportPlayer(this.currentUser);
        }

        /// <summary>
        /// Teleports the a player to the given destination.
        /// </summary>
        /// <param name="player">The player to teleport</param>
        /// <param name="destination">The desired destination</param>
        protected abstract void TeleportTo(Humanoid player, PortalStation.Destination destination);

        /// <summary>
        /// Teleports the given user to this portal.
        /// </summary>
        /// <param name="destination">The destination to teleport to</param>
        private void OnRequestTeleportation(PortalStation.Destination destination) {
            if (!CanTeleport(destination)) {
                return;
            }

            TeleportTo(this.currentUser, destination);
            Close();
        }

        /// <summary>
        /// Handles a change to available destinations.
        /// </summary>
        private void OnChangeDestinations() {
            if (!this.currentUser || !this.gameObject.activeInHierarchy) {
                return;
            }
            
            UpdateDestinationList(this.cachedDestinationList);
            UpdateDestinationItems();
        }

    }
    
}
