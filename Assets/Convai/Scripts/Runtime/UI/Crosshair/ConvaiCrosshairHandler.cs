using System.Collections.Generic;
using System.Linq;
using Convai.Scripts.Runtime.Features;
using Convai.Scripts.Runtime.LoggerSystem;
using UnityEngine;

namespace Convai.Scripts.Runtime.UI
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Convai/Crosshair Handler")]
    public class ConvaiCrosshairHandler : MonoBehaviour
    {
        private Camera _camera;
        private Dictionary<GameObject, string> _interactableReferences;
        private ConvaiInteractablesData _interactablesData;

        private void Awake()
        {
            _camera = Camera.main;

            _interactablesData = FindObjectOfType<ConvaiInteractablesData>();
            if (_interactablesData == null) return;

            _interactableReferences = new Dictionary<GameObject, string>();
            foreach (ConvaiInteractablesData.Object eachObject in _interactablesData.Objects)
                _interactableReferences[eachObject.gameObject] = eachObject.Name;
            foreach (ConvaiInteractablesData.Character eachCharacter in _interactablesData.Characters)
                _interactableReferences[eachCharacter.gameObject] = eachCharacter.Name;
        }

        public string FindPlayerReferenceObject()
        {
            if (_interactablesData == null || _camera == null) return "None";
            
            Ray ray = _camera.ScreenPointToRay(UnityEngine.InputSystem.Mouse.current.position.ReadValue());

            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                if (_interactablesData.DynamicMoveTargetIndicator != null)
                {
                    _interactablesData.DynamicMoveTargetIndicator.position = hit.point;
                }

                string reference = FindInteractableReference(hit.transform.gameObject);
                ConvaiLogger.DebugLog($"Player is looking at: {reference}", ConvaiLogger.LogCategory.Actions);
                return reference;
            }

            return "None";
        }

        private string FindInteractableReference(GameObject hitGameObject)
        {
            foreach (KeyValuePair<GameObject, string> kvp in _interactableReferences.Where(kvp => hitGameObject.GetComponentInParent<Transform>() == kvp.Key.transform))
                return kvp.Value;

            return "None";
        }
    }
}