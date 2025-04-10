using UnityEngine;
using UnityEngine.AI;

public class FSM_ChasePathfind : StateMachineBehaviour
{
    GameObject Player, NPC_00;
    // OnStateEnter is called when a transition starts and the state machine starts to evaluate this state
    override public void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        Player = GameObject.Find("Player");
        NPC_00 = animator.transform.parent.gameObject;
        // NPC_00.GetComponent<NavMeshAgent>().enabled = true;
        NPC_00.GetComponent<NavMeshAgent>().SetDestination(Player.transform.position);
    }

    // OnStateUpdate is called on each Update frame between OnStateEnter and OnStateExit callbacks
    override public void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        NPC_00.GetComponent<NavMeshAgent>().SetDestination(Player.transform.position);
    }

    // OnStateExit is called when a transition ends and the state machine finishes evaluating this state
    override public void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        // stop navmesh agent
        //NPC_00.GetComponent<NavMeshAgent>().isStopped = true;
        //NPC_00.GetComponent<NavMeshAgent>().ResetPath();
        //NPC_00.GetComponent<NavMeshAgent>().enabled = false;
    }

}
