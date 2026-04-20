using UnityEngine;

public class FPSModelDriver : MonoBehaviour
{
    public Transform target; // assign "right" here

    void Update()
    {
        transform.localPosition = target.localPosition;
        transform.localRotation = target.localRotation;
    }
}