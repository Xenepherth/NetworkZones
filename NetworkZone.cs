// Spawns one server zone process per scene.
// Also shuts down other zones when the main one died or was terminated.
//
// IMPORTANT: we do not EVER set manager.onlineScene/offlineScene. This part of
// UNET is broken and will cause all kinds of random issues when the server
// forces the client to reload the scene while receiving initialization messages
// already. Instead we always load the scene manually, then connect to the
// server afterwards.
using System;
using System.IO;
using System.Diagnostics;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Mirror;
using kcp2k;
using SQLite;
using Tymski;
using UnityEngine.EventSystems;
using System.Security.Policy;
using UnityEngine.XR;

[RequireComponent(typeof(NetworkManager))]
[RequireComponent(typeof(KcpTransport))]
public class NetworkZone : MonoBehaviour
{
    public bool active;
    // component
    public NetworkManager manager;
    public KcpTransport transport;

    // paths to the scenes to spawn
    public SceneReference[] scenesToSpawn;

    // write online time to db every 'writeInterval'
    // die if not online for 'writeInterval * timeoutMultiplier'

    // switch server packet handler
    [HideInInspector]
    public string autoSelectCharacter { get; set; }

    [HideInInspector]
    public bool autoConnectClient;

    [HideInInspector] public bool isSibling;
    // original network port
    ushort originalPort;

    [Header("AliveCheck")]
    [Range(1, 10)] public float writeInterval = 1;
    [Range(2, 10)] public float timeoutMultiplier = 3;
    public float timeoutInterval { get { return writeInterval * timeoutMultiplier; } }
    // command line args ///////////////////////////////////////////////////////
    public int ParseSceneIndexFromArgs()
    {
        // note: args are null on android
        String[] args = System.Environment.GetCommandLineArgs();
        if (args != null)
        {
            int index = args.ToList().FindIndex(arg => arg == "-scenePath");
            return 0 <= index && index < args.Length - 1 ? args[index + 1].ToInt() : 0;
        }
        return 0;
    }

    public string ArgsString()
    {
        // note: first arg is always process name or empty
        // note: args are null on android
        String[] args = System.Environment.GetCommandLineArgs();
        return args != null ? String.Join(" ", args.Skip(1).ToArray()) : "";
    }

    // full path to game executable
    // -> osx: ../game.app/Contents/MacOS/game
    // -> GetCurrentProcess, Application.dataPaht etc. all aren't good for this
    public string processPath
    {
        get
        {
            // note: args are null on android
            String[] args = System.Environment.GetCommandLineArgs();
            return args != null ? args[0] : "";
        }
    }
    ////////////////////////////////////////////////////////////////////////////

    // before NetworkServer.Start
    void Awake()
    {
        if (!active) return;

        originalPort = transport.port;

        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;

        if (FindObjectsOfType<NetworkZone>().Length > 1)
        {
            print("Multiple NetworkZone components in the Scene will cause Deadlocks!");
            return;
        }

        // -- Setup Sibling

        int index = ParseSceneIndexFromArgs();
        if (index > 0)
        {
            isSibling = true;
            string scenePath = scenesToSpawn[index].ScenePath;
            SceneManager.GetSceneByPath(scenePath);

            print("[Zones] setting requested port: +" + index);
            transport.port = (ushort)(originalPort + index);

            print("[Zones] changing server scene: " + scenePath);
            manager.onlineScene = scenePath; // loads scene automatically
            manager.StartServer();
        }
    }

    public void SpawnProcesses()
    {
        // only if we are the main scene (if no -scene parameter was passed)
        if (ParseSceneIndexFromArgs() == 0)
        {
            // write zone online time every few seconds
            print("[Zones]: main process starts online writer...");
            InvokeRepeating("WriteOnlineTime", 1, writeInterval);

            print("[Zones]: main process spawns siblings...");

            // -- Spawn Siblings

            for (int i = 1; i < scenesToSpawn.Length; ++i)
            {
                int index = i;
                Process p = new Process();
                p.StartInfo.FileName = processPath;
                p.StartInfo.Arguments = ArgsString() + " -scenePath " + index.ToString();
                print("[Zones]: spawning: " + p.StartInfo.FileName + "; args=" + p.StartInfo.Arguments);
                p.Start();
            }
        }
    }

    public void OnClientSwitchServerRequested(SwitchServerMsg message)
    {
        print("OnClientSwitchServerRequested: " + message.scene);

        // only on client
        if (!NetworkServer.active)
        {
            print("[Zones]: disconnecting from current server");
            manager.StopClient();

            // clean up as much as possible.
            // if we don't call NetworkManager.Shutdown then objects aren't
            // spawned on the client anymore after connecting to scene #2 for
            // the second time.
            NetworkClient.Shutdown();
            GetComponent<NetworkManager>().enabled = false;
            GetComponent<NetworkManager>().enabled = true;


            Transport.active.enabled = false; // don't receive while switching scenes
            print("[Zones]: loading required scene: " + message.scene);
            autoSelectCharacter = message.characterName;
            // load requested scene and make sure to auto connect when it's done
            string scenePath = Path.GetFileNameWithoutExtension(message.scene);

            SceneManager.LoadSceneAsync(scenePath);
            autoConnectClient = true;
        }
    }

    // on scene loaded /////////////////////////////////////////////////////////
    public void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        UnityEngine.Debug.Log("OnSceneLoaded: " + scene.name);

        // server started and loaded the requested scene?
        int index = ParseSceneIndexFromArgs();

        // Server
        if (NetworkServer.active && scenesToSpawn[index] == scene.path)
        {
            // write online time every few seconds and check main zone alive every few seconds
            print("[Zones]: starting online alive check for zone: " + scene.name);
            InvokeRepeating("AliveCheck", timeoutInterval, timeoutInterval);

        }

        // Client
        // client started and needs to automatically connect?
        if (autoConnectClient)
        {
            int sceneIndex = scenesToSpawn.ToList().FindIndex(x => x.ScenePath == scene.path);
            transport.port = (ushort)(originalPort + sceneIndex);
            print("[Zones]: automatically connecting client to new server at port: " + transport.port);
            //manager.onlineScene = scene.name; DO NOT DO THIS! will cause client to reload scene again, causing unet bugs
            manager.StartClient(); // loads new scene automatically
            autoConnectClient = false;
        }
    }
    public void WriteOnlineTime()
    {
        Database.singleton.SaveMainZoneOnlineTime();
    }
    public void AliveCheck()
    {
        double mainZoneOnline = Database.singleton.TimeElapsedSinceMainZoneOnline();
        print("[Zones]: AliveCheck... " + mainZoneOnline);

        if (mainZoneOnline > timeoutInterval)
        {
            print("[Zones]: alive check failed, time to die for: " + SceneManager.GetActiveScene().name);
            Application.Quit();
        }
    }
}



// messages ////////////////////////////////////////////////////////////////////
public struct SwitchServerMsg : NetworkMessage
{
    public string scene;
    public string characterName;
}

// networkmanager hooks via addon system ///////////////////////////////////////
public partial class NetworkManagerMMO
{
    public void OnClientConnect_Zones(NetworkConnection conn)
    {
        NetworkClient.RegisterHandler<SwitchServerMsg>(GetComponent<NetworkZone>().OnClientSwitchServerRequested);
    }

    public void OnServerCharacterCreate_Zones(CharacterCreateMsg message, Player player)
    {
        if (player.startingScene == null) return;
        Database.singleton.SaveCharacterScenePath(player.name, player.startingScene.ScenePath);
    }

    public void OnClientCharactersAvailable_Zones(CharactersAvailableMsg message)
    {
        string autoSelectCharacter = GetComponent<NetworkZone>().autoSelectCharacter;
        int index = message.characters.ToList().FindIndex(c => c.name == autoSelectCharacter);
        if (index != -1)
        {
            // send character select message
            print("[Zones]: autoselect " + autoSelectCharacter + "(" + index + ")");

            NetworkClient.Ready();
            NetworkClient.Send(new CharacterSelectMsg { index = index });
            GetComponent<NetworkManagerMMO>().ClearPreviews();

            // clear auto select
            autoSelectCharacter = null;
        }
    }

    public void OnStartServer_Zones()
    {

#if !UNITY_EDITOR
        
        // spawn instance processes (if any)
        if(GetComponent<NetworkManager>() != null)
        GetComponent<NetworkZone>().SpawnProcesses();
#endif
    }

    public void OnServerAddPlayer_Zones(string account, GameObject player, NetworkConnection conn, CharacterSelectMsg message)
    {
        // where was the player saved the last time?
        string lastScene = Database.singleton.GetCharacterScenePath(player.name);
        if (lastScene != null && lastScene != SceneManager.GetActiveScene().path)
        {
            print("[Zones]: " + player.name + " was last saved on another scene, transferring to: " + lastScene);

            // ask client to switch server
            conn.Send(new SwitchServerMsg{scene=lastScene, characterName=player.name});

            // immediately destroy so nothing messes with the new
            // position and so it's not saved again etc.
            NetworkServer.Destroy(player);
        }
    }
}

// database hooks via addon system /////////////////////////////////////////////
public partial class Database
{
    public class character_scene
    {

        [PrimaryKey]
        [NotNull]
        public string character { get; set;}

        [NotNull]
        public string scene { get; set; }
    }
    public class zones_online
    {
        [NotNull]
        public string online { get; set; }

        public zones_online() { }

        public zones_online(string name, string isOnline)
        { 
            online = isOnline;
        }
    }
    private void Connect_Zone()
    {
        connection.CreateTable<character_scene>();
        connection.CreateTable<zones_online>();
    }

    // a character is online on any of the servers if the online string is not
    // empty and if the time difference is less than the save interval * 2
    // (* 2 to have some tolerance)
    public bool IsCharacterOnlineAnywhere(string characterName)
    {
        float saveInterval = ((NetworkManagerMMO)NetworkManager.singleton).saveInterval;
        object obj = connection.FindWithQuery<characters>("SELECT online FROM characters WHERE name=?", characterName);
        if (obj != null)
        {
            string online = (string)obj;
            if (online != "")
            {
                DateTime time = DateTime.Parse(online);
                double elapsedSeconds = (DateTime.UtcNow - time).TotalSeconds;

                return elapsedSeconds < saveInterval * 2;
            }
        }
        return false;
    }

    public bool AnyAccountCharacterOnline(string account)
    {
        List<string> characters = CharactersForAccount(account);
        return characters.Any(IsCharacterOnlineAnywhere);
    }

    public string GetCharacterScenePath(string characterName)
    {
        character_scene characterScene = connection.FindWithQuery<character_scene>("SELECT scene FROM character_scene WHERE character=?", characterName);
        if (characterScene != null)
            return characterScene.scene;

        return "";
    }

    public void SaveCharacterScenePath(string characterName, string scene)
    {
        connection.InsertOrReplace(new character_scene
        {
            character = characterName,
            scene = scene
        });
    }
    public double TimeElapsedSinceMainZoneOnline()
    {
        zones_online onlineInfo = connection.FindWithQuery<zones_online>("SELECT online FROM zones_online");
        if (onlineInfo != null && !string.IsNullOrEmpty(onlineInfo.online))
        {
            DateTime time = DateTime.Parse(onlineInfo.online);
            return (DateTime.UtcNow - time).TotalSeconds;
        }

        return Mathf.Infinity;
    }

    // should only be called by main zone
    public void SaveMainZoneOnlineTime()
    {
        // online status:
        //   '' if offline (if just logging out etc.)
        //   current time otherwise
        // -> it uses the ISO 8601 standard format
        string onlineString = DateTime.UtcNow.ToString("s");
        connection.Execute("DELETE FROM zones_online");
        connection.InsertOrReplace(new zones_online
        {
            online = onlineString
        });
    }
}