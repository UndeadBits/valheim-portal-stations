using System;
using System.Collections.Generic;
using UnityEngine;

namespace UndeadBits.ValheimMods.PortalStation {
    
    /// <summary>
    /// Logic for personal teleportation device GUI.
    /// </summary>
    public class PersonalTeleportationDeviceGUI : BaseTeleportationGUI {
        private PortalStation.Destination teleportBackPoint;
        private ItemDrop.ItemData device;

        /// <summary>
        /// Gets the amount of fuel available.
        /// </summary>
        public override int GetFuelAmount() {
            var user = CurrentUser;
            return user == null ? 0 : PersonalTeleportationDevice.GetFuelAmount(user);
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

            if (destination == null) {
                return Int32.MaxValue;
            }

            var distance = Vector3.Distance(user.transform.position, destination.position);
            
            return PersonalTeleportationDevice.CalculateFuelCost(this.device, distance);
        }
        
        /// <summary>
        /// Shows the GUI.
        /// </summary>
        public void Open(Humanoid user, ItemDrop.ItemData item) {
            this.device = item;
            this.teleportBackPoint = DeserializeTeleportBackPoint(user.m_nview);
            
            OpenGUI(user);
        }

        /// <summary>
        /// Deserializes the "teleport back" point from the given view.
        /// </summary>
        private static PortalStation.Destination DeserializeTeleportBackPoint(ZNetView view) {
            if (!view || !view.IsValid()) {
                return null;
            }

            var travelBackDestinationBase64 = view.GetZDO().GetString(PersonalTeleportationDevice.PROP_TELEPORT_BACK_POINT);
            Vector3 position;
            Quaternion rotation;

            if (PersonalTeleportationDevice.DeserializeTeleportBackPoint(travelBackDestinationBase64, out position, out rotation)) {
                return new PortalStation.Destination(ZDOID.None) {
                    position = position,
                    rotation = rotation,
                    stationName = PortalStationPlugin.Localization.TryTranslate("$travel_back"),
                };
            }

            return null;
        }
        
        /// <summary>
        /// Updates the list of destinations to which the user can travel.
        /// </summary>
        protected override void UpdateDestinationList(List<PortalStation.Destination> destinations) {
            destinations.Clear();

            if (this.teleportBackPoint != null) {
                destinations.Add(this.teleportBackPoint);
            }

            destinations.AddRange(PortalStationPlugin.Instance.AvailableDestinations);
        }
        
        /// <summary>
        /// Closes the GUI
        /// </summary>
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
        protected override bool CanTeleport(PortalStation.Destination destination) {
            if (!base.CanTeleport(destination)) {
                return false;
            }
            
            var user = CurrentUser;
            if (!user) {
                return false;
            }

            var distance = Vector3.Distance(user.transform.position, destination.position);

            return PersonalTeleportationDevice.CanPlayerUseDevice(user, this.device, distance);
        }
        
        /// <summary>
        /// Teleports the a player to the given destination.
        /// </summary>
        /// <param name="player">The player to teleport</param>
        /// <param name="destination">The desired destination</param>
        protected override void TeleportTo(Humanoid player, PortalStation.Destination destination) {
            var user = player.GetComponent<PersonalTeleportationDeviceUser>();
            if (user) {
                user.Use(this.device, destination);
            }
        }

    }
    
}
