using UnityEngine;
using UnityEngine.AI;

public class Move_NPC : MonoBehaviour
{
    GameObject player;
    NavMeshAgent agent;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        player = GameObject.Find("Player");
        agent = GetComponent<NavMeshAgent>();
    }

    // Update is called once per frame
    void Update()
    {
        // Move the NPC to the player
        agent.SetDestination(player.transform.position);

    }
}
