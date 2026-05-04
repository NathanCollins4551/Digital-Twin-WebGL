using System.Collections;
using Convai.Scripts.Runtime.Features.Actions;
using UnityEngine;

namespace Convai.Scripts.Runtime.Features.CustomActions 
{
    public interface ICustomAction
    {
        string ActionName { get; }
        void Initialize(ConvaiActionsHandler handler); 
        IEnumerator Execute(GameObject target); 
    }
}