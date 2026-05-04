using UnityEngine;
using UnityEngine.AI;

public class EnableNavAgentDelayed : MonoBehaviour {
    void Start() {
        Invoke("EnableNow", 1.0f);
    }
    void EnableNow() {
        GetComponent<NavMeshAgent>().enabled = true;
    }
}
