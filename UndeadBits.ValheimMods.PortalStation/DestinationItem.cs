using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace UndeadBits.ValheimMods.PortalStation {
    
    /// <summary>
    /// Logic for teleportation destination items.
    /// </summary>
    public class DestinationItem : UIElementBase {
        private PortalStation.Destination destination;
        private Text stationNameText;
        private Text fuelCostText;
        private Image fuelIconImage;
        private Color originalFuelColor;
        private Button teleportButton;

        /// <summary>
        /// Raised when the "teleport" button has been clicked. 
        /// </summary>
        /// ReSharper disable once InconsistentNaming
        public readonly UnityEvent<PortalStation.Destination> onClick = new UnityEvent<PortalStation.Destination>();

        /// <summary>
        /// Gets or sets the destination data.
        /// </summary>
        public PortalStation.Destination Destination {
            get {
                return this.destination;
            }
            set {
                if (this.destination == value) {
                    return;
                }
                
                this.destination = value;
                
                UpdateGUI();
            }
        }

        /// <summary>
        /// Initializes the component.
        /// </summary>
        private void Awake() {
            this.stationNameText = RequireComponentByName<Text>("$part_StationName");
            this.teleportButton = RequireComponentByName<Button>("$part_TeleportButton");
            this.fuelCostText = RequireComponentByName<Text>("$part_FuelCount", true);
            this.fuelIconImage = RequireComponentByName<Image>("$part_FuelImage", true);
            this.originalFuelColor = this.fuelCostText ? this.fuelCostText.color : Color.black;
            
            if (this.teleportButton) {
                this.teleportButton.onClick.AddListener(OnTeleportClick);
            }

            if (this.fuelCostText) {
                InvokeRepeating(nameof(UpdateGUI), 0, 0.2f);
            }
        }
        
        /// <summary>
        /// Updates the fuel cost
        /// </summary>
        private void UpdateGUI() {
            var cost = 0;
            var affordable = false;
            var teleportationGUI = GetComponentInParent<BaseTeleportationGUI>();
            
            if (teleportationGUI) {
                var user = teleportationGUI.CurrentUser;
                if (user) {
                    var distance = Vector3.Distance(user.transform.position, this.destination.position);
                    var available = teleportationGUI.GetFuelAmount();

                    cost = teleportationGUI.CalculateFuelCost(this.destination);
                    affordable = cost <= available;
                    Jotunn.Logger.LogInfo($"Fuel: {available}, cost: {cost}, distance: {distance}");
                }
            }

            if (this.fuelCostText) {
                this.fuelCostText.text = $"{cost}";
                this.fuelCostText.color = affordable ? this.originalFuelColor : Color.red;
            }

            if (this.teleportButton) {
                this.teleportButton.interactable = affordable;
            }
            
            if (this.stationNameText) {
                this.stationNameText.text = this.destination.stationName;
            }
        }

        /// <summary>
        /// Handles a click on the teleport button.
        /// </summary>
        private void OnTeleportClick() {
            if (this.destination.id.IsNone()) {
                return;
            }

            this.onClick.Invoke(this.destination);
        }
    }
}
