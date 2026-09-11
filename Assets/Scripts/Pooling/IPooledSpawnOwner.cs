using UnityEngine;

/// <summary>
/// Implemented by anything that spawns/claims entities from the ObjectPool
/// and needs to be notified when it no longer owns one (death, forced repool, etc).
/// </summary>
public interface IPooledSpawnOwner
{
    /// <summary>
    /// Called when an entity this owner claimed is being returned to the pool,
    /// for any reason. Implementers must clear all local state referencing it
    /// (slot, spawn point, alive count, etc). Guaranteed to be called exactly
    /// once per successful ClaimOwnership, either naturally or because another
    /// owner force-claimed the same entity out from under a stale reference.
    /// </summary>
    void OnEntityReleased(MonoBehaviour releasedObject);
}