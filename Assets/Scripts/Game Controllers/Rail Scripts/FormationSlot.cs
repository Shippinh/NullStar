using UnityEngine;
using UnityEngine.Playables;

[System.Serializable]
public class FormationSlot
{
    public EnemyRailController enemy;
    public string poolTag;

    // Written each FixedUpdate by FormationController
    [HideInInspector] public float rightOffset;
    [HideInInspector] public float upOffset;

    public bool IsOccupied => enemy != null;
}