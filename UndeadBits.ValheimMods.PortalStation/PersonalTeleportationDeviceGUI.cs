using System;
using UnityEngine;

namespace UndeadBits.ValheimMods.PortalStation {
    
    public class PersonalTeleportationDeviceGUI : BaseTeleportationGUI {
        private ItemDrop.ItemData device;

        /// <summary>
        /// Gets the amount of fuel available.
        /// </summary>
        public override int GetFuelAmount() {
            var user = CurrentUser;
            return user ? PersonalTeleportationDevice.GetFuelAmount(user) : 0;
        }
        
        /// <summary>
        /// Calculates the fuel cost to travel to the given destination.
        /// </summary>
        /// <returns></returns>
        public override int CalculateFuelCost(PortalStation.Destination destination) {
            var user = CurrentUser;
            if (!user) {
                return Int32.MaxValue;
            }

            var distance = Vector3.Distance(user.transform.position, destination.position);
            
            return PersonalTeleportationDevice.CalculateFuelCost(this.device, distance);
        }

        /// <summary>
        /// Shows the GUI.
        /// </summary>
        public void Open(Humanoid user, ItemDrop.ItemData item) {
            Jotunn.Logger.LogInfo($"Opening personal teleportation device GUI");
            this.device = item;
            
            OpenGUI(user);
        }

        protected override void Close() {
            base.Close();

            this.device = null;
        }

        /// <summary>
        /// Creates a new teleport destination item.
        /// </summary>
        protected override DestinationItem CreateDestinationItem(RectTransform parent) {
            return PortalStationPlugin.Instance.CreatePTDDestinationItem(parent);
        }

        /// <summary>
        /// Prepares teleportation to the given destination.
        /// </summary>
        /// <param name="destination">The destination to teleport to</param>
        /// <returns>Whether teleportation is possible or not</returns>
        protected override bool PrepareTeleportation(PortalStation.Destination destination) {
            if (!base.PrepareTeleportation(destination)) {
                return false;
            }
            
            var user = CurrentUser;
            if (!user) {
                return false;
            }

            return PersonalTeleportationDevice.PrepareTeleportation(user, this.device, destination);
        }
    }
    
}
