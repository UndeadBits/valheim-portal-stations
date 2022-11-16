using Jotunn.Managers;
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
        private Color originalFuelColor;
        private Button teleportButton;
        private Image fuelImage;
        private RectTransform fuelElement;

        /// <summary>
        /// Raised when the "teleport" button has been clicked. 
        /// </summary>
        /// ReSharper disable once InconsistentNaming
        public readonly UnityEvent<PortalStation.Destination> onClick = new UnityEvent<PortalStation.Destination>();
        
        /// <summary>
        /// Sets the destination which this item represents.
        /// </summary>
        public void SetDestination(BaseTeleportationGUI teleportationGUI, PortalStation.Destination value) {
            if (this.destination == value) {
                return;
            }
            
            this.destination = value;
            UpdateGUI(teleportationGUI);
        }

        /// <summary>
        /// Initializes the component.
        /// </summary>
        private void Awake() {
            this.stationNameText = RequireComponentByName<Text>("$part_StationName");
            this.teleportButton = RequireComponentByName<Button>("$part_TeleportButton");
            this.fuelCostText = RequireComponentByName<Text>("$part_FuelCount", true);
            this.fuelImage = RequireComponentByName<Image>("$part_FuelImage", true);
            this.fuelElement = RequireComponentByName<RectTransform>("$part_FuelElement", true);
            this.originalFuelColor = this.fuelCostText ? this.fuelCostText.color : Color.black;

            var fuelItem = PortalStationPlugin.Instance.FuelItem;
            if (this.fuelImage && fuelItem) {
                var icon = fuelItem.m_itemData.m_shared.m_icons?[0];
                if (icon) {
                    this.fuelImage.sprite = icon;
                }
            }
            
            if (this.teleportButton) {
                this.teleportButton.onClick.AddListener(OnTeleportClick);
            }
        }
        
        /// <summary>
        /// Updates the fuel cost
        /// </summary>
        public void UpdateGUI(BaseTeleportationGUI teleportationGUI) {
            var cost = 0;
            var affordable = false;
            
            if (teleportationGUI) {
                var available = teleportationGUI.GetFuelAmount();

                cost = teleportationGUI.CalculateFuelCost(this.destination);
                affordable = cost <= available;
            }
            
            if (cost <= 0) {
                if (this.fuelElement && this.fuelElement.gameObject.activeInHierarchy) {
                    this.fuelElement.gameObject.SetActive(false);
                }
            } else {
                if (this.fuelElement && !this.fuelElement.gameObject.activeInHierarchy) {
                    this.fuelElement.gameObject.SetActive(true);
                }

                if (this.fuelCostText) {
                    this.fuelCostText.text = $"{cost}";
                    this.fuelCostText.color = affordable ? this.originalFuelColor : Color.red;
                }
            }

            if (this.teleportButton) {
                this.teleportButton.interactable = affordable;
            }
            
            if (this.stationNameText) {
                this.stationNameText.text = this.destination == null ? "N/A" : this.destination.stationName;
            }
        }

        /// <summary>
        /// Handles a click on the teleport button.
        /// </summary>
        private void OnTeleportClick() {
            if (this.destination == null) {
                return;
            }

            this.onClick.Invoke(this.destination);
        }
    }
}
