using System;
using System.Linq;
using Convai.Scripts.Runtime.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Convai.Scripts.Runtime.Core
{
    public class ConvaiPlayerInteractionManager : MonoBehaviour
    {
        private ConvaiNPC _convaiNPC;
        private ConvaiChatUIHandler _convaiChatUIHandler;
        private ConvaiCrosshairHandler _convaiCrosshairHandler;
        private ConvaiInputManager _inputManager;
        private ConvaiGRPCWebAPI _grpcWebAPI;
        private TMP_InputField _currentInputField;

        public void Initialize(ConvaiNPC convaiNPC, ConvaiCrosshairHandler convaiCrosshairHandler, ConvaiChatUIHandler convaiChatUIHandler)
        {
            _convaiNPC = convaiNPC ?? throw new ArgumentNullException(nameof(convaiNPC));
            _convaiChatUIHandler = convaiChatUIHandler;
            _convaiCrosshairHandler = convaiCrosshairHandler;
            _inputManager = ConvaiInputManager.Instance ?? throw new InvalidOperationException("InputManager instance not found.");
            _grpcWebAPI = ConvaiGRPCWebAPI.Instance ?? throw new InvalidOperationException("GRPCWebAPI instance not found.");
            SubscribeToInputEvents();
        }

        private void OnEnable() { if (_inputManager != null) SubscribeToInputEvents(); }
        private void OnDisable() { UnsubscribeFromInputEvents(); }

        private void SubscribeToInputEvents()
        {
            if (_inputManager == null) return;
            UnsubscribeFromInputEvents();
            _inputManager.sendText += HandleTextInput;
            _inputManager.toggleChat += HandleToggleChat;
            _inputManager.talkKeyInteract += HandleVoiceInput;
        }

        private void UnsubscribeFromInputEvents()
        {
            if (_inputManager != null)
            {
                _inputManager.sendText -= HandleTextInput;
                _inputManager.toggleChat -= HandleToggleChat;
                _inputManager.talkKeyInteract -= HandleVoiceInput;
            }
        }

        private void HandleTextInput()
        {
            // --- DIGITAL TWIN MODIFICATION: REMOVED NPCManager.activeConvaiNPC CHECK ---
            TMP_InputField inputFieldInScene = FindActiveInputField();
            UpdateCurrentInputFieldCache(inputFieldInScene);

            if (_currentInputField != null && _currentInputField.isFocused)
            {
                string inputText = _currentInputField.text;
                if (!string.IsNullOrWhiteSpace(inputText))
                {
                    HandleInputSubmission(inputText);
                }
            }
        }

        private void HandleVoiceInput(bool isStartingToTalk)
        {
            if (UIUtilities.IsAnyInputFieldFocused() || (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())) return;

            // --- DIGITAL TWIN MODIFICATION: REMOVED NPCManager.activeConvaiNPC CHECK ---
            if (isStartingToTalk)
            {
                UpdateActionConfig();
                _convaiNPC.StartListening();
            }
            else
            {
                // Stop listening safely
                _convaiNPC.StopListening();
            }
        }

        private void HandleToggleChat()
        {
            // --- DIGITAL TWIN MODIFICATION: REMOVED NPCManager.activeConvaiNPC CHECK ---
            TMP_InputField inputFieldInScene = FindActiveInputField();
            UpdateCurrentInputFieldCache(inputFieldInScene);

            if (_currentInputField != null && !_currentInputField.isFocused)
            {
                _currentInputField.ActivateInputField();
                _currentInputField.Select();
            }
        }

        private void HandleInputSubmission(string validInputText)
        {
            UpdateActionConfig();
            _convaiNPC.InterruptCharacterSpeech();
            _convaiNPC.SendTextData(validInputText);
            _convaiChatUIHandler?.SendPlayerText(validInputText);
            ClearInputField();
        }

        public TMP_InputField FindActiveInputField()
        {
            if (_convaiChatUIHandler == null) return null;
            GameObject currentUIRoot = _convaiChatUIHandler.GetCurrentUI()?.GetCanvasGroup()?.gameObject;
            if (currentUIRoot == null || !currentUIRoot.activeInHierarchy) return null;

            return currentUIRoot.GetComponentsInChildren<TMP_InputField>(true)
                              .FirstOrDefault(inputField => inputField.interactable && inputField.gameObject.activeInHierarchy);
        }

        private void UpdateCurrentInputFieldCache(TMP_InputField foundInputField)
        {
            if (_currentInputField != foundInputField) _currentInputField = foundInputField;
        }

        private void ClearInputField()
        {
            if (_currentInputField != null)
            {
                _currentInputField.text = string.Empty;
                _currentInputField.DeactivateInputField();
                if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject == _currentInputField.gameObject)
                    EventSystem.current.SetSelectedGameObject(null);
            }
        }

        private void UpdateActionConfig()
        {
            ActionConfig currentActionConfig = _convaiNPC?.ActionsHandler?.ActionConfig;
            if (currentActionConfig == null || _convaiCrosshairHandler == null || _grpcWebAPI == null) return;
            string attentionObjectName = _convaiCrosshairHandler.FindPlayerReferenceObject();
            currentActionConfig.currentAttentionObject = attentionObjectName;
            _grpcWebAPI.UpdateActionConfig(currentActionConfig);
        }
    }
}