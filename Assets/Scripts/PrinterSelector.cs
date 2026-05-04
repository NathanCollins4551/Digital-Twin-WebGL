using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

public class PrinterSelector : MonoBehaviour
{
    private Camera _cam;

    [Header("Setup")]
    public SidebarController sidebar; 
    public LayerMask interactableLayer;
    public float reachDistance = 100f;

    void Awake() => _cam = GetComponent<Camera>();

    void Update()
    {
        if (Pointer.current == null || !Pointer.current.press.wasPressedThisFrame) return;

        // stop the raycast if the user is actually just clicking a UI button that happens to be over a printer
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            return;
        }

        Vector2 mousePos = Pointer.current.position.ReadValue();
        Ray ray = _cam.ScreenPointToRay(mousePos);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, reachDistance, interactableLayer))
        {
            PrinterObject printerData = hit.collider.GetComponentInParent<PrinterObject>();

            if (printerData != null && sidebar != null)
            {
                // grabs the station name directly from the heirarchy name (so make sure the prefab is named properly in the scene!)
                string stationName = printerData.gameObject.name;
                string modelName = printerData.printerModel;
                string deviceId = printerData.deviceId;

                Debug.Log($"<color=green>[Selector]</color> Station: {stationName} | Model: {modelName}");
                
                sidebar.OpenDashboard(stationName, modelName, deviceId);
            }
            else if (printerData == null)
            {
                Debug.LogWarning($"<color=orange>[Selector]</color> Hit {hit.collider.name} but no 'PrinterObject' script found!");
            }
        }
    }
}