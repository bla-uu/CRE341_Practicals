using UnityEngine;

public class Top_FSM_Chase : StateMachineBehaviour
{
    override public void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        GameObject NPC_00 = animator.transform.gameObject;
        // get AIState_Patrol child in NPC_000
        GameObject AIState_Chase = NPC_00.transform.Find("AIState_Chase").gameObject;
        // access animator component of AIState_Patrol
        Animator AIState_Chase_animator = AIState_Chase.GetComponent<Animator>();
        // rebind AIState_Patrol
        AIState_Chase_animator.Rebind(); // This restarts the animator from the beginning
    }

    override public void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
    }

    override public void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
    }

}
