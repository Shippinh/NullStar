using UnityEngine;

public class ArenaStartTrigger : MonoBehaviour
{
    public ArenaController arena;

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player"))
            return;

        arena.PlayerEnteredTrigger();
        gameObject.SetActive(false);
    }
}
