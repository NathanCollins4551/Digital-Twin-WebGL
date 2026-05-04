using UnityEngine;

// just a dumb data bucket that sits on the root of the 3d model so the raycaster can find its identity
public class PrinterObject : MonoBehaviour
{
    [Header("Identity")]
    [Tooltip("The hardware model (e.g., Prusa MK4, Ender 3)")]
    public string printerModel; 
    
    [Tooltip("The technical ID for the SSE backend")]
    public string deviceId;           
}