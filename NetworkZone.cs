// Spawns one server zone process per scene.
// Also shuts down other zones when the main one died or was terminated.
//
// IMPORTANT: we do not EVER set manager.onlineScene/offlineScene. This part of
// UNET is broken and will cause all kinds of random issues when the server
// forces the client to reload the scene while receiving initialization messages
// already. Instead we always load the scene manually, then connect to the
// server afterwards.
using System;
using System.Diagnostics;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using Mirror;
using kcp2k;
using SQLite;
using System.Collections;
using Eflatun.SceneReference;

[RequireComponent(typeof(NetworkManager))]
[RequireComponent(typeof(KcpTransport))]
public class NetworkZone : MonoBehaviour
{
    public bool active;
    // component
    public NetworkManager manager;
    public KcpTransport transport;

    // paths to the scenes to spawn
    [SerializeField] private List<SceneReference> scenesToSpawn;

    public GameObject LoadingScreen;
    public Image loadingBarFill;

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
            string sceneName = scenesToSpawn[index].Name;
            print("[Zones] setting requested port: +" + index);
            transport.port = (ushort)(originalPort + scenesToSpawn[index].BuildIndex);

            print("[Zones] changing server scene: " + sceneName);
            StartCoroutine(WaitOnServerReady());
        }
    }

    private IEnumerator WaitOnServerReady()
    {
        int index = ParseSceneIndexFromArgs();
        const int waitInterval = 1;

        while (!manager.isNetworkActive)
        {
            print("[Zones] waiting on manager.isNetworkActive");
            yield return waitInterval;
        }

        // convert scene path to scene name
        string sceneName = scenesToSpawn[index].Name;

        print("[Zones] switching zone server scene to: " + sceneName);
        manager.ServerChangeScene(sceneName);
    }

    public void SpawnProcesses()
    {
        if (ParseSceneIndexFromArgs() == 0)
        {
            // write zone online time every few seconds
            print("[Zones]: main process starts online writer...");
            InvokeRepeating("WriteOnlineTime", 0, writeInterval);

            print("[Zones]: main process spawns siblings...");

            // -- Spawn Siblings

            for (int i = 1; i < scenesToSpawn.Count; ++i)
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
            NetworkManager.singleton.enabled = false;
            NetworkManager.singleton.enabled = true;


            Transport.active.enabled = false; // don't receive while switching scenes
            print("[Zones]: loading required scene: " + message.scene);
            autoSelectCharacter = message.characterName;
            // load requested scene and make sure to auto connect when it's done
            LoadScene(message.scene);
            autoConnectClient = true;
        }
    }

    // on scene loaded /////////////////////////////////////////////////////////
    public void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        UnityEngine.Debug.Log("OnSceneLoaded: " + scene.name);

        // Server
        if (NetworkServer.active)
        {
            // write online time every few seconds and check main zone alive every few seconds
            print("[Zones]: starting online alive check for zone: " + scene.name);
            InvokeRepeating("AliveCheck", timeoutInterval, timeoutInterval);

        }

        // Client
        // client started and needs to automatically connect?
        if (autoConnectClient)
        {
            transport.port = (ushort)(originalPort + scene.buildIndex);
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
    public void LoadScene(string sceneName)
    {
        StartCoroutine(LoadSceneAsync(sceneName));
    }
    IEnumerator LoadSceneAsync(string sceneName)
    {
        AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName);
        LoadingScreen.SetActive(true);
        while (!operation.isDone)
        {
            float progressValue = Mathf.Clamp01(operation.progress / 0.9f);
            loadingBarFill.fillAmount = progressValue;
            yield return null;
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
        Database.singleton.SaveCharacterSceneName(player.name, player.startingScene.Name);
    }

    public void OnClientCharactersAvailable_Zones(CharactersAvailableMsg message)
    {
        int index = message.characters.ToList().FindIndex(c => c.name == GetComponent<NetworkZone>().autoSelectCharacter);
        if (index != -1 && GetComponent<NetworkZone>().autoSelectCharacter != null)
        {
            // send character select message
            print("[Zones]: autoselect " + GetComponent<NetworkZone>().autoSelectCharacter + "(" + index + ")");

            NetworkClient.Ready();
            ClearPreviews();
            NetworkClient.Send(new CharacterSelectMsg { index = index });
            GetComponent<NetworkZone>().LoadingScreen.SetActive(false);
            // clear auto select
            GetComponent<NetworkZone>().autoSelectCharacter = null;
        }
    }

    public void OnStartServer_Zones()
    {

#if !UNITY_EDITOR
        // spawn instance processes (if any)
        GetComponent<NetworkZone>().SpawnProcesses();
#endif
    }

    public void OnServerCharacterSelect_Zones(string account, GameObject player, NetworkConnection conn, CharacterSelectMsg message)
    {
        // where was the player saved the last time?
        string lastScene = Database.singleton.GetCharacterSceneName(player.name);
        if (lastScene != null && lastScene != SceneManager.GetActiveScene().name)
        {
            print("[Zones]: " + player.name + " was last saved on another scene, transferring to: " + lastScene);

            // ask client to switch server
            conn.Send(new SwitchServerMsg { scene = lastScene, characterName = player.name });

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
        characters character = connection.FindWithQuery<characters>("SELECT * FROM characters WHERE name=?", characterName);
        if (character != null)
        {
            if (character.online == true)
            {
                var lastsaved = character.lastsaved;
                double elapsedSeconds = (DateTime.UtcNow - lastsaved).TotalSeconds;
                float saveInterval = ((NetworkManagerMMO)NetworkManager.singleton).saveInterval;

                // online if 1 and last saved recently (it's possible that online is
                // still 1 after a server crash, hence last saved detection)
                return character.online == true && elapsedSeconds < saveInterval * 2;
            }
        }
        return false;
    }

    public bool AnyAccountCharacterOnline(string account)
    {
        List<string> characters = CharactersForAccount(account);
        return characters.Any(IsCharacterOnlineAnywhere);
    }

    public string GetCharacterSceneName(string characterName)
    {
        character_scene characterScene = connection.FindWithQuery<character_scene>("SELECT scene FROM character_scene WHERE character=?", characterName);
        if (characterScene != null)
            return characterScene.scene;

        return "";
    }

    public void SaveCharacterSceneName(string characterName, string scene)
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