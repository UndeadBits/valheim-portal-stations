using System;
using UnityEngine;

namespace UndeadBits.ValheimMods.PortalStation {
    
    public static class PersonalTeleportationDevice {

        /// <summary>
        /// The name of the fuel item to use.
        /// </summary>
        private const string FUEL_ITEM_NAME = "$item_greydwarfeye";
        
        /// <summary>
        /// The distance which can be traveled with one fuel item. 
        /// </summary>
        private const float TRAVEL_DISTANCE_PER_FUEL_ITEM = 1000;

        /// <summary>
        /// How much the travel distance is increased by item quality
        /// </summary>
        private const float TRAVEL_DISTANCE_PER_LEVEL = 1000;

        public static int GetFuelAmount(Humanoid user) {
            var inventory = user.GetInventory();
            if (inventory == null) {
                return 0;
            }

            return inventory.CountItems(FUEL_ITEM_NAME);
        }
        
        public static int CalculateFuelCost(ItemDrop.ItemData deviceData, float distance) {
            var travelDistancePerFuelItem = TRAVEL_DISTANCE_PER_FUEL_ITEM + (Math.Max(0, deviceData.m_quality - 1) * TRAVEL_DISTANCE_PER_LEVEL);
            
            return Mathf.Max(1, Mathf.CeilToInt(distance / travelDistancePerFuelItem));
        }
        
        /// <summary>
        /// Invoked when the player wants to use the object.
        /// </summary>
        /// <param name="user">The user</param>
        /// <param name="inventory">The inventory of the user</param>
        /// <param name="item">The item data</param>
        public static void UseItem(Humanoid user, Inventory inventory, ItemDrop.ItemData item) {
            if (item.m_durability < item.m_shared.m_durabilityDrain) {
                Jotunn.Logger.LogDebug($"Repair is needed");
                return;
            }
            
            var gui = PortalStationPlugin.Instance.GetPersonalTeleportationDeviceGUIInstance();
            if (gui) {
                gui.Open(user, item);
            }
        }

        public static bool PrepareTeleportation(Humanoid user, ItemDrop.ItemData device, PortalStation.Destination destination) {
            var inventory = user.GetInventory();
            if (inventory == null) {
                return false;
            }

            var distance = Vector3.Distance(user.transform.position, destination.position);
            var fuelCost = CalculateFuelCost(device, distance);
            var affordable = fuelCost <= GetFuelAmount(user);

            if (!affordable) {
                return false;
            }
            
            // TODO: Use RPC for this?
            
            inventory.RemoveItem(FUEL_ITEM_NAME, fuelCost);
            device.m_durability = Mathf.Max(0, device.m_durability - device.m_shared.m_useDurabilityDrain);
                
            return true;
        }
    }
    
}
