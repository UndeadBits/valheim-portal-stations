using System.Linq;
using UnityEngine;

namespace UndeadBits.ValheimMods.PortalStation {
    
    public static class FindReferenceHelper {

        public static T FindComponentByName<T>(this Transform transform, string name) where T : Component {
            return transform.GetComponentsInChildren<T>()
                .FirstOrDefault(x => x.gameObject.name == name);
        }
        
    }
}
