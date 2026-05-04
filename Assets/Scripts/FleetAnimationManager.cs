using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;

[Serializable]
public class FleetStatusList { public List<PrinterStatusCheck> printers; }

// keeps all the 3d printers in the scene synced with the real world backend data
public class FleetAnimationManager : MonoBehaviour
{
    [DllImport("__Internal")]
    private static extern void FetchAllPrinterStatuses();

    private PrinterAnimationHandler[] allHandlers;

    void Start()
    {
        allHandlers = FindObjectsByType<PrinterAnimationHandler>(FindObjectsSortMode.None);
        Debug.Log($"[AnimManager] Monitoring {allHandlers.Length} machines.");
        
        StartCoroutine(FleetCheckHeartbeat());
    }

    IEnumerator FleetCheckHeartbeat()
    {
        while (true)
        {
            #if !UNITY_EDITOR && UNITY_WEBGL
                FetchAllPrinterStatuses();
            #else
                foreach(var h in allHandlers) 
                    if(!string.IsNullOrEmpty(h.GetDeviceId())) h.SetRunningState(true);
            #endif

            // ping the server every 60s. change this if it's lagging the frontend.
            yield return new WaitForSeconds(60f);
        }
    }

    public void OnFleetStatusUpdate(string json)
    {
        // unity's json parser is super basic and hates root arrays, so we wrap it in a dummy object here
        string formattedJson = "{\"printers\":" + json + "}";
        try
        {
            FleetStatusList list = JsonUtility.FromJson<FleetStatusList>(formattedJson);
            
            foreach (var handler in allHandlers)
            {
                string id = handler.GetDeviceId();
                if (string.IsNullOrEmpty(id)) continue;

                PrinterStatusCheck status = list.printers.Find(p => p.deviceId == id);
                
                bool shouldAnimate = (status != null && status.isRunning && status.isSimulationControlled);
                
                handler.SetRunningState(shouldAnimate);
            }
        }
        catch (Exception e) 
        { 
            Debug.LogError($"[AnimManager] Sync Error: {e.Message}"); 
        }
    }
}