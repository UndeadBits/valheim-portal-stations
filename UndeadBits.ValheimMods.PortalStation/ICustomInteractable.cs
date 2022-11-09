namespace UndeadBits.ValheimMods.PortalStation {
    
    /// <summary>
    /// Can be used to override an interactables UseItem method.
    /// </summary>
    public interface ICustomInteractable {
        
        /// <summary>
        /// Invoked when the player wants to use the object.
        /// </summary>
        /// <param name="user">The user</param>
        /// <param name="item">The item data</param>
        /// <param name="originalUseItemResult">The result as returned by the original UseItem method</param>
        /// <returns>Whether the object can be used or not</returns>
        bool UseItem(Humanoid user, ItemDrop.ItemData item, bool originalUseItemResult);
        
    }
}
