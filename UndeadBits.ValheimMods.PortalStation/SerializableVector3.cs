using System;
using UnityEngine;

namespace UndeadBits.ValheimMods.PortalStation {
    
    [Serializable]
    public struct SerializableVector3 {

        public float x;

        public float y;

        public float z;

        public SerializableVector3(float x, float y, float z) {
            this.x = x;
            this.y = y;
            this.z = z;
        }

        public static explicit operator Vector3(SerializableVector3 v) {
            return new Vector3(v.x, v.y, v.z);
        }

        public static explicit operator SerializableVector3(Vector3 v) {
            return new SerializableVector3(v.x, v.y, v.z);
        }


    }
}
