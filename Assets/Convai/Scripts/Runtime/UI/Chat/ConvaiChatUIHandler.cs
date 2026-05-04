using System;
using System.Collections.Generic;
using System.Linq;
using Convai.Scripts.Runtime.Attributes;
using Convai.Scripts.Runtime.Core;
using UnityEngine;

namespace Convai.Scripts.Runtime.UI
{
    [Serializable]
    public class Character
    {
        [Header("Character settings")] [Tooltip("Convai NPC Game Object")]
        public ConvaiNPC characterGameObject;

        [ReadOnly] [Tooltip("Display name of the NPC")]
        public string characterName = "Character";

        [ColorUsage(true)] [Tooltip("Color of the NPC text. Alpha value will be ignored.")] [SerializeField]
        private Color characterTextColor = Color.red;

        public Color CharacterTextColor
        {
            get => characterTextColor;
            set => characterTextColor = value;
        }
    }

    [AddComponentMenu("Convai/Chat UI Handler")]
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-100)]
    public class ConvaiChatUIHandler : MonoBehaviour
    {
        public enum UIType
        {
            ChatBox,
            QuestionAnswer,
            Subtitle
        }

        [Header("UI Prefabs")] [Tooltip("Prefab for the chat box UI.")]
        public GameObject chatBoxPrefab;

        [Tooltip("Prefab for the subtitle UI.")]
        public GameObject subtitlePrefab;

        [Tooltip("Prefab for the question-answer UI.")]
        public GameObject questionAnswerPrefab;

        [Header("Character List")] [Tooltip("List of characters.")]
        public List<Character> characters = new();

        [Header("Player settings")] [Tooltip("Display name of the player.")]
        public string playerName = "Player";

        [ColorUsage(true)] [Tooltip("Color of the player's text. Alpha value will be ignored.")]
        public Color playerTextColor = Color.white;

        private IChatUI _currentUIImplementation;
        public static ConvaiChatUIHandler Instance { get; private set; }

        public Dictionary<UIType, IChatUI> GetUIAppearances { get; } = new();

        private void Awake()
        {
            if (Instance != null)
            {
                Debug.Log("<color=red> There's More Than One ConvaiChatUIHandler </color> " + transform + " - " + Instance);
                Destroy(gameObject);
                return;
            }

            Instance = this;

            ValidateUIPrefabs();
            InitializeUIStrategies();
        }

        private void OnEnable()
        {
            UISaveLoadSystem.Instance.OnLoad += UISaveLoadSystem_OnLoad;
            UISaveLoadSystem.Instance.OnSave += UISaveLoadSystem_OnSave;
        }

        private void OnDisable()
        {
            UISaveLoadSystem.Instance.OnLoad -= UISaveLoadSystem_OnLoad;
            UISaveLoadSystem.Instance.OnSave -= UISaveLoadSystem_OnSave;
        }

        private void OnDestroy()
        {
            SaveUIType();
        }

        private void OnValidate()
        {
            try { UpdateCharacterList(); }
            catch { RemoveDuplicateCharacters(); }
        }

        public void UpdateCharacterList()
        {
            for (int i = 0; i < characters.Count; i++)
            {
                Character character = characters[i];
                if (character.characterGameObject == null)
                    characters.Remove(character);
                else
                    character.characterName = character.characterGameObject.characterName;
            }

            characters = characters.Where(c => c.characterGameObject != null).ToList();

            foreach (ConvaiNPC convaiNpc in FindObjectsOfType<ConvaiNPC>())
            {
                if (characters.Any(c => c.characterGameObject == convaiNpc))
                    continue;

                characters.Add(new Character
                {
                    characterGameObject = convaiNpc,
                    characterName = convaiNpc.characterName
                });
            }
        }

        private void RemoveDuplicateCharacters()
        {
            characters = characters.GroupBy(c => c.characterGameObject).Select(g => g.First()).ToList();
        }

        private void UISaveLoadSystem_OnLoad()
        {
            _currentUIImplementation = GetChatUIByUIType(UISaveLoadSystem.Instance.UIType);
            SetUIType(UISaveLoadSystem.Instance.UIType);
            _currentUIImplementation.ActivateUI();
        }

        private void UISaveLoadSystem_OnSave()
        {
            SaveUIType();
        }

        private void InitializeUIStrategies()
        {
            InitializeUI(chatBoxPrefab, UIType.ChatBox);
            InitializeUI(questionAnswerPrefab, UIType.QuestionAnswer);
            InitializeUI(subtitlePrefab, UIType.Subtitle);

            // --- DIGITAL TWIN MODIFICATION: FORCE UI VISIBILITY ---
            SetUIType(UIType.ChatBox);
            GetCurrentUI()?.ActivateUI();
        }

        private void InitializeUI(GameObject uiPrefab, UIType uiType)
        {
            try
            {
                IChatUI uiComponent = uiPrefab.GetComponent<IChatUI>();
                if (uiComponent == null) return;

                uiComponent.Initialize(uiPrefab);
                GetUIAppearances[uiType] = uiComponent;
            }
            catch (Exception ex)
            {
                Debug.LogError($"An error occurred while initializing the UI: {ex.Message}");
            }
        }

        public void SendCharacterText(string charName, string text)
        {
            Character character = characters.Find(c => c.characterName == charName);
            if (character == null) return;
            _currentUIImplementation?.SendCharacterText(charName, text, character.CharacterTextColor);
        }

        public void SendPlayerText(string text)
        {
            _currentUIImplementation?.SendPlayerText(playerName, text, playerTextColor);
        }

        private void ValidateUIPrefabs()
        {
            if (chatBoxPrefab == null || subtitlePrefab == null || questionAnswerPrefab == null)
                Debug.LogError("All UI prefabs must be assigned in the inspector.");
        }

        public void SetUIType(UIType newUIType)
        {
            if (!GetUIAppearances.ContainsKey(newUIType)) return;
            _currentUIImplementation = GetUIAppearances[newUIType];
        }

        private void SaveUIType()
        {
            foreach (KeyValuePair<UIType, IChatUI> strategy in GetUIAppearances.Where(strategy => strategy.Value == _currentUIImplementation))
            {
                UISaveLoadSystem.Instance.UIType = strategy.Key;
                break;
            }
        }

        public IChatUI GetChatUIByUIType(UIType uiType) => GetUIAppearances[uiType];
        public IChatUI GetCurrentUI() => _currentUIImplementation;
        public bool HasCharacter(string convaiNPCCharacterName) => characters.Any(character => character.characterName == convaiNPCCharacterName);
        public void AddCharacter(Character newCharacter) => characters.Add(newCharacter);
    }
}