using UnityEngine;
using System.Collections.Generic;

public class DontDestroyOnLoad : MonoBehaviour
{
    public static Dictionary<string, DontDestroyOnLoad> singletons = new Dictionary<string, DontDestroyOnLoad>();

    void Awake()
    {
        DontDestroyOnLoad(gameObject);

        // if we load the initial scene again then the object will exists twice
        // so let's make sure to delete any duplicates
        // -> its important to keep the exact ones so that server/client ids are
        //    the same
        if (!singletons.ContainsKey(name))
            singletons[name] = this;
        else Destroy(gameObject);
    }
}
