using UnityEngine;

public class Top_FSM_Patrol : StateMachineBehaviour
{
    // OnStateEnter is called when a transition starts and the state machine starts to evaluate this state
    override public void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        GameObject NPC_00 = animator.transform.gameObject;
        // get AIState_Patrol child in NPC_000
        GameObject AIState_Patrol = NPC_00.transform.Find("AIState_Patrol").gameObject;
        // access animator component of AIState_Patrol
        Animator AIState_Patrol_animator = AIState_Patrol.GetComponent<Animator>();
        // rebind AIState_Patrol
        AIState_Patrol_animator.Rebind(); // This restarts the animator from the beginning
    }

    // OnStateUpdate is called on each Update frame between OnStateEnter and OnStateExit callbacks
    override public void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        
    }

    // OnStateExit is called when a transition ends and the state machine finishes evaluating this state
    override public void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        
    }
}
