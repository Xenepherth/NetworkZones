using UnityEngine;
using Mirror;

public partial class Player
{
    [Header("Destination")]
    public Player player;
    public Vector3 position;
    

    [ServerCallback]
    public void OnPortal(SceneLocation scene)
    {
        
        Database.singleton.CharacterSave(this, false);
        Database.SaveCharacterScenePath(this.name, scene.mapScene);

        player.transform.position = scene.position;  

        // ask client to switch server
        this.connectionToClient.Send(
            new SwitchServerMsg
            {
                scene = scene.mapScene.ScenePath,
                characterName = this.name
            }
        ); ;

        // immediately destroy so nothing messes with the new
        // position and so it's not saved again etc.
        NetworkServer.Destroy(this.gameObject);
    }
}
