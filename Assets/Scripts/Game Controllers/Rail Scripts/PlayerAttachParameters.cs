using UnityEngine;
using UnityEngine.Splines;

// Use this to set parameters for arena based attach calls
[System.Serializable]
public class PlayerAttachParameters
{
    public SplineContainer newSplineContainer;
    public float transitionDuration = 3f;
    public float xOffset = 0f;
    public float yOffset = 0f;
    public float newSplineT = 0f;
    public float initialSpeed = 56f;

    [SerializeField, Range(0f, 200f)] public float maxBoostVerticalStrafeSpeed = 15f;
    [SerializeField, Range(0f, 200f)] public float maxBoostHorizontalStrafeSpeed = 15f;
    [SerializeField, Range(0f, 200f)] public float maxBoostAcceleration = 80f;
    [SerializeField, Range(0f, 1000)] public float boostDodgeMaxSpeed = 25f;
}
