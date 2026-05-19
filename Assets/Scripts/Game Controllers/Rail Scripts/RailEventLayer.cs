using System;
using UnityEngine;

[Serializable]
public class RailEventLayer
{
    public string name = "Layer";
    public Color color = new Color(0.6f, 0.6f, 0.6f);
    public bool visible = true;
    public bool locked = false;

    public RailEventLayer() { }

    public RailEventLayer(string name, Color color)
    {
        this.name = name;
        this.color = color;
    }
}