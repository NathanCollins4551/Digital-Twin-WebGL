using System.Collections;
using Convai.Scripts.Runtime.LoggerSystem;
using Convai.Scripts.Runtime.Features.CustomActions;
using UnityEngine;

namespace Convai.Scripts.Runtime.Features.Actions.CustomActions
{
    public class DisplayPrinterDashboardAction : ICustomAction
    {
        public string ActionName => "Display Printer Dashboard"; 

        private ConvaiActionsHandler _handler;
        private SidebarController _sidebar;

        public void Initialize(ConvaiActionsHandler handler)
        {
            _handler = handler;
            // Find the SidebarController in the scene
            _sidebar = Object.FindAnyObjectByType<SidebarController>();
        }

        public IEnumerator Execute(GameObject target)
        {
            if (target == null)
            {
                ConvaiLogger.Warn($"[{ActionName}] Target is null. AI might be pointing at nothing.", ConvaiLogger.LogCategory.Actions);
                yield break;
            }

            if (_sidebar == null)
            {
                ConvaiLogger.Error($"[{ActionName}] SidebarController not found in scene!", ConvaiLogger.LogCategory.Actions);
                yield break;
            }

            // Get the PrinterObject script from the target to get the Model and DeviceID
            PrinterObject printerData = target.GetComponentInParent<PrinterObject>();
            
            if (printerData == null)
            {
                ConvaiLogger.Warn($"[{ActionName}] Target '{target.name}' does not have a PrinterObject script!", ConvaiLogger.LogCategory.Actions);
                yield break;
            }

            _handler.SignalActionStarted(ActionName, target);
            
            ConvaiLogger.DebugLog($"[{ActionName}] AI opening dashboard for Station: {target.name}", ConvaiLogger.LogCategory.Actions);

            // Extract data based on your new architecture:
            // 1. Station ID = The GameObject Name (e.g., "A1")
            // 2. Printer Model = From the script (e.g., "Prusa MK4")
            // 3. Device ID = From the script (e.g., "SSE_UUID_123")
            
            string stationId = target.name; 
            string modelName = printerData.printerModel;
            string deviceId = printerData.deviceId;

            // Trigger the sidebar with the 3 parameters required by your updated SidebarController
            _sidebar.OpenDashboard(stationId, modelName, deviceId);

            yield return null; 
            
            _handler.SignalActionEnded(ActionName, target);
        }
    }
}