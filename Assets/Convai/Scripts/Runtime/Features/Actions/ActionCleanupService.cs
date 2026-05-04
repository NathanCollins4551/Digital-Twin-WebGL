using UnityEngine;
using System.Collections;
using Convai.Scripts.Runtime.Core;
using Convai.Scripts.Runtime.Features;
using Convai.Scripts.Runtime.Features.Actions;

public class ActionCleanupService : MonoBehaviour
{
    private HighlightingService _highlightingService;
    private ConvaiActionsHandler _actionsHandler;
    private ConvaiNPC _npc;

    private GameObject _currentlyHighlightedTarget = null;
    
    // We listen for both "Highlight" and "Show Me" actions
    private readonly string[] _highlightActionNames = { "Highlight", "Show Me" };

    void Start()
    {
        _highlightingService = FindFirstObjectByType<HighlightingService>();
        _actionsHandler = GetComponent<ConvaiActionsHandler>();
        _npc = GetComponent<ConvaiNPC>();

        if (_actionsHandler != null)
            _actionsHandler.ActionStarted += OnActionStarted;

        if (ConvaiGRPCWebAPI.Instance != null)
        {
            ConvaiGRPCWebAPI.Instance.OnCharacterSpeakingChanged += HandleGlobalSpeakingChanged;
        }
    }

    private void OnActionStarted(string actionName, GameObject target)
    {
        // If Steve starts showing or highlighting something, track it for cleanup
        foreach (string name in _highlightActionNames)
        {
            if (actionName.Equals(name, System.StringComparison.OrdinalIgnoreCase))
            {
                _currentlyHighlightedTarget = target;
                break;
            }
        }
    }

    private void HandleGlobalSpeakingChanged(bool isTalking)
    {
        // When Steve STOPS talking, wait a moment then dim the glow
        if (!isTalking && _npc != null && ConvaiGRPCWebAPI.Instance.CurrentInteractingNPC == _npc)
        {
            if (_currentlyHighlightedTarget != null)
            {
                StartCoroutine(DelayedUnhighlight(2.5f)); // 2.5s gives the user time to look
            }
        }
    }

    private IEnumerator DelayedUnhighlight(float delay)
    {
        yield return new WaitForSeconds(delay);
        
        if (_currentlyHighlightedTarget != null && _highlightingService != null)
        {
            _highlightingService.DisableHighlight(_currentlyHighlightedTarget);
            Debug.Log($"[Cleanup] Glow removed from {_currentlyHighlightedTarget.name}");
        }
        
        _currentlyHighlightedTarget = null;
    }

    private void OnDestroy()
    {
        if (_actionsHandler != null) _actionsHandler.ActionStarted -= OnActionStarted;
        if (ConvaiGRPCWebAPI.Instance != null)
        {
            ConvaiGRPCWebAPI.Instance.OnCharacterSpeakingChanged -= HandleGlobalSpeakingChanged;
        }
    }
}