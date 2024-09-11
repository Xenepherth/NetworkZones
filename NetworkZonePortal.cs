using UnityEngine;
using Mirror;
using Eflatun.SceneReference;


public class NetworkZonePortal : MonoBehaviour
{
    [Header("[-=-=- NETWORK ZONE PORTAL -=-=-]")]
    public SceneReference sceneReference;
    public Vector3 position;

    public void OnPortal(Player player)
    {

        Database.singleton.CharacterSave(player, false);
        Database.singleton.SaveCharacterSceneName(player.name, sceneReference.Name);

        player.transform.position = position;

        // ask client to switch server
        player.connectionToClient.Send(
            new SwitchServerMsg
            {
                scene = sceneReference.Name,
                characterName = player.name
            }
        ); ;

        // immediately destroy so nothing messes with the new
        // position and so it's not saved again etc.
        NetworkServer.Destroy(player.gameObject);
    }

    // -----------------------------------------------------------------------------------
    // OnInteractServer
    // @Server
    // -
    // ----------------------------------------------------------------------------------
    [ServerCallback]
    public void OnTriggerEnter2D(Collider2D collision)
    {
            Player player = collision.GetComponentInParent<Player>();
            OnPortal(player);
    }
}
