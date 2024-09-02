using UnityEngine;
using Mirror;
using Tymski;

public partial class Player
{
    [Header("Destination")]
    public SceneReference startingScene;
    public Player player;
    public Vector3 position;


    [ServerCallback]
    public void OnPortal(SceneLocation scene)
    {

        Database.singleton.CharacterSave(this, false);
        Database.singleton.SaveCharacterScenePath(this.name, scene.mapScene);

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
