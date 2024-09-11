# NetworkZones
 Network Zones Addon for uMMORPG 2D Remastered



NetworkZones for uMMORPG by vis2k Reworked by Xenepherth

--------------------------------------------------------------------------------
Usage Guide:
--------------------------------------------------------------------------------
Get Eflatun.SceneReference from https://github.com/starikcetin/Eflatun.SceneReference

* Copy NetworkZones folder into uMMORPG Addons folder
* Add NetworkZone component to NetworkManager
  * assign the public components by dragging networkmanager's components into it
  * add World2.unity to scene reference to spawn
* Add NetworkZonePortal component to your Scene for teleport
* In NetworkManagerMMO do inactive Don't Destroy On Load
* Add DontDestroyOnLoad component to:
  * Canvas
  * MinimapCamera (because Canvas/Minimap references it)
  * Network Manager
* Save scene, select scene in Project Area, duplicate via ctrl+d
  * rename new scene to 'World2'
  * go to File->Build Settings and add World2
  * open it
  * delete:
    * Canvas
    * MinimapCamera
    * NetworkManager
  * select Portal, change scene reference to World

test it:
* press build and run
  * select server-only, notice how it automatically launchers another zone process
* press play in the editor
  * Login
  * Create/Select Character
  * Run into the portal to see the other zone

--------------------------------------------------------------------------------
Notes:
--------------------------------------------------------------------------------
* chat doesn't work across zones yet. using an irc server is a possible solution
* sqlite does allow concurrent access. but if you get errors, consider mysql.