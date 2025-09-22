using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EntityArenaController : MonoBehaviour
{
    [SerializeField, Range(1, 10)] public int waveToAppear = 1; // gets pooled out at the preset location when the arena wave counter changes
}
