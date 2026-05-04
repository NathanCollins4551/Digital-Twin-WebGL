using UnityEngine;


// Marker component for objects that can be highlighted by the HighlightingService.

public class HighlightableObject : MonoBehaviour
{
    [Tooltip("Unique id for this highlightable object. Use this id when calling the HighlightingService from AI.")]
    public string elementId;

}