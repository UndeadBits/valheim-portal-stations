using UnityEngine;

namespace UndeadBits.ValheimMods.PortalStation {
    
    public class PersonalTeleportationDeviceGUI : BaseTeleportationGUI {

        public int GetFuelAmount() {
            var user = CurrentUser;
            if (!user) {
                return 0;
            }

            var inventory = user.GetInventory();
            if (inventory == null) {
                return 0;
            }

            return inventory.CountItems("$item_greydwarfeye");
        }
        
        public void Open(Humanoid user, Inventory inventory, ItemDrop.ItemData item) {
            Jotunn.Logger.LogInfo($"Opening personal teleportation device GUI");
            OpenGUI(user);
        }

        /// <summary>
        /// Creates a new teleport destination item.
        /// </summary>
        protected override DestinationItem CreateDestinationItem(RectTransform parent) {
            return PortalStationPlugin.Instance.CreatePTDDestinationItem(parent);
        }

        protected override bool AllowTeleportation(PortalStation.Destination destination, float distance) {
            if (!base.AllowTeleportation(destination, distance)) {
                return false;
            }
            
            var user = CurrentUser;
            if (!user) {
                return false;
            }

            var inventory = user.GetInventory();
            if (inventory == null) {
                return false;
            }
            
            var fuelCost = PersonalTeleportationDevice.CalculateFuelCost(distance);
            var affordable = fuelCost <= GetFuelAmount();

            if (!affordable) {
                return false;
            }

            inventory.RemoveItem("$item_greydwarfeye", fuelCost);
                
            return true;
        }
    }
    
}
