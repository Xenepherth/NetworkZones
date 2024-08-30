using UnityEngine;
using Mirror;


public class NetworkZonePortal : InteractablePortal
{
    [Header("[-=-=- NETWORK ZONE PORTAL -=-=-]")]
    public Collider2D collider2d;
    public SceneLocation location;


    // -----------------------------------------------------------------------------------
    // OnInteractServer
    // @Server
    // -
    // ----------------------------------------------------------------------------------
    [ServerCallback]
    public void OnTriggerEnter2D(Collider2D collision)
    {
            Player player = collision.GetComponentInParent<Player>();
            player.OnPortal(location);
    }
}
