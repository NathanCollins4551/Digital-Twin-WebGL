using UnityEngine;
using System.Collections;
using Convai.Scripts.Runtime.Features.CustomActions;
using Convai.Scripts.Runtime.LoggerSystem;

namespace Convai.Scripts.Runtime.Features.Actions.CustomActions 
{
    public class HighlightAction : ICustomAction
    {
        public string ActionName => "Highlight"; 

        private ConvaiActionsHandler _handler;
        private HighlightingService _highlightingService; 

        public void Initialize(ConvaiActionsHandler handler)
        {
            _handler = handler;
            // Finds the service in your makerspace scene
            _highlightingService = Object.FindAnyObjectByType<HighlightingService>();
        }

        public IEnumerator Execute(GameObject target)
        {
            _handler.SignalActionStarted(ActionName, target); 

            if (_highlightingService != null && target != null)
            {
                ConvaiLogger.DebugLog($"[Actions] Highlighting {target.name} using Material settings.", ConvaiLogger.LogCategory.Actions);
                
                // PASSING NULL: This ensures your HDR Intensity and Alpha settings are used!
                _highlightingService.EnableHighlight(target, null);
            }
            else
            {
                ConvaiLogger.Warn($"[{ActionName}] Failed: Target or Service missing.", ConvaiLogger.LogCategory.Actions);
            }

            yield return null; 
            _handler.SignalActionEnded(ActionName, target);
        }
    }
}