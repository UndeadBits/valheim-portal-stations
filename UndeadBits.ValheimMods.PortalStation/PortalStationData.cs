using System;
using System.Collections.Generic;
using UnityEngine;

namespace UndeadBits.ValheimMods.PortalStation {
    
    /// <summary>
    /// Record used to store a point to teleport back to.
    /// </summary>
    [Serializable]
    public struct TeleportBackPoint {

        /// <summary>
        /// The position of the point
        /// </summary>
        public SerializableVector3 position;

        /// <summary>
        /// The rotation of the point
        /// </summary>
        public SerializableQuaternion rotation;

    }

    /// <summary>
    /// Used to cache and de/serialize data for this mod.
    /// </summary>
    [Serializable]
    public class PortalStationData {

        /// <summary>
        /// Used to store "Teleport Back" points for players on a valheim server.
        /// </summary>
        public Dictionary<string, TeleportBackPoint> teleportBackPoints = new Dictionary<string, TeleportBackPoint>();

        /// <summary>
        /// Tries to get the teleport back point for the given player.
        /// </summary>
        public bool TryGetTeleportBackPoint(string playerId, out TeleportBackPoint point) {
            if (this.teleportBackPoints != null && this.teleportBackPoints.Count != 0) {
                return this.teleportBackPoints.TryGetValue(playerId, out point);
            }

            point = default(TeleportBackPoint);
            return false;
        }

        /// <summary>
        /// Sets a new teleport back point for the given player.
        /// </summary>
        public void SetTeleportBackPoint(string playerId, Vector3 position, Quaternion rotation) {
            if (this.teleportBackPoints == null) {
                this.teleportBackPoints = new Dictionary<string, TeleportBackPoint>();
            }

            this.teleportBackPoints[playerId] = new TeleportBackPoint {
                position = (SerializableVector3) position,
                rotation = (SerializableQuaternion)rotation
            };
        }
        
    }
}
