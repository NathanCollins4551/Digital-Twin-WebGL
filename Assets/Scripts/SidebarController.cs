using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Runtime.InteropServices;
using UnityEngine.InputSystem;

[Serializable]
public class TelemetryUpdate { 
    public string DeviceId; 
    public string PrintStatus; 
    public float ProgressPercent; 
    public float NozzleTempC; 
    public float BedTempC;
    public int CurrentLayer;
    public int TotalLayers;
}

[Serializable]
public class PrinterStatusCheck { 
    public string deviceId; 
    public bool isRunning; 
    public bool isSimulationControlled;
    public string printStatus; 
    public float latestProgressPercent; 
    public float avgNozzleTempC; 
    public float avgBedTempC;
    public int currentLayer;
    public int totalLayers;
}

[Serializable]
public class TelemetrySnapshot { 
    public float latestProgressPercent; 
    public float avgNozzleTempC; 
    public float avgBedTempC; 
    public int currentLayer; 
    public int totalLayers; 
}

// drives the fly-out UI menu when you click a machine. manages the live data connection.
public class SidebarController : MonoBehaviour
{
    // bridge methods to our react frontend so we can tap into the real server sent events
    [DllImport("__Internal")] private static extern void OpenSSEStream(string deviceId);
    [DllImport("__Internal")] private static extern void CloseSSEStream();
    [DllImport("__Internal")] private static extern void CheckPrinterStatus(string deviceId);

    [Header("Top Info")]
    public TextMeshProUGUI stationNameDisplay; 
    public TextMeshProUGUI modelNameDisplay;   
    public Image statusBox;
    public TextMeshProUGUI statusText;

    [Header("UI ProgressBars")]
    public ProgressBar progressPb;   
    public ProgressBar nozzlePb;
    public ProgressBar bedPb;

    void Start() => CloseDashboard();

    public void OpenDashboard(string stationID, string modelName, string deviceId)
    {
        CloseSSEOnly(); 
        this.gameObject.SetActive(true);

        if (stationNameDisplay != null) stationNameDisplay.text = "Station: " + stationID;
        if (modelNameDisplay != null) modelNameDisplay.text = modelName;
        
        if (string.IsNullOrEmpty(deviceId)) { ShowUnregisteredState(); return; }
        
        ShowIdleState("FETCHING...");

        #if !UNITY_EDITOR && UNITY_WEBGL
            CheckPrinterStatus(deviceId); 
        #endif
    }

    public void InitializeDashboardState(string json)
    {
        try {
            PrinterStatusCheck status = JsonUtility.FromJson<PrinterStatusCheck>(json);
        
            if (status != null && status.isRunning && status.isSimulationControlled) {
                if (statusText != null) statusText.text = status.printStatus.ToUpper();
                if (statusBox != null) statusBox.color = Color.green;

                UpdateUI(status.printStatus, status.latestProgressPercent, status.avgNozzleTempC, status.avgBedTempC, status.currentLayer, status.totalLayers);

#if !UNITY_EDITOR && UNITY_WEBGL
                OpenSSEStream(status.deviceId);
#endif
            } else {
#if !UNITY_EDITOR && UNITY_WEBGL
                CloseSSEStream(); 
#endif
                ShowIdleState("IDLE");
            }
        } catch { 
            // json parse failed, probably a weird response from the backend
            ShowIdleState("IDLE"); 
        }
    }

    public void SetInitialTelemetry(string json)
    {
        try {
            TelemetrySnapshot snap = JsonUtility.FromJson<TelemetrySnapshot>(json);
            if (snap != null) {
                UpdateUI("RUNNING", snap.latestProgressPercent, snap.avgNozzleTempC, snap.avgBedTempC, snap.currentLayer, snap.totalLayers);
            }
        } catch { }
    }

    public void OnStreamUpdate(string json)
    {
        // the SSE stream spits out keepalive messages sometimes, just ignore anything that isn't a solid json block
        if (json.Contains("message") || !json.Trim().StartsWith("{")) return;
        try {
            TelemetryUpdate data = JsonUtility.FromJson<TelemetryUpdate>(json);
            if (data != null) {
                UpdateUI(data.PrintStatus, data.ProgressPercent, data.NozzleTempC, data.BedTempC, data.CurrentLayer, data.TotalLayers);
            }
        } catch (Exception e) { Debug.LogError($"[Sidebar] Stream Error: {e.Message}"); }
    }

    private void UpdateUI(string status, float progress, float nozzle, float bed, int curL = 0, int totL = 0) {
        if (!string.IsNullOrEmpty(status)) {
            if (statusText != null) statusText.text = status.ToUpper();
            if (statusBox != null) statusBox.color = (status.ToUpper().Contains("RUNNING") || status.ToUpper().Contains("PRINTING")) ? Color.green : Color.gray;
        }

        if (progressPb != null) { 
            progressPb.Title = "Progress"; 
            progressPb.SetProgressWithLayers(progress, curL, totL); 
        }
        if (nozzlePb != null) { 
            progressPb.Title = "Nozzle"; 
            // guessing 300C is roughly max temp for the UI bar scaling. might need tweaked if we get high temp machines.
            nozzlePb.SetValueWithRawText((nozzle / 300f) * 100f, nozzle); 
        }
        if (bedPb != null) { 
            bedPb.Title = "Bed"; 
            bedPb.SetValueWithRawText(bed, bed); 
        }
    }

    private void ShowIdleState(string label) {
        if (statusText != null) statusText.text = label;
        if (statusBox != null) statusBox.color = Color.gray;
        if (progressPb != null) { progressPb.Title = "Progress"; progressPb.SetToNA(); }
        if (nozzlePb != null) { nozzlePb.Title = "Nozzle"; nozzlePb.SetToNA(); }
        if (bedPb != null) { bedPb.Title = "Bed"; bedPb.SetToNA(); }
    }

    private void ShowUnregisteredState() {
        if (statusText != null) statusText.text = "Offline";
        if (statusBox != null) statusBox.color = Color.black;
        if (progressPb != null) { progressPb.Title = "Progress"; progressPb.SetToNA(); }
        if (nozzlePb != null) { nozzlePb.Title = "Nozzle"; nozzlePb.SetToNA(); }
        if (bedPb != null) { bedPb.Title = "Bed"; bedPb.SetToNA(); }
    }

    private void CloseSSEOnly() {
        #if !UNITY_EDITOR && UNITY_WEBGL
            CloseSSEStream();
        #endif
    }

    public void CloseDashboard() {
        CloseSSEOnly();
        this.gameObject.SetActive(false);
    }

    void Update() {
        if (this.gameObject.activeSelf && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            CloseDashboard();
    }
}