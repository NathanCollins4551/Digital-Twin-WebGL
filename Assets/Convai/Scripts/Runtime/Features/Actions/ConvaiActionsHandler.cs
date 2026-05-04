using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Convai.Scripts.Runtime.Core;
using Convai.Scripts.Runtime.LoggerSystem;
using Convai.Scripts.Runtime.UI;
using Convai.Scripts.Runtime.Features.Actions.CustomActions;
using Convai.Scripts.Runtime.Features.CustomActions;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Serialization;

namespace Convai.Scripts.Runtime.Features.Actions
{
    public enum ActionChoice
    {
        None,
        Jump,
        Crouch,
        MoveTo,
        PickUp,
        Drop
    }

    [DisallowMultipleComponent]
    [AddComponentMenu("Convai/Convai Actions Handler")]
    public class ConvaiActionsHandler : MonoBehaviour
    {
        [SerializeField] public ActionMethod[] actionMethods;
        public List<string> actionResponseList = new();
        private readonly List<ConvaiAction> _actionList = new();
        public readonly ActionConfig ActionConfig = new();
        private List<string> _actions = new();
        private ConvaiNPC _currentNPC;
        private ConvaiInteractablesData _interactablesData;

        private readonly Dictionary<string, ICustomAction> _customActions = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<string> _registeredActionNames = new();

        public event Action<string, GameObject> ActionStarted;
        public event Action<string, GameObject> ActionEnded;

        private void Awake()
        {
            _interactablesData = FindFirstObjectByType<ConvaiInteractablesData>();
            if (TryGetComponent(out ConvaiNPC npc))
                _currentNPC = npc;

            // Initialize modular classes internally
            RegisterCustomActions();
        }

        private IEnumerator Start()
        {
            // FIX: Wait for Convai SDK internal singleton and Active NPC setup
            // This prevents the "UpdateActionConfig called, but no active NPC" warning
            yield return new WaitForSeconds(0.75f);

            SetupActionConfig();
            SyncWithCloud();

            // Start the execution loop
            StartCoroutine(PlayActionList());
        }

        private void SetupActionConfig()
        {
            ActionConfig.actions.Clear();
            ActionConfig.objects.Clear();
            ActionConfig.characters.Clear();

            // Register modular actions (Force Lowercase for gRPC compatibility)
            foreach (var name in _registeredActionNames)
            {
                string lowerName = name.ToLower();
                if (!ActionConfig.actions.Contains(lowerName))
                    ActionConfig.actions.Add(lowerName);
            }

            // Register Inspector actions
            foreach (ActionMethod actionMethod in actionMethods)
            {
                if (string.IsNullOrEmpty(actionMethod.action)) continue;
                string lowerName = actionMethod.action.ToLower();
                if (!ActionConfig.actions.Contains(lowerName))
                    ActionConfig.actions.Add(lowerName);
            }

            // Map Printers/Objects from Interactables Data
            if (_interactablesData != null)
            {
                foreach (var obj in _interactablesData.Objects)
                {
                    ActionConfig.objects.Add(new ActionConfig.Types.Object { 
                        name = obj.Name, 
                        description = obj.Description 
                    });
                }
                foreach (var character in _interactablesData.Characters)
                {
                    ActionConfig.characters.Add(new ActionConfig.Types.Character { 
                        name = character.Name, 
                        bio = character.Bio 
                    });
                }
            }
            ActionConfig.classification = "multistep";
        }

        private void SyncWithCloud()
        {
            if (_currentNPC != null && ConvaiGRPCWebAPI.Instance != null)
            {
                // Push the vocabulary to the global API manager
                ConvaiGRPCWebAPI.Instance.UpdateActionConfig(ActionConfig);
                
                string actionList = string.Join(", ", ActionConfig.actions);
                Debug.Log($"<color=cyan>[Convai Sync]</color> Actions synced to {_currentNPC.characterName}: {actionList}");
            }
            else
            {
                Debug.LogError("<color=red>[Convai Error]</color> Sync failed: NPC or API Instance missing.");
            }
        }

        private void RegisterCustomActions()
        {
            ICustomAction[] actions = new ICustomAction[]
            {
                new DisplayPrinterDashboardAction(),
                new ShowMeAction(),
                new HighlightAction(),
                new MoveToAction(),    
                new PickUpAction(),    
                new DropAction()
            };

            foreach (var action in actions)
            {
                action.Initialize(this); 
                _customActions[action.ActionName] = action;
                if(!_registeredActionNames.Contains(action.ActionName))
                    _registeredActionNames.Add(action.ActionName);
            }
        }

        public void SignalActionStarted(string actionName, GameObject target) => ActionStarted?.Invoke(actionName, target);
        public void SignalActionEnded(string actionName, GameObject target) => ActionEnded?.Invoke(actionName, target);

        private void Update()
        {
            if (actionResponseList.Count > 0)
            {
                ParseActions(actionResponseList[0]);
                actionResponseList.RemoveAt(0);
            }
        }

        private void ParseActions(string actionsString)
        {
            _actions = actionsString.Trim().Split(", ").ToList();
            _actionList.Clear();

            foreach (string action in _actions)
            {
                ParseSingleAction(action.Split(' ').ToList());
            }
        }

        private void ParseSingleAction(List<string> actionWords)
        {
            string fullSentence = string.Join(" ", actionWords).ToLower();

            // Loop through our registered verbs (highlight, show me, move to)
            foreach (string registeredVerb in _registeredActionNames)
            {
                string lowerVerb = registeredVerb.ToLower();
        
                // If the AI's sentence starts with our verb (or is very close)
                if (fullSentence.StartsWith(lowerVerb) || LevenshteinDistance(fullSentence.Split(' ')[0], lowerVerb.Split(' ')[0]) <= 1)
                {
                    // Calculate how many words the verb took up
                    int verbWordCount = lowerVerb.Split(' ').Length;
                    string[] objectPart = actionWords.Skip(verbWordCount).ToArray();
            
                    GameObject targetObject = FindTargetObject(objectPart);

                    if (targetObject != null)
                    {
                        Debug.Log($"<color=green>[Match Found]</color> Verb: {registeredVerb} | Target: {targetObject.name}");
                        _actionList.Add(new ConvaiAction(ActionChoice.None, targetObject, registeredVerb));
                    }
                    else
                    {
                        Debug.LogWarning($"<color=yellow>[Match Fail]</color> Found verb '{registeredVerb}' but couldn't identify target in: {string.Join(" ", objectPart)}");
                    }
                    return; // Exit after finding the first valid action match
                }
            }
        }

        private IEnumerator PlayActionList()
        {
            while (true)
            {
                if (_actionList.Count > 0)
                {
                    yield return DoAction(_actionList[0]);
                    _actionList.RemoveAt(0);
                }
                yield return null;
            }
        }

        private IEnumerator DoAction(ConvaiAction action)
        {
            if (!string.IsNullOrEmpty(action.Animation) && _customActions.TryGetValue(action.Animation, out ICustomAction customAction))
            {
                yield return customAction.Execute(action.Target);
                yield break;
            }

            switch (action.Verb)
            {
                case ActionChoice.Jump: Jump(); break;
                case ActionChoice.Drop: Drop(action.Target); break;
                case ActionChoice.MoveTo: yield return MoveTo(action.Target); break;
                // Add PickUp or others if they aren't modular yet
            }
            yield return null;
        }

        private GameObject FindTargetObject(string[] objectPart)
        {
            string input = string.Join(" ", objectPart).ToLower();
            if (string.IsNullOrEmpty(input)) return null;

            // 1. Pre-process the input to handle "A 1" vs "A1"
            input = input.Replace("the ", "").Replace(" printer", "").Trim();
            // Remove the space between a letter and a number (e.g., "a 1" -> "a1")
            input = System.Text.RegularExpressions.Regex.Replace(input, @"([a-zA-Z])\s+(\d)", "$1$2");

            // 2. Exact/Levenshtein Match
            var bestMatch = _interactablesData.Objects
                .Select(o => {
                    // Clean the registered name for comparison (remove underscores)
                    string cleanRegistered = o.Name.ToLower().Replace("_", " ");
                    return new { Obj = o, Score = LevenshteinDistance(cleanRegistered, input) };
                })
                .OrderBy(x => x.Score)
                .FirstOrDefault();

            if (bestMatch != null && bestMatch.Score <= 3) 
            {
                return bestMatch.Obj.gameObject;
            }

            // 3. Last Resort: Character-by-character "Contains"
            foreach (var obj in _interactablesData.Objects)
            {
                string cleanName = obj.Name.ToLower().Replace("_", "");
                string cleanInput = input.Replace(" ", "");

                if (cleanInput.Contains(cleanName) || cleanName.Contains(cleanInput))
                    return obj.gameObject;
            }

            return null;
        }

        private int LevenshteinDistance(string s, string t)
        {
            int[][] d = new int[s.Length + 1][];
            for (int i = 0; i <= s.Length; i++) { d[i] = new int[t.Length + 1]; d[i][0] = i; }
            for (int j = 0; j <= t.Length; j++) d[0][j] = j;
            for (int j = 1; j <= t.Length; j++)
                for (int i = 1; i <= s.Length; i++)
                    d[i][j] = Math.Min(Math.Min(d[i - 1][j] + 1, d[i][j - 1] + 1), d[i - 1][j - 1] + (s[i - 1] == t[j - 1] ? 0 : 1));
            return d[s.Length][t.Length];
        }

        private void Jump() 
        {
            SignalActionStarted("Jump", _currentNPC.gameObject);
            if(TryGetComponent(out Rigidbody rb)) rb.AddForce(Vector3.up * 5f, ForceMode.Impulse);
            SignalActionEnded("Jump", _currentNPC.gameObject);
        }

        private void Drop(GameObject t) { SignalActionStarted("Drop", t); if(t!=null){ t.transform.parent=null; t.SetActive(true); } SignalActionEnded("Drop", t); }
        public IEnumerator MoveTo(GameObject t) { yield return null; } 

        [Serializable]
        public class ActionMethod 
        { 
            public string action; 
            public string animationName; 
            public ActionChoice actionChoice; 
        }

        private class ConvaiAction 
        { 
            public ConvaiAction(ActionChoice v, GameObject t, string a) 
            { 
                Verb = v; 
                Target = t; 
                Animation = a; 
            }
            public readonly string Animation; 
            public readonly GameObject Target; 
            public readonly ActionChoice Verb;
        }
    }
}