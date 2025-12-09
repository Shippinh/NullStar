using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Non mono behavior obj to dynamically change stats for solo and duo modes of barrier rammers
[CreateAssetMenu(menuName = "NullStar/AI Presets/Barrier Rammer Enemy")]
public class BarrierRammerPreset : ScriptableObject
{
    [Header("Movement Settings")]
    public float maxSpeed = 500f;
    public float minBurstDistance = 50f;
    public float maxBurstDistance = 50f;
    public float burstCooldown = 0.5f;

    public bool randomizeSpinDirection = true;

    [Header("Spiral Settings (Solo)")]
    public float maxSpiralOffset = 30f;
    public float verticalOffsetMultiplier = 1f;
    public float spiralFadeDistance = 150f;

    [Header("Pivot Point System (Paired Movement)")]
    public float pivotMoveSpeed = 10f;       // how quickly enemies move toward their assigned pivot
    public float pivotRandomOffset = 3f;     // how much to randomize pivot position each burst

    [Header("Pivot Rotation Settings")]
    public float pivotRotationSpeed = 180f; // degrees per second

    [Header("Avoidance")]
    public float avoidanceForce = 1000f;
    public float detectionRadius = 40f;
    public LayerMask obstacleMask;

    [Header("Pre-Attack Burst Settings")]
    public float burstSpeed = 200f;           // how fast the enemy moves during burst

    [Header("Attack Pathfinding")]
    public float pivotDistance = 25f;
    public float pivotForwardPush = 10f;   // worm goes past player
    public float pivotHeightOffset = 3f;   // lift pivot slightly above ground
    public float pivotChangeInterval = 3f; // every 3 seconds
    public LayerMask LOSMask;

    [Header("Attack Sequence Handling")]
    public float maxAcceleration = 200f;
    public float attackRotationBreakoffDistance = 150f; // distance at which the enemy stops rotating towards the player aggressively during the attack
    public float alignTime = 1f; // time for the enemy controller to get into the currentPivot position (during that we also start rotating the enemy towards the player)
    public float leftRightAlignTime = 1f; // time for enemy a and enemy b to take left and right positions
    public float attackTime = 3f; // time during which the enemy performs the attack (consequentially - moves)
    public int maxCollisionsBeforePrematureStop = 3; // how many objects it can kill before breaking the attack sequence
}
