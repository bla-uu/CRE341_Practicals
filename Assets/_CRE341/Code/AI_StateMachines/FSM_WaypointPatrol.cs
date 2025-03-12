using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.AI;
using System.Collections.Generic;

public class FSM_WaypointPatrol : StateMachineBehaviour
{
    GameObject NPC_00;

    // list of gameObject waypoints
    List<GameObject> waypoints;
    [SerializeField] Transform WaypointTarget;
    
    // OnStateEnter is called when a transition starts and the state machine starts to evaluate this state
    override public void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        // debug statement 
        Debug.Log("Entering Patrol State");

        // get all waypoints with tag Waypoint
        waypoints = new List<GameObject>(GameObject.FindGameObjectsWithTag("Waypoint"));
        
        // Find the NPC
        NPC_00 = GameObject.Find("NPC_00");
        if (NPC_00 == null)
        {
            Debug.LogError("NPC_00 not found! Make sure the NPC is named correctly.");
            return;
        }
        
        // Check if we have any waypoints
        if (waypoints.Count > 0)
        {
            // Set a random waypoint as the target
            WaypointTarget = waypoints[Random.Range(0, waypoints.Count)].transform;
            
            // Set the destination for the NavMeshAgent
            NavMeshAgent agent = NPC_00.GetComponent<NavMeshAgent>();
            if (agent != null)
            {
                agent.SetDestination(WaypointTarget.position);
            }
            else
            {
                Debug.LogError("NavMeshAgent component not found on NPC_00!");
            }
        }
        else
        {
            Debug.LogWarning("No waypoints found with tag 'Waypoint'! The NPC will not move.");
        }
    }

    // OnStateUpdate is called on each Update frame between OnStateEnter and OnStateExit callbacks
    override public void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        // Debug log showing the current state
        Debug.Log("On State Update ~ Patrol State");

        // Check if we have a valid NPC and waypoint target
        if (NPC_00 == null || WaypointTarget == null)
        {
            return;
        }
        
        // Check if we've reached the current waypoint
        if (Vector3.Distance(NPC_00.transform.position, WaypointTarget.position) < 0.1f)
        {
            // Make sure we have waypoints to choose from
            if (waypoints.Count > 0)
            {
                // Set a new random waypoint as the target
                WaypointTarget = waypoints[Random.Range(0, waypoints.Count)].transform;
                
                // Set the new destination
                NavMeshAgent agent = NPC_00.GetComponent<NavMeshAgent>();
                if (agent != null)
                {
                    agent.SetDestination(WaypointTarget.position);
                }
            }
        }
    }

    // OnStateExit is called when a transition ends and the state machine finishes evaluating this state
    override public void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        // debug statement 
        Debug.Log("Exiting Patrol State");
    }
}
