using UnityEngine.Events;
using UnityEngine.UI;

namespace UndeadBits.ValheimMods.PortalStation {
    
    /// <summary>
    /// Logic for teleportation destination items.
    /// </summary>
    public class DestinationItem : UIElementBase {
        private ZDO stationZDO;
        private Text stationNameText;
        private Button teleportButton;

        /// <summary>
        /// Raised when the "teleport" button has been clicked. 
        /// </summary>
        /// ReSharper disable once InconsistentNaming
        public readonly UnityEvent<ZDO> onClick = new UnityEvent<ZDO>();

        /// <summary>
        /// Gets or sets the stations ZDO.
        /// </summary>
        public ZDO StationZDO {
            get {
                return this.stationZDO;
            }
            set {
                if (this.stationZDO == value) {
                    return;
                }
                
                this.stationZDO = value;
                
                if (this.stationNameText) {
                    this.stationNameText.text = this.stationZDO == null ? "N/A" : this.stationZDO.GetString(PortalStation.PROP_STATION_NAME, "???");
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
            if (this.stationZDO == null) {
                return;
            }

            this.onClick.Invoke(this.stationZDO);
        }
    }
}
