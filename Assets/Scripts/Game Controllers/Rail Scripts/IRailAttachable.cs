using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface IRailAttachable
{
    /// <summary>
    /// Use this to attach enemies to the rail
    /// </summary>
    void HandleRailAttach();

    /// <summary>
    /// Use this to detach enemies from the rail
    /// </summary>
    void HandleRailDetach();
}
