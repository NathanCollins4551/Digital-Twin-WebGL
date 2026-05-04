using System;
using System.Collections.Generic;
using Convai.Scripts.Runtime.Attributes;
using Convai.Scripts.Runtime.LoggerSystem;
using UnityEngine;

namespace Convai.Scripts.Runtime.Core
{
    /// <summary>
    /// Manages which ConvaiNPC is currently active based on player's direct line of sight (Raycast)
    /// and maintains the active state within a specified angle and distance threshold (Persistence).
    /// Implements Singleton pattern. Core component for the Convai SDK interaction flow.
    /// </summary>
    [DefaultExecutionOrder(-101)] // Ensure this runs before components that depend on the active NPC
    public class ConvaiNPCManager : MonoBehaviour
    {
        // Singleton Instance
        public static ConvaiNPCManager Instance { get; private set; }

        [Header("Detection Settings")]
        [Tooltip("Length of the ray used for initial NPC detection via direct hit and max distance check.")]
        [SerializeField] private float detectionDistance = 3.0f;

        [Tooltip("Total angle (degrees) of the cone. If the player looks away from the active NPC beyond half this angle, it deactivates.")]
        [SerializeField] private float detectionFOVAngle = 120f; // Persistence cone angle

        // State (Read Only in Inspector)
        [Header("Current State")]
        [Tooltip("Reference to the NPC currently considered active for interaction.")]
        [ReadOnly] public ConvaiNPC activeConvaiNPC;

        // Internal References & Cache
        private Camera _mainCamera;
        // Cache for ConvaiNPC components to avoid repeated GetComponent calls
        private readonly Dictionary<GameObject, ConvaiNPC> _convaiNPCCache = new();
        // Reference to the NPC that was last determined to be active (either by direct hit or persistence).
        private ConvaiNPC _lastHitNpc;
        // Reusable buffer for RaycastNonAlloc to avoid GC allocations
        private static readonly RaycastHit[] RaycastHits = new RaycastHit[1];

        // Event
        /// <summary>
        /// Fired when the active NPC changes. Passes the new active NPC (or null).
        /// Consumed by systems needing to know the current interaction target (e.g., GRPCWebAPI).
        /// </summary>
        public event Action<ConvaiNPC> OnActiveNPCChanged;

        #region Unity Lifecycle Methods

        private void Awake()
        {
            // Singleton pattern implementation
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                ConvaiLogger.Warn($"Duplicate instance of {nameof(ConvaiNPCManager)} detected on {gameObject.name}. Destroying this one.", ConvaiLogger.LogCategory.Character);
                Destroy(gameObject);
                return;
            }

            // Cache the main camera reference for efficiency
            _mainCamera = Camera.main;
            if (_mainCamera == null)
            {
                ConvaiLogger.Error($"Requires a Camera tagged as 'MainCamera' in the scene. Component disabled.", ConvaiLogger.LogCategory.Character, this);
                enabled = false; // Disable the component if the main camera is missing
            }
        }

        // --- DIGITAL TWIN MODIFICATION: INITIAL NPC LOCK ---
        private void Start()
        {
            if (activeConvaiNPC == null)
            {
                ConvaiNPC npc = FindFirstObjectByType<ConvaiNPC>();
                if (npc != null) UpdateActiveNPCState(npc);
            }
        }

        // Using LateUpdate as NPC activation often depends on final camera position/rotation for the frame
        private void LateUpdate()
        {
            // Only run detection logic if the component is enabled (e.g., camera found)
            if (!enabled) return;

            // --- DIGITAL TWIN MODIFICATION: PERSISTENCE BYPASS ---
            if (activeConvaiNPC == null)
            {
                DetectAndMaintainActiveNPC_RaycastPersistence();
            }
            else if (!activeConvaiNPC.isCharacterActive)
            {
                // Ensure that once an NPC is found, it is forced to stay active
                activeConvaiNPC.isCharacterActive = true;
            }
        }

        #endregion

        #region NPC Detection Logic (Raycast + Persistence)

        /// <summary>
        /// Detects NPCs via direct raycast and maintains/updates the active NPC
        /// based on angle and distance thresholds if the raycast doesn't hit.
        /// </summary>
        private void DetectAndMaintainActiveNPC_RaycastPersistence()
        {
            if (_mainCamera == null) return;

            Transform cameraTransform = _mainCamera.transform;
            Ray ray = new Ray(cameraTransform.position, cameraTransform.forward);
            bool foundConvaiNPCViaRay = false; // Track if the direct ray hit an NPC this frame
            ConvaiNPC nearbyNPCOnRay = null;   // Store the NPC hit by the ray, if any

            // --- Stage 1: Check for direct Raycast hit ---
            if (Physics.RaycastNonAlloc(ray, RaycastHits, detectionDistance) > 0)
            {
                RaycastHit hit = RaycastHits[0];
                nearbyNPCOnRay = GetOrCacheConvaiNPC(hit.transform.gameObject);

                if (nearbyNPCOnRay != null)
                {
                    foundConvaiNPCViaRay = true;
                    if (_lastHitNpc != nearbyNPCOnRay)
                    {
                        ConvaiLogger.DebugLog($"[{nameof(ConvaiNPCManager)}] Player view targeted: {nearbyNPCOnRay.name}", ConvaiLogger.LogCategory.Character);
                        UpdateActiveNPCState(nearbyNPCOnRay); // Activate the newly hit NPC
                    }
                }
            }

            // --- Stage 2: Handle cases where Raycast did NOT hit an NPC (Persistence Check) ---
            if (!foundConvaiNPCViaRay && _lastHitNpc != null)
            {
                Vector3 rayOrigin = ray.origin;
                Vector3 lastNPCPosition = _lastHitNpc.transform.position;
                Vector3 toLastHitNPCDirection = lastNPCPosition - rayOrigin;
                float distanceToLastHitNPC = toLastHitNPCDirection.magnitude;

                bool distanceOutOfRange = distanceToLastHitNPC > detectionDistance * 1.2f;
                bool angleOutOfRange = false;

                if (!distanceOutOfRange)
                {
                    float angleToLastHitNPC = Vector3.Angle(ray.direction, toLastHitNPCDirection.normalized);
                    angleOutOfRange = angleToLastHitNPC > (detectionFOVAngle / 2.0f);
                }

                if (angleOutOfRange || distanceOutOfRange)
                {
                    ConvaiLogger.DebugLog($"[{nameof(ConvaiNPCManager)}] Player left: {(_lastHitNpc != null ? _lastHitNpc.name : "NPC")} - (Angle/Dist out of range)", ConvaiLogger.LogCategory.Character);
                    UpdateActiveNPCState(null); // Deactivate the NPC
                }
            }
        }

        #endregion

        #region State Management & Component Cache

        private void UpdateActiveNPCState(ConvaiNPC newActiveNPC)
        {
            if (activeConvaiNPC != newActiveNPC)
            {
                if (activeConvaiNPC != null)
                {
                    activeConvaiNPC.isCharacterActive = false;
                }

                activeConvaiNPC = newActiveNPC;
                _lastHitNpc = newActiveNPC; // Keep _lastHitNpc synced with active NPC

                if (activeConvaiNPC != null)
                {
                    activeConvaiNPC.isCharacterActive = true;
                    ConvaiLogger.DebugLog($"[{nameof(ConvaiNPCManager)}] Active NPC set to: {(activeConvaiNPC != null ? activeConvaiNPC.name : "None")}", ConvaiLogger.LogCategory.Character);
                }
                else
                {
                    ConvaiLogger.DebugLog($"[{nameof(ConvaiNPCManager)}] Active NPC cleared.", ConvaiLogger.LogCategory.Character);
                }

                // Notify subscribers
                try
                {
                    OnActiveNPCChanged?.Invoke(activeConvaiNPC);
                }
                catch (Exception ex)
                {
                    ConvaiLogger.Error($"Error invoking OnActiveNPCChanged event: {ex.Message}", ConvaiLogger.LogCategory.Character, this);
                }
            }
        }

        private ConvaiNPC GetOrCacheConvaiNPC(GameObject obj)
        {
            if (obj == null) return null;
            if (_convaiNPCCache.TryGetValue(obj, out ConvaiNPC npc))
            {
                if (npc != null) return npc;
                else _convaiNPCCache.Remove(obj);
            }
            npc = obj.GetComponent<ConvaiNPC>();
            if (npc != null)
            {
                _convaiNPCCache[obj] = npc;
            }
            return npc;
        }

        #endregion

        #region Public Methods

        public void SetActiveConvaiNPC(ConvaiNPC newActiveNPC)
        {
            UpdateActiveNPCState(newActiveNPC);
        }

        public ConvaiNPC GetActiveConvaiNPC()
        {
            return activeConvaiNPC;
        }

        #endregion
    }
}