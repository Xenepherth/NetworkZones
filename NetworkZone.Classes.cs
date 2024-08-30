using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Mirror;
using Tymski;

// SceneLocation

[Serializable]
public partial class SceneLocation
{
    public SceneReference mapScene;
    public Vector3 position;

    public bool Valid
    {
        get
        {
            return mapScene.ScenePath != null;
        }
    }
}
