using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum RailEventType
{
    None
}

public class RailEvent : MonoBehaviour
{
    [Range(0f, 1f)] public float eventExecutionTime = 0f;
    public RailEventType eventType;
    public bool canShootAgain = false;
    public bool canShoot = true;


    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
