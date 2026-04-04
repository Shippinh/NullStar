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
}
