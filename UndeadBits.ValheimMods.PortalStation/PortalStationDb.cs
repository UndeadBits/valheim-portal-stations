using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Xml;
using System.Xml.Serialization;
using UnityEngine;

namespace UndeadBits.ValheimMods.PortalStation {

    /// <summary>
    /// Used to cache data for this mod.
    /// </summary>
    public class PortalStationDb {
        private static readonly BinaryFormatter DataFormatter = new BinaryFormatter();
        private readonly string directoryPath;
        private readonly string fullPath;
        private PortalStationData data;

        /// <summary>
        /// Initializes a new instance of the <see cref="PortalStationDb"/> type.
        /// </summary>
        /// <param name="path">The data storage path</param>
        public PortalStationDb(string path) {
            this.directoryPath = path;
            this.fullPath = Path.Combine(path, "data.db");
            this.data = new PortalStationData();
            
            Load();
        }

        /// <summary>
        /// Loads data from the database file.
        /// </summary>
        public void Load() {
            try {
                Directory.CreateDirectory(this.directoryPath);
                
                if (File.Exists(this.fullPath)) {
                    using (var stream = File.OpenRead(this.fullPath)) {
                        this.data = (PortalStationData)DataFormatter.Deserialize(stream);
                    }
                } else {
                    this.data = new PortalStationData();
                }
            } catch (Exception ex) {
                Jotunn.Logger.LogError($"Can't load portal station DB: {ex.Message}");
            }
        }

        /// <summary>
        /// Saves data to the database file.
        /// </summary>
        public void Save() {
            try {
                Directory.CreateDirectory(this.directoryPath);

                using (var stream = File.OpenWrite(this.fullPath)) {
                    DataFormatter.Serialize(stream, this.data);
                }
            } catch (Exception ex) {
                Jotunn.Logger.LogError($"Can't save portal station DB: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Tries to get the teleport back point for the given player.
        /// </summary>
        public bool TryGetTeleportBackPoint(string playerId, out Vector3 position, out Quaternion rotation) {
            TeleportBackPoint point;

            if (!this.data.TryGetTeleportBackPoint(playerId, out point)) {
                position = Vector3.zero;
                rotation = Quaternion.identity;
                return false;
            }
            
            position = (Vector3)point.position;
            rotation = (Quaternion)point.rotation;

            return true;
        }
        
        /// <summary>
        /// Sets a new teleport back point for the given player.
        /// </summary>
        public void SetTeleportBackPoint(string playerId, Vector3 position, Quaternion rotation) {
            this.data.SetTeleportBackPoint(playerId, position, rotation);
        }

    }
    
}
