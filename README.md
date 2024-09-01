# NetworkZones
 Network Zones Addon for uMMORPG 2D Remastered

NetworkZones for uMMORPG by vis2k reworked by Xenepherth

--------------------------------------------------------------------------------
Usage Guide:
--------------------------------------------------------------------------------

* Copy NetworkZones folder into uMMORPG Addons folder
* Add NetworkZone component to NetworkManager
  * assign the public components by dragging networkmanager's components into it
  * add [your-path-here]/World2.unity to scene paths to spawn
* Create a GameObject->3D->Cylinder for the portal, place it somewhere in the scene
  (for uMMORPG 2D just add a portal-like sprite with a 2D collider)
  * enable 'Is Trigger' in the Collider
  * add NetworkZonePortal component to it
  * change ScenePath to path to [your-path-here]/World2.unity

* Do NetworkManagerMMO's Don't Destroy On Load to inactive
* Add DontDestroyOnLoad component to:
  * Canvas
  * EventSystem
  * MinimapCamera (because Canvas/Minimap references it)
* Save scene, select scene in Project Area, duplicate via ctrl+d
  * rename new scene to 'World2'
  * go to File->Build Settings and add World2
  * open it
  * delete:
    * Canvas
    * MinimapCamera
    * NetworkManager
  * select Portal, change scene path to [your-path-here]/World.unity

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
