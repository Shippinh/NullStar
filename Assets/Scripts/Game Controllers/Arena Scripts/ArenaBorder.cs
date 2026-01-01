using UnityEngine;

public class ArenaBorder : MonoBehaviour
{
    public ArenaController arenaControllerRef;
    public ArenaState activationState = ArenaState.None;
    public ArenaState deactivationState = ArenaState.None;

    private Transform[] children;

    private void Awake()
    {
        arenaControllerRef.OnArenaStateChanged += ArenaStateChangedHandler;

        children = transform.GetComponentsInChildren<Transform>(true);
    }

    void ArenaStateChangedHandler(ArenaState arenaState)
    {
        if (arenaState == activationState)
        {
            foreach (var child in children)
            {
                child.gameObject.SetActive(true);
            }
        }

        if (arenaState == deactivationState)
        {
            foreach (var child in children)
            {
                child.gameObject.SetActive(false);
            }
        }
    }
}
