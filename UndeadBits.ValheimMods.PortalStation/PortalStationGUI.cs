using Jotunn.Managers;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace UndeadBits.ValheimMods.PortalStation {
    
    /// <summary>
    /// Logic for portal station GUI.
    /// </summary>
    public class PortalStationGUI : BaseTeleportationGUI {
        private PortalStation currentPortalStation;
        private InputField stationNameInput;

        /// <summary>
        /// Shows the GUI.
        /// </summary>
        public void Open(Humanoid user, PortalStation station) {
            this.currentPortalStation = station;
            
            OpenGUI(user);
            
            ResetStationName();
        }

        /// <summary>
        /// Closes the GUI.
        /// </summary>
        /// <param name="station">The station which wants to close the GUI.</param>
        public void Close(PortalStation station) {
            if (this.currentPortalStation != station) {
                return;
            }

            Close();
        }

        /// <summary>
        /// Invoked when a station name has changed.
        /// </summary>
        /// <param name="station">The station which changed it's name</param>
        public void OnChangeStationName(PortalStation station) {
            if (this.currentPortalStation != station) {
                return;
            }
            
            ResetStationName();
            UpdateDestinationItems();
        }
                
        /// <summary>
        /// Closes the GUI
        /// </summary>
        protected override void Close() {
            base.Close();

            this.currentPortalStation = null;
        }

        /// <summary>
        /// Creates a new teleport destination item.
        /// </summary>
        protected override DestinationItem CreateDestinationItem(RectTransform parent) {
            return PortalStationPlugin.Instance.CreatePSDestinationItem(parent);
        }

        /// <summary>
        /// Initializes the component.
        /// </summary>
        protected override void Awake() {
            base.Awake();
            
            this.stationNameInput = RequireComponentByName<InputField>("$part_PortalStationName", false);
            if (this.stationNameInput) {
                this.stationNameInput.onEndEdit.AddListener(OnEndEdit);
            }
        }

        /// <summary>
        /// Updates the GUI visibility.
        /// </summary>
        protected override void UpdateGUI() {
            base.UpdateGUI();

            var currentUser = CurrentUser;
            if (!currentUser || !this.currentPortalStation || !this.currentPortalStation.InUseDistance(currentUser)) {
                Close();
                return;
            }
            
            var view = this.currentPortalStation.GetComponent<ZNetView>();
            if (!view || !view.IsValid()) {
                Close();
            }
        }

        /// <summary>
        /// Teleports the a player to the given destination.
        /// </summary>
        /// <param name="player">The player to teleport</param>
        /// <param name="destination">The desired destination</param>
        protected override void TeleportTo(Humanoid player, PortalStation.Destination destination) {
            var user = player.GetComponent<PortalStationUser>();
            if (user) {
                user.Use(this.currentPortalStation.AsDestination(), destination);
            }
        }

        /// <summary>
        /// Can be overriden to filter destinations out.
        /// </summary>
        protected override bool FilterDestination(PortalStation.Destination destination) {
            if (!base.FilterDestination(destination)) {
                return false;
            }

            if (this.currentPortalStation) {
                return this.currentPortalStation.StationId != destination.id;
            }

            return true;
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
            var currentUser = CurrentUser;
            if (!currentUser) {
                return;
            }

            if (!this.currentPortalStation.Rename(value)) {
                return;
            }

            UpdateDestinationItems();
        }

    }
    
}
