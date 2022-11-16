using System;
using UnityEngine;

namespace UndeadBits.ValheimMods.PortalStation {
    
    /// <summary>
    /// Logic for using a personal teleportation device.
    /// </summary>
    public static class PersonalTeleportationDevice {
        
        /// <summary>
        /// The property which is used to remember the last position from which a user has teleported itself.
        /// </summary>
        public const string PROP_TELEPORT_BACK_POINT = "lastTeleportationPoint";
        
        /// <summary>
        /// The name of the item.
        /// </summary>
        public const string ITEM_NAME = "$item_personal_teleportation_device";

        /// <summary>
        /// Gets the amount of fuel left in a users inventory.
        /// </summary>
        /// <param name="user">The user</param>
        /// <returns>The fuel amount</returns>
        public static int GetFuelAmount(Humanoid user) {
            var inventory = user.GetInventory();
            if (inventory == null) {
                return 0;
            }

            var fuelItem = PortalStationPlugin.Instance.FuelItemId;
            if (String.IsNullOrEmpty(fuelItem)) {
                return 0;
            }

            return inventory.CountItems(fuelItem);
        }
        
        /// <summary>
        /// Calculates the fuel cost for the given travel distance.
        /// </summary>
        /// <param name="deviceData">The device data</param>
        /// <param name="distance">The travel distance</param>
        /// <returns>The fuel cost</returns>
        public static int CalculateFuelCost(ItemDrop.ItemData deviceData, float distance) {
            if (!PortalStationPlugin.Instance.UseFuel) {
                return 0;
            }
            
            var travelDistancePerFuelItem = PortalStationPlugin.Instance.TravelDistancePerFuelItem
                                            + (Math.Max(0, deviceData.m_quality - 1) * PortalStationPlugin.Instance.AdditionalTeleportationDistancePerUpgrade);
            
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

        /// <summary>
        /// Determines whether the given player can use a personal teleportation device.
        /// </summary>
        /// <param name="player">The player</param>
        /// <param name="device">The device item</param>
        /// <param name="distance">The travel distance</param>
        /// <returns>Whether the device is usable</returns>
        public static bool CanPlayerUseDevice(Humanoid player, ItemDrop.ItemData device, float distance) {
            var fuelCost = CalculateFuelCost(device, distance);
            var affordable = fuelCost <= GetFuelAmount(player);

            if (!affordable) {
                return false;
            }
                
            return true;
        }

        /// <summary>
        /// Consumes fuel from the inventory and durability from the device after a successful teleportation.
        /// </summary>
        /// <param name="player">The player</param>
        /// <param name="device">The device item</param>
        /// <param name="distance">The travel distance</param>
        public static void ConsumeFuelAndDurability(Humanoid player, ItemDrop.ItemData device, float distance) {
            device.m_durability = Mathf.Max(0, device.m_durability - device.m_shared.m_useDurabilityDrain);

            var fuelCost = CalculateFuelCost(device, distance);
            var inventory = player.GetInventory();

            if (inventory == null) {
                return;
            }
            
            var fuelItem = PortalStationPlugin.Instance.FuelItemId;
            if (String.IsNullOrEmpty(fuelItem)) {
                return;
            }
            
            inventory.RemoveItem(fuelItem, fuelCost);
            inventory.Changed();
        }

        /// <summary>
        /// Serializes position and rotation for the teleport back point.
        /// </summary>
        /// <returns>A base64 encoded string</returns>
        public static string SerializeTeleportBackPoint(Vector3 position, Quaternion rotation) {
            var package = new ZPackage();
            package.Write(position);
            package.Write(rotation);
                
            return package.GetBase64();
        }

        /// <summary>
        /// Deserializes a travel back destination
        /// </summary>
        /// <param name="base64">The base64 encoded string</param>
        /// <param name="position">Will be set to the position</param>
        /// <param name="rotation">Will be set to the rotation</param>
        /// <returns>Whether deserialization was successful or not</returns>
        public static bool DeserializeTeleportBackPoint(string base64, out Vector3 position, out Quaternion rotation) {
            if (String.IsNullOrEmpty(base64)) {
                position = default(Vector3);
                rotation = default(Quaternion);

                return false;
            }
            
            var package = new ZPackage(Convert.FromBase64String(base64));
            position = package.ReadVector3();
            rotation = package.ReadQuaternion();

            return true;
        }
        
    }
    
}
