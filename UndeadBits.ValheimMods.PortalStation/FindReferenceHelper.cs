using UnityEngine;

namespace UndeadBits.ValheimMods.PortalStation {
    
    public static class FindReferenceHelper {

        public static T FindComponentByName<T>(this Transform transform, string name) where T : Component {
            foreach (var component in transform.GetComponentsInChildren<T>()) {
                if (component.gameObject.name == name) {
                    return component;
                }
            }

            return null;
        }
    }
}
