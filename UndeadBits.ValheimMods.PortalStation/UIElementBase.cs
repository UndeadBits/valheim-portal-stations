using UnityEngine;

namespace UndeadBits.ValheimMods.PortalStation {
    
    /// <summary>
    /// Base class for UI elements.
    /// </summary>
    public abstract class UIElementBase : MonoBehaviour {

        /// <summary>
        /// Searches for the given component in children by name and logs a warning if it could not be found.
        /// </summary>
        /// <param name="componentName">The component name</param>
        /// <param name="optional">Whether the component is mandatory or optional</param>
        /// <typeparam name="T">The component type</typeparam>
        /// <returns>The found component or null</returns>
        protected T RequireComponentByName<T>(string componentName, bool optional = false) where T : Component {
            var result = this.transform.FindComponentByName<T>(componentName);

            if (result == null && !optional) {
                Jotunn.Logger.LogWarning($"Unable to find required child component with name \"{componentName}\"");
            }

            return result;
        }
        
    }
}
