using UnityEngine;

namespace UndeadBits.ValheimMods.PortalStation {
    
    /// <summary>
    /// Adds the ability to use a <see cref="PortalStation"/> to a <see cref="Humanoid"/>.
    /// </summary>
    public class PortalStationUser : MonoBehaviour {
        private Humanoid player;
        private ZNetView view;
        
        /// <summary>
        /// Uses the station.
        /// </summary>
        /// <param name="from">The current station being used</param>
        /// <param name="target">The destination to travel to</param>
        public void Use(PortalStation.Destination from, PortalStation.Destination target) {
            if (!this.view || !this.view.IsValid()) {
                Jotunn.Logger.LogWarning("Can't use portal station - player object not valid.");
                return;
            }

            if (!this.view.IsOwner()) {
                this.view.InvokeRPC(nameof(RPC_UsePortalStation), from.ID, target.ID);
            } else {
                UsePortalStation(from, target);
            }
        }

        /// <summary>
        /// Initializes the component.
        /// </summary>
        private void Awake() {
            this.view = GetComponent<ZNetView>();
            this.view.Register<ZDOID, ZDOID>(nameof(RPC_UsePortalStation), RPC_UsePortalStation);
            this.player = GetComponent<Humanoid>();
        }
        
        /// <summary>
        /// RPC call on the server to use a portal station.
        /// </summary>
        /// <param name="sender">The sender of the RPC</param>
        /// <param name="stationId">The current station being used</param>
        /// <param name="targetId">The destination to travel to</param>
        private void RPC_UsePortalStation(long sender, ZDOID stationId, ZDOID targetId) {
            var station = PortalStationPlugin.Instance.GetPortalStation(stationId);
            if (station == null) {
                Jotunn.Logger.LogWarning($"Can't use portal station - source station {stationId} not found.");
                return;
            }

            var target = PortalStationPlugin.Instance.GetPortalStation(targetId);
            if (target == null) {
                Jotunn.Logger.LogWarning($"Can't use portal station - target station {targetId} not found.");
                return;
            }

            UsePortalStation(station, target);
        }

        /// <summary>
        /// Uses the station.
        /// </summary>
        /// <param name="station">The current station being used</param>
        /// <param name="target">The destination to travel to</param>
        private void UsePortalStation(PortalStation.Destination station, PortalStation.Destination target) {
            if (!this.player || !this.view) {
                Jotunn.Logger.LogWarning("Can't use portal station - player object not valid.");
                return;
            }
            
            var playerZdo = this.view.IsValid() ? this.view.GetZDO() : null;
            if (playerZdo == null) {
                Jotunn.Logger.LogWarning("Can't use portal station - ZDO not valid.");
                return;
            }

            if (!PortalStationPlugin.Instance.CanTeleportPlayer(this.player)) {
                Jotunn.Logger.LogWarning("Can't use portal station - player not teleportable.");
                return;
            }
            
            var useDistance = Vector3.Distance(playerZdo.GetPosition(), station.Position);
            if (useDistance > PortalStation.USE_DISTANCE) {
                Jotunn.Logger.LogWarning($"Can't use portal station - station {target} out of use range ({useDistance}).");
                return;
            }

            var position = target.Position;
            var rotation = target.Rotation;
            var distance = Vector3.Distance(playerZdo.GetPosition(), position);
            var distant = distance >= ZoneSystem.instance.m_zoneSize;

            if (!player.TeleportTo(position, rotation, distant)) {
                Jotunn.Logger.LogWarning("Unable to teleport player.");
            }
        }
    }
}
