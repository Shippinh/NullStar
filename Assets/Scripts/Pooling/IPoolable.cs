using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface IPoolable
{
    /// <summary>
    /// Stores the tag of pool it was taken from
    /// </summary>
    public string IPoolableTag 
    {
        get; set; 
    }

    /// <summary>
    /// Use this to grab things from the pool
    /// </summary>
    /// <param name="spawnPos">Where we will put the thing</param>
    /// <param name="spawnRot">Orientation of the thing</param>
    void HandleDepool(string poolableTag, Vector3 spawnPos, Quaternion spawnRot);   // taken from pool

    /// <summary>
    /// Use this to put things back into the pool
    /// </summary>
    void HandleRepool();                                                            // returned to pool
}

