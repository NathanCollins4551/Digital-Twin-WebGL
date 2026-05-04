using System.Collections;
using Convai.Scripts.Runtime.LoggerSystem;
using Convai.Scripts.Runtime.Features;
using UnityEngine;
using System.Linq;
using Convai.Scripts.Runtime.Features.CustomActions;

namespace Convai.Scripts.Runtime.Features.Actions.CustomActions
{
    public class ShowMeAction : ICustomAction
    {
        public string ActionName => "Show Me";

        private ConvaiActionsHandler _handler;
        
        // References to other action classes for delegation
        private ICustomAction _moveToAction;
        private ICustomAction _highlightAction;

        public void Initialize(ConvaiActionsHandler handler)
        {
            _handler = handler;
            
            // We initialize the sub-actions required for the "Show Me" sequence.
            // This ensures "Show Me" behaves as a high-level orchestrator.
            
            // 1. Initialize MoveToAction for walking to the printer
            _moveToAction = new MoveToAction();
            _moveToAction.Initialize(handler);

            // 2. Initialize HighlightAction for the Glow effect
            // This will use the logic we just updated to pass 'null' (Material Defaults)
            _highlightAction = new HighlightAction();
            _highlightAction.Initialize(handler);
        }

        public IEnumerator Execute(GameObject target)
        {
            if (target == null)
            {
                ConvaiLogger.Warn($"[{ActionName}] Failed: Target object is null.", ConvaiLogger.LogCategory.Actions);
                yield break;
            }

            // Signal the start of the orchestration
            _handler.SignalActionStarted(ActionName, target);
            
            ConvaiLogger.DebugLog($"[Actions] Starting 'Show Me' sequence for {target.name}", ConvaiLogger.LogCategory.Actions);

            // --- STEP 1: Move to the target ---
            if (_moveToAction != null)
            {
                ConvaiLogger.DebugLog($"[Actions] 'Show Me' -> Phase 1: Moving to {target.name}", ConvaiLogger.LogCategory.Actions);
                yield return _moveToAction.Execute(target); 
            }
            else
            {
                ConvaiLogger.Error($"[{ActionName}] MoveToAction missing. Skipping movement.", ConvaiLogger.LogCategory.Actions);
            }
            
            // --- STEP 2: Highlight the target ---
            // This happens once Steve arrives at the destination
            if (_highlightAction != null)
            {
                ConvaiLogger.DebugLog($"[Actions] 'Show Me' -> Phase 2: Highlighting {target.name}", ConvaiLogger.LogCategory.Actions);
                
                // This calls the HighlightAction.Execute which now uses our 
                // updated Service (handling multi-materials and HDR glow).
                yield return _highlightAction.Execute(target);
            }
            else
            {
                ConvaiLogger.Error($"[{ActionName}] HighlightAction missing. Skipping highlight.", ConvaiLogger.LogCategory.Actions);
            }

            ConvaiLogger.DebugLog($"[Actions] 'Show Me' sequence completed for {target.name}", ConvaiLogger.LogCategory.Actions);
            
            // Signal the end of the orchestration
            _handler.SignalActionEnded(ActionName, target);
        }
    }
}