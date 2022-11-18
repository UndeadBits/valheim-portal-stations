using System;
using UnityEngine;

namespace UndeadBits.ValheimMods.PortalStation
{
    [Serializable]
    public struct SerializableQuaternion {

        public float x;

        public float y;

        public float z;

        public float w;

        public SerializableQuaternion(float x, float y, float z, float w) {
            this.x = x;
            this.y = y;
            this.z = z;
            this.w = w;
        }

        public static explicit operator Quaternion(SerializableQuaternion value) {
            return new Quaternion(value.x, value.y, value.z, value.w);
        }

        public static explicit operator SerializableQuaternion(Quaternion value) {
            return new SerializableQuaternion(value.x, value.y, value.z, value.w);
        }

    }
}
