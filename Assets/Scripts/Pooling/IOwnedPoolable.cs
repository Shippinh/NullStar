/// <summary>
/// Extends IPoolable for poolables that need single-owner tracking.
/// Claim/Release are driven by ObjectPool itself (on depool/repool),
/// not by the poolable's own gameplay events — this guarantees release
/// fires on every path back into the pool, not just "natural" death.
/// </summary>
public interface IOwnedPoolable : IPoolable
{
    void ClaimOwnership(IPooledSpawnOwner newOwner);
    void ReleaseOwnership();
}