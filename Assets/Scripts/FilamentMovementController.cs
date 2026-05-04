using System.Collections;
using UnityEngine;
using UnityEngine.AI;

// sequence runner for the little NPC dude. moves him around using navmesh.
public class FilamentMovementController : MonoBehaviour
{
    [Header("References")]
    public Animator npcAnimator;
    public NavMeshAgent navMeshAgent;
    public Transform npcTransform;
    public Transform npcHandTransform; 

    private FilamentManager _manager;

    void Start()
    {
        _manager = FindObjectOfType<FilamentManager>();
    }

    public void ExecuteFilamentMove(GameObject filament, Transform fromSpot, Transform toSpot)
    {
        StartCoroutine(FilamentMoveSequence(filament, fromSpot, toSpot));
    }

    private IEnumerator FilamentMoveSequence(GameObject filament, Transform fromSpot, Transform toSpot)
    {
        yield return StartCoroutine(MoveToRoutine(fromSpot.gameObject));
        yield return StartCoroutine(SnapToHandRoutine(filament));
        yield return StartCoroutine(MoveToRoutine(toSpot.gameObject));
        yield return StartCoroutine(DropRoutine(filament, toSpot));

        if (_manager != null) _manager.isNpcBusy = false;
    }

    private IEnumerator MoveToRoutine(GameObject target)
    {
        npcAnimator.CrossFade(Animator.StringToHash("Walking"), 0.01f);
        npcAnimator.applyRootMotion = false;
        navMeshAgent.updateRotation = false;

        // note the 0.4f offset on the destination - tweak this if he clips into the tables or stops too far away.
        Vector3 targetDestination = target.transform.position;
        targetDestination += 0.4f * target.transform.forward;

        navMeshAgent.SetDestination(targetDestination);
        yield return null;

        while (navMeshAgent.remainingDistance > navMeshAgent.stoppingDistance)
        {
            if (navMeshAgent.velocity.sqrMagnitude > 0.1f)
            {
                // manually steer his rotation while moving so he doesn't look completely robotic snapping to paths
                Quaternion rotation = Quaternion.LookRotation(navMeshAgent.velocity.normalized);
                npcTransform.rotation = Quaternion.Slerp(npcTransform.rotation, rotation, 5 * Time.deltaTime);
            }
            yield return null;
        }

        npcAnimator.CrossFade(Animator.StringToHash("Idle"), 0.1f);
        npcAnimator.applyRootMotion = true;
    }

    private IEnumerator SnapToHandRoutine(GameObject target)
    {
        Vector3 direction = (target.transform.position - npcTransform.position).normalized;
        direction.y = 0;
        npcTransform.rotation = Quaternion.LookRotation(direction);

        // tiny pause so it doesn't look like a glitchy frame-skip
        yield return new WaitForSeconds(0.2f);

        if (npcHandTransform != null)
        {
            // fake picking it up by just parenting it to the hand bone. no fancy IK here.
            target.transform.SetParent(npcHandTransform);
            target.transform.localPosition = Vector3.zero; 
            target.transform.localRotation = Quaternion.identity; 
        }
        
        target.transform.localScale = Vector3.one;
        yield return new WaitForSeconds(0.2f);
    }

    private IEnumerator DropRoutine(GameObject target, Transform destination)
    {
        Vector3 direction = (destination.position - npcTransform.position).normalized;
        direction.y = 0;
        npcTransform.rotation = Quaternion.LookRotation(direction);

        yield return new WaitForSeconds(0.2f);

        target.transform.SetParent(null);
        target.transform.position = destination.position;
        // reset rotation so it stands upright on the table
        target.transform.rotation = Quaternion.identity; 
        target.transform.localScale = Vector3.one;

        yield return new WaitForSeconds(0.2f);
    }
}