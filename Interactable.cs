using Mirror;
using UnityEngine;

// UCE INTERACTABLE AREA (BOX) CLASS

[RequireComponent(typeof(NetworkIdentity))]
public abstract partial class Interactable : NetworkBehaviour
{
    // -----------------------------------------------------------------------------------
    // OnInteractClient
    // -----------------------------------------------------------------------------------
    //[ClientCallback]
    public virtual void OnInteractClient(Player player) { }

    // -----------------------------------------------------------------------------------
    // OnInteractServer
    // -----------------------------------------------------------------------------------
    //[ServerCallback]
    public virtual void OnInteractServer(Player player) { }

    // -----------------------------------------------------------------------------------
    // IsUnlocked
    // -----------------------------------------------------------------------------------
    public virtual bool IsUnlocked() { return false; }

    // -----------------------------------------------------------------------------------
    // IsWorthUpdating
    // -----------------------------------------------------------------------------------
    public virtual bool IsWorthUpdating()
    {
        return netIdentity.observers == null ||
               netIdentity.observers.Count > 0;
    }

}