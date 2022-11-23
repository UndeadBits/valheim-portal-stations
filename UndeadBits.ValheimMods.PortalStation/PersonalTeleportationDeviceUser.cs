﻿using System;
using UnityEngine;

namespace UndeadBits.ValheimMods.PortalStation {
    
    /// <summary>
    /// Adds the ability to use a <see cref="PersonalTeleportationDevice"/>s to a <see cref="Humanoid"/>.
    /// </summary>
    public class PersonalTeleportationDeviceUser : MonoBehaviour {
        private Humanoid player;
        private ZNetView view;
        private TeleportBackPoint? teleportBackPoint;

        /// <summary>
        /// Gets or sets the teleport back point for the player.
        /// </summary>
        public TeleportBackPoint? TeleportBackPoint {
            get { return this.teleportBackPoint; }
            set { this.teleportBackPoint = value; }
        }

        /// <summary>
        /// Uses the device.
        /// </summary>
        /// <param name="deviceItem">The device item being used</param>
        /// <param name="target">The destination to travel to</param>
        public void Use(ItemDrop.ItemData deviceItem, PortalStation.Destination target) {
            if (!this.view || !this.view.IsValid()) {
                Jotunn.Logger.LogWarning($"Can't use device - player object not valid.");
                return;
            }
            
            if (!this.view.IsOwner()) {
                Jotunn.Logger.LogWarning($"Can't use device - player is not the owner.");
            } else {
                UseDevice(deviceItem, target);
            }
        }
        
        /// <summary>
        /// Initializes the component.
        /// </summary>
        private void Awake() {
            this.view = GetComponent<ZNetView>();
            this.player = GetComponent<Humanoid>();
        }

        private void Start() {
            PortalStationPlugin.Instance.RequestTeleportBackPointFromServer();
        }

        /// <summary>
        /// Uses the station.
        /// </summary>
        /// <param name="deviceItem">The device item being used</param>
        /// <param name="target">The destination to travel to</param>
        private void UseDevice(ItemDrop.ItemData deviceItem, PortalStation.Destination target) {
            if (!this.player || !this.view) {
                Jotunn.Logger.LogWarning($"Can't use device - player object not valid.");
                return;
            }
            
            var playerZdo = this.view.IsValid() ? this.view.GetZDO() : null;
            if (playerZdo == null) {
                Jotunn.Logger.LogWarning($"Can't use device - ZDO not valid.");
                return;
            }

            if (!PortalStationPlugin.Instance.CanTeleportPlayer(this.player)) {
                Jotunn.Logger.LogWarning($"Can't use device - player not teleportable.");
                return;
            }

            var currentPosition = playerZdo.GetPosition();
            var currentRotation = playerZdo.GetRotation();
            var distance = Vector3.Distance(currentPosition, target.position);
            var distant = distance >= ZoneSystem.instance.m_zoneSize;
            
            if (!PersonalTeleportationDevice.CanPlayerUseDevice(player, deviceItem, distance)) {
                Jotunn.Logger.LogWarning($"Can't use device - not affordable.");
                return;
            }

            if (!player.TeleportTo(target.position, target.rotation, distant)) {
                Jotunn.Logger.LogError("Unable to teleport player.");
                return;
            }
            
            PersonalTeleportationDevice.ConsumeFuelAndDurability(player, deviceItem, distance);

            this.teleportBackPoint = new TeleportBackPoint {
                position = (SerializableVector3)currentPosition,
                rotation = (SerializableQuaternion)currentRotation
            };
            
            PortalStationPlugin.Instance.SetTeleportBackPoint(currentPosition, currentRotation);
        }
        
    }
    
}
