using UnityEngine;

namespace UndeadBits.ValheimMods.PortalStation {
    
    /// <summary>
    /// Base class for UI elements.
    /// </summary>
    public abstract class UIElementBase : MonoBehaviour {

        /// <summary>
        /// Searches for the given component in children by name and logs a warning if it could not be found.
        /// </summary>
        /// <param name="name">The component name</param>
        /// <typeparam name="T">The component type</typeparam>
        /// <returns>The found component or null</returns>
        protected T RequireComponentByName<T>(string name) where T : Component {
            var result = this.transform.FindComponentByName<T>(name);

            if (result == null) {
                Jotunn.Logger.LogWarning($"Unable to find required child component with name \"{name}\"");
            }

            return result;
        }
        
    }
}
