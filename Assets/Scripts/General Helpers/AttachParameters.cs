using UnityEngine.Splines;

[System.Serializable]
public class AttachParameters
{
    public SplineContainer newSplineContainer;
    public float transitionDuration = 3f;
    public float xOffset = 0f;
    public float yOffset = 0f;
    public float newSplineT = 0f;
    public float initialSpeed = 56f;
}
