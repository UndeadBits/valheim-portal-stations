using UnityEngine;

namespace UndeadBits.ValheimMods.PortalStation {
    
    public static class PersonalTeleportationDevice {

        private const float FUEL_CONSUMPTION = 1000.0f / 2.0f;
        
        public static int CalculateFuelCost(float distance) {
            return Mathf.Max(1, Mathf.CeilToInt(distance / FUEL_CONSUMPTION));
        }
        
        /// <summary>
        /// Invoked when the player wants to use the object.
        /// </summary>
        /// <param name="user">The user</param>
        /// <param name="item">The item data</param>
        public static void UseItem(Humanoid user, Inventory inventory, ItemDrop.ItemData item) {
            var gui = PortalStationPlugin.Instance.GetPersonalTeleportationDeviceGUIInstance();
            if (gui) {
                gui.Open(user, inventory, item);
            }
        }
    }
    
}
