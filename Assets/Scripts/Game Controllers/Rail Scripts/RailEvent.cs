using UnityEngine;


public enum RailEventType
{
    None = 0,
    ChangePlayerSpeed = 1,
    EnableObject = 2
}

[System.Serializable]
public class RailEvent
{
    // INNATE FIELDS
    [Range(0f, 1f)] public float eventTimelinePosition = 0f;

    public RailEventType type;

    public bool canShootAgain = false;
    public bool canShoot = true;

    // GENERIC FIELDS
    public int intPtr;
    public float floatPtr;
    public bool boolPtr;
    public Object objectParam;
}
