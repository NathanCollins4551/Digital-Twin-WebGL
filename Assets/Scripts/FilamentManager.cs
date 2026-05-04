using UnityEngine;
using System.Collections.Generic;
using Newtonsoft.Json;
using System.Runtime.InteropServices;

// Handles the overall logic for filament spools, keeping track of where they are vs where they should be.
public class FilamentManager : MonoBehaviour {
    [DllImport("__Internal")]
    private static extern void OpenInventoryStream();

    [System.Serializable]
    public struct FilamentData {
        // maps colors from the json payload to actual unity prefabs
        public string colorName;
        public GameObject filamentObject;
    }

    public List<FilamentData> filamentLibrary;
    [Header("Assign Zone_Left Spots Here")]
    public List<Transform> zoneASpots; 
    [Header("Assign Zone_Right Spots Here")]
    public List<Transform> zoneBSpots; 
    public LayerMask filamentLayer;

    private ZoneSnapshotResponseDto _lastState;
    private Dictionary<string, GameObject> _colorToObj = new Dictionary<string, GameObject>();
    private Dictionary<string, Vector3> _colorToHomePos = new Dictionary<string, Vector3>();
    private Dictionary<string, Quaternion> _colorToHomeRot = new Dictionary<string, Quaternion>();
    
    [HideInInspector] public bool isNpcBusy = false;
    private bool _isInitialized = false;

    void Start() {
        // save initial shelf positions so we know exactly where to send them when they get removed from the table
        foreach (var data in filamentLibrary) {
            if (data.filamentObject == null) continue;
            string lowColor = data.colorName.ToLower().Trim();
            _colorToObj[lowColor] = data.filamentObject;
            _colorToHomePos[lowColor] = data.filamentObject.transform.position;
            _colorToHomeRot[lowColor] = data.filamentObject.transform.rotation;
            data.filamentObject.layer = LayerMask.NameToLayer("Filament");
        }
        
        Debug.Log("<color=white>[System] FilamentManager started. Initial shelf positions saved.</color>");
        
        #if !UNITY_EDITOR && UNITY_WEBGL
            OpenInventoryStream();
        #endif
    }

    public void OnInventoryUpdate(string json) {
        Debug.Log($"<color=lime><b>[C# RECV]</b></color> {json}");

        try {
            // called from the browser side when we get new camera data. try to parse it and update the scene.
            var newState = JsonConvert.DeserializeObject<ZoneSnapshotResponseDto>(json);
            if (newState == null) {
                Debug.LogWarning("[C#] Deserialized JSON is null.");
                return;
            }

            if (!_isInitialized) {
                Debug.Log("<color=cyan>[C#] Initializing startup positions...</color>");
                InitializeObjectPlacement(newState);
                _lastState = newState;
                _isInitialized = true;
                return;
            }

            // don't try to update the scene if the steve is already in the middle of carrying something
            if (isNpcBusy) {
                Debug.Log("<color=grey>[C#] Update ignored - NPC is moving.</color>");
                return;
            }

            DetectMovement(_lastState, newState);
            _lastState = newState;
        }
        catch (System.Exception e) {
            Debug.LogError($"[C#] JSON Error: {e.Message}");
        }
    }

    private void DetectMovement(ZoneSnapshotResponseDto oldState, ZoneSnapshotResponseDto newState) {
        HashSet<Transform> reservedSpots = new HashSet<Transform>();
        foreach (var color in _colorToObj.Keys) {
            ProcessColor(color, oldState, newState, reservedSpots);
        }
    }

    private void ProcessColor(string color, ZoneSnapshotResponseDto oldState, ZoneSnapshotResponseDto newState, HashSet<Transform> reservedSpots) {
        GameObject obj = _colorToObj[color];
        
        int oldA = GetColorCount(oldState, "Zone_Left", color);
        int newA = GetColorCount(newState, "Zone_Left", color);
        int oldB = GetColorCount(oldState, "Zone_Right", color);
        int newB = GetColorCount(newState, "Zone_Right", color);

        bool onTable = (newA > 0 || newB > 0);
        bool wasOnTable = (oldA > 0 || oldB > 0);

        // figures out if a spool appeared, disapeared, or just moved across the table.
        // we teleport them if they are just appearing or leaving, but make the NPC carry it if it's moving between the left and right zones
        if (wasOnTable && !onTable) {
            Debug.Log($"<color=yellow>[Vanish] {color} going home.</color>");
            TeleportToHome(color);
        }
        else if (!wasOnTable && onTable) {
            string zone = (newA > 0) ? "Zone_Left" : "Zone_Right";
            Transform target = GetAvailableSpot(zone, reservedSpots);
            if (target != null) {
                Debug.Log($"<color=cyan>[Appear] {color} popping to {zone}.</color>");
                reservedSpots.Add(target);
                TeleportToTable(obj, target);
            }
        }
        else if ((oldA > 0 && newB > 0) || (oldB > 0 && newA > 0)) {
            string targetZone = (newB > 0) ? "Zone_Right" : "Zone_Left";
            Transform target = GetAvailableSpot(targetZone, reservedSpots);
            if (target != null) {
                Debug.Log($"<color=orange>[NPC] {color} transferring to {targetZone}.</color>");
                reservedSpots.Add(target);
                TriggerAction(obj, obj.transform, target);
            }
        }
    }

    private void TeleportToHome(string color) {
        _colorToObj[color].transform.SetParent(null);
        _colorToObj[color].transform.SetPositionAndRotation(_colorToHomePos[color], _colorToHomeRot[color]);
        Physics.SyncTransforms();
    }

    private void TeleportToTable(GameObject obj, Transform spot) {
        obj.transform.SetParent(null);
        obj.transform.SetPositionAndRotation(spot.position, Quaternion.identity);
        obj.transform.localScale = Vector3.one;
        Physics.SyncTransforms();
    }

    private Transform GetAvailableSpot(string zone, HashSet<Transform> reservedSpots) {
        // grabs the first empty transform in a zone so we don't accidentally stack filaments on top of each other
        List<Transform> spots = (zone == "Zone_Left") ? zoneASpots : zoneBSpots;
        foreach (var spot in spots) {
            if (!IsSpotOccupied(spot) && !reservedSpots.Contains(spot)) return spot;
        }
        return null;
    }

    private bool IsSpotOccupied(Transform spot) {
        // tiny physics check just to be absolutely sure the spot is actually clear. 
        // bump up the 0.15f radius here if your spools are bigger.
        Collider[] colliders = Physics.OverlapSphere(spot.position, 0.15f, filamentLayer);
        foreach (var c in colliders) {
            if (_colorToObj.ContainsValue(c.gameObject)) return true;
        }
        return false;
    }

    public void InitializeObjectPlacement(ZoneSnapshotResponseDto state) {
        HashSet<Transform> reserved = new HashSet<Transform>();
        foreach (var color in _colorToObj.Keys) TeleportToHome(color);
        if (state.zones == null) return;

        foreach (var zoneName in state.zones.Keys) {
            if (state.zones[zoneName].colors == null) continue;
            foreach (var colorPair in state.zones[zoneName].colors) {
                string lowColor = colorPair.Key.ToLower().Trim();
                if (_colorToObj.ContainsKey(lowColor) && colorPair.Value > 0) {
                    Transform target = GetAvailableSpot(zoneName, reserved);
                    if (target != null) {
                        reserved.Add(target);
                        TeleportToTable(_colorToObj[lowColor], target);
                    }
                }
            }
        }
    }

    private int GetColorCount(ZoneSnapshotResponseDto state, string zoneName, string color) {
        if (state?.zones != null) {
            foreach (var zoneKey in state.zones.Keys) {
                if (zoneKey.Replace(" ", "_").ToLower() == zoneName.ToLower()) {
                    var colors = state.zones[zoneKey].colors;
                    if (colors != null) {
                        foreach (var colorKey in colors.Keys) {
                            if (colorKey.ToLower().Trim() == color.ToLower().Trim()) return colors[colorKey];
                        }
                    }
                }
            }
        }
        return 0;
    }

    private void TriggerAction(GameObject item, Transform from, Transform to) {
        var actionScript = FindObjectOfType<FilamentMovementController>();
        if (actionScript != null) {
            isNpcBusy = true;
            actionScript.ExecuteFilamentMove(item, from, to);
        }
    }
}