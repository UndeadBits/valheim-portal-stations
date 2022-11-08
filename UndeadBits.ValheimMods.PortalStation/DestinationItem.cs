using UnityEngine.Events;
using UnityEngine.UI;

namespace UndeadBits.ValheimMods.PortalStation {
    
    /// <summary>
    /// Logic for teleportation destination items.
    /// </summary>
    public class DestinationItem : UIElementBase {
        private PortalStation.Destination destination;
        private Text stationNameText;
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
                
                if (this.stationNameText) {
                    this.stationNameText.text = this.destination.stationName; // this.stationZDO == null ? "N/A" : this.stationZDO.GetString(PortalStation.PROP_STATION_NAME, "???");
                }
            }
        }

        /// <summary>
        /// Initializes the component.
        /// </summary>
        private void Awake() {
            this.stationNameText = RequireComponentByName<Text>("$part_StationName");
            this.teleportButton = RequireComponentByName<Button>("$part_TeleportButton");
            
            if (this.teleportButton) {
                this.teleportButton.onClick.AddListener(OnTeleportClick);
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
