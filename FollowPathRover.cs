using System.Collections;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;

public class FollowPath : Agent
{
    [Header("Infinite Track Settings")]
    public GameObject[] dynamicStages;
    public int visibleTrackLength = 5;

    [Header("Speed Settings")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float turnSpeed = 40f;

    [Header("Camera Settings")]
    public Transform agentCamera;

    [Header("Movement Settings")]
    private Rigidbody rb;
    public Collider bodyCollider;

    [Header("Environment References")]
    public Material offRopeMaterial;
    public Material goalMetMaterial;
    public Renderer flagRenderer;

    [Header("Target Switching")]
    public float minSwitchDistance = 0.5f;
    
    private Transform _currentTarget;
    private Transform _pendingTarget;

    private Queue<GameObject> spawnedSegments = new Queue<GameObject>();
    private List<Transform> segmentGoals = new List<Transform>();
    private Vector3 previousPosition;
    private int segmentsTotalTouched;

    private string rope;
    private string prevTouch;
    private GameObject firstSegment;
    private Transform target;
    private Transform rope1;
    private Transform rope2;
    private Transform seamStart1;
    private Transform seamStart2;
    private GameObject currentSegment;
    private Transform activeRope1;
    private Transform activeRope2;

    public override void Initialize()
    {
        rb = GetComponent<Rigidbody>();
    }

    public override void OnEpisodeBegin()
    {
        ResetWorld();
        SpawnInitialTrack();
        CheckSeamPass();
    }

    //Rope prefabs are called Dynamic Stages 1-13, they represent ropes with a 2 parts equal length. One always going forward and the second rotating to any 
    //angles left or right incrementing in 20 degree stages. They are randomly generated for complete generalisation incentivised learning.
    private void ResetWorld()
    {
        SetReward(0f);
        segmentsTotalTouched=0;
        GameObject[] allObjects = Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
        foreach (GameObject obj in allObjects)
        {
            if (obj.name.Contains("Dynamic"))
            {
                Destroy(obj);
            }
        }

        spawnedSegments.Clear();

        if (rb == null) rb = GetComponent<Rigidbody>();
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        transform.position = new Vector3(0f, 0.5f, 0f);
        //POST TRAINING CAMERA TILT GENERALISATION.
        transform.rotation = Quaternion.Euler(Random.Range(-10f, 15f), 0f, 0f);

        segmentGoals.Clear();
    }

    private void SpawnInitialTrack()
    {
        if (dynamicStages.Length == 0)
        {
            Debug.LogError("No dynamic stages assigned.");
            return;
        }

        Vector3 spawnPosition = new Vector3(transform.position.x, 0f, transform.position.z);
        GameObject segment = Instantiate(dynamicStages[0], spawnPosition, Quaternion.identity);
        spawnedSegments.Enqueue(segment);
        for (int i = 2; i < visibleTrackLength; i++)
        {
            SpawnNextSegment();
        }
    }
    private void SpawnNextSegment()
    {
        if (spawnedSegments.Count >= visibleTrackLength)
        {
            GameObject oldest = spawnedSegments.Dequeue();
            if (oldest != null) Destroy(oldest);
        }

        GameObject lastSegment = null;
        foreach (var segment in spawnedSegments)
        {
            lastSegment = segment;
        }

        if (lastSegment == null)
        {
            Debug.LogError("No segments in queue to base next spawn on.");
            return;
        }

        Transform goal = FindDeepChild(lastSegment.transform, "Goal");
        if (goal == null)
        {
            Debug.LogError("Last segment has no 'Goal' transform.");
            return;
        }

        //ADJUSTED FOR POST TRAINING LATER STAGE HARD TURN GENERALISATION
        int[] weights = { 30, 25, 25, 25, 25, 15, 15, 10, 10, 5, 5, 1, 1 };
        int totalWeight = 0;
        foreach (int weight in weights) totalWeight += weight;
        int rand = Random.Range(0, totalWeight);
        int chosenIndex = 0;
        for (int i = 0; i < weights.Length; i++)
        {
            if (rand < weights[i])
            {
                chosenIndex = i;
                break;
            }
            rand -= weights[i];
        }
        goal.rotation*= Quaternion.Euler(0f, 90f, 0f);
        GameObject newSegment = Instantiate(dynamicStages[chosenIndex], goal.position, goal.rotation);
        spawnedSegments.Enqueue(newSegment);
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        int action = actions.DiscreteActions[0];
        switch (action)
        {
            case 0:
                rb.MoveRotation(rb.rotation * Quaternion.Euler(0f, -turnSpeed * Time.fixedDeltaTime, 0f));
                break;
            case 1:
                rb.MoveRotation(rb.rotation * Quaternion.Euler(0f, turnSpeed * Time.fixedDeltaTime, 0f));
                break;
            case 2:
                rb.MovePosition(rb.position + transform.forward * (moveSpeed * Time.fixedDeltaTime));
                break;
            case 3:
                break;
        }

        EvaluateMovementAndPosition();
    }

    private void EvaluateMovementAndPosition()
    {
        CheckSeamPass();
        CheckRopeContact();
        CheckGoalContact();
    }

    private void CheckSeamPass()
    {
        if (spawnedSegments.Count < 1) return;

        Collider[] hits = Physics.OverlapBox(
            transform.position,
            Vector3.one * 0.5f,
            transform.rotation
        );

        activeRope1 = null;
        activeRope2 = null;
        GameObject rope1Segment = null;
        GameObject rope2Segment = null;

        foreach (Collider hit in hits)
        {
            if (hit.transform.name == "rope1")
            {
                activeRope1 = hit.transform;
                rope1Segment = activeRope1.parent.gameObject;
            }
            else if (hit.transform.name == "rope2")
            {
                activeRope2 = hit.transform;
                rope2Segment = activeRope2.parent.gameObject;
            }
        }

        if (activeRope1 != null && activeRope2 == null)
        {
            currentSegment = rope1Segment;
            target = activeRope1.Find("SeamStart");
        }
        else if (activeRope2 != null && activeRope1 == null)
        {
            currentSegment = rope2Segment;
            target = activeRope2.Find("SeamStart");
        }
        else if (activeRope1 != null && activeRope2 != null && rope1Segment == rope2Segment)
        {
            currentSegment = rope1Segment;
            target = activeRope2.Find("SeamStart");
        }
        else if (activeRope1 != null && activeRope2 != null && rope1Segment != rope2Segment)
        {
            currentSegment = rope1Segment;
            target = activeRope1.Find("SeamStart");
        }

        if (target != null)
        {
            Debug.Log($"Tracking {target.name} on segment {currentSegment.name} " +
                    $"(Active ropes: {(activeRope1 != null ? "1" : "")}{(activeRope2 != null ? "2" : "")})");
        }
    }

    private void CheckGoalContact()
    {
        if (spawnedSegments.Count < 1) return;

        GameObject[] segmentArray = spawnedSegments.ToArray();
        GameObject secondSegment = segmentArray[1];

        Transform goal = FindDeepChild(secondSegment.transform, "Goal");
        if (goal == null) return;

        Collider[] hits = Physics.OverlapBox(
            transform.position,
            Vector3.one * 0.5f,
            transform.rotation
        );

        foreach (Collider hit in hits)
        {
            if (hit.gameObject == goal.gameObject)
            {
                segmentsTotalTouched+=1;
                AddReward(1f);
                SpawnNextSegment();
                break;
            }
        }
    }

    //Uneccessary efficiency reward signal. Segments high scoring over max step already incentivises efficiency :'D  My poor brain.
    private int CalculateRopeAlignment()
    {
        Vector3 boundsCenter = bodyCollider.bounds.center;
        Vector3 boundsSize = bodyCollider.bounds.size;
        float checkWidth = boundsSize.x * 0.25f;

        int score = 0;
        score += Physics.OverlapBox(boundsCenter - transform.right * checkWidth, new Vector3(checkWidth, boundsSize.y, boundsSize.z) * 0.5f, transform.rotation, LayerMask.GetMask("Rope")).Length > 0 ? 1 : 0;
        score += Physics.OverlapBox(boundsCenter, new Vector3(checkWidth, boundsSize.y, boundsSize.z) * 0.5f, transform.rotation, LayerMask.GetMask("Rope")).Length > 0 ? 1 : 0;
        score += Physics.OverlapBox(boundsCenter + transform.right * checkWidth, new Vector3(checkWidth, boundsSize.y, boundsSize.z) * 0.5f, transform.rotation, LayerMask.GetMask("Rope")).Length > 0 ? 1 : 0;

        return score;
    }

    //Checks if rover dumb
    private void CheckRopeContact()
    {
        Collider[] hits = Physics.OverlapBox(
            bodyCollider.bounds.center,
            bodyCollider.bounds.extents,
            transform.rotation
        );

        bool onRope = false;

        foreach (var hit in hits)
        {
            if (hit.CompareTag("rope"))
            {
                onRope = true;
                break;
            }
        }

        if (!onRope)
        {
            FailEpisode();
        }
    }

    private void FailEpisode()
    {
        flagRenderer.material = offRopeMaterial;
        AddReward(-1f);
        EndEpisode();
    }

    //Helper function for finding correct objects within objects.
    private Transform FindDeepChild(Transform parent, string targetName)
    {
        foreach (Transform child in parent)
        {
            if (child.name == targetName) return child;
            Transform result = FindDeepChild(child, targetName);
            if (result != null) return result;
        }
        return null;
    }

    //Perfect Demo Rover. Follows Path adjusts using pre placed seams at the end of every rope to move towards VIA discreteVariables 0, 1, 2
    //Adjusts if rover is facing too far away from the seam <e.g. 15 degrees> and prioritises rotating before moving forward.
    //Can go infinitley but had to reasonably record 66MB of data as my laptop couldnt handle loading more into RAM at the beginning of training.
    public override void Heuristic(in ActionBuffers actionsOut)
    {
        ActionSegment<int> discreteActions = actionsOut.DiscreteActions;
        
        Transform newTarget = target;
        
        if (newTarget != _pendingTarget)
        {
            _pendingTarget = newTarget;
        }
        
        if (_currentTarget == null || 
            Vector3.Distance(transform.position, _currentTarget.position) <= minSwitchDistance)
        {
            _currentTarget = _pendingTarget;
        }
        
        Vector3 targetPos = _currentTarget != null ? _currentTarget.position : 
                          transform.position + transform.forward * 10f;
        
        Vector3 agentPosFlat = new Vector3(transform.position.x, 0f, transform.position.z);
        Vector3 targetPosFlat = new Vector3(targetPos.x, 0f, targetPos.z);
        Vector3 directionToTarget = (targetPosFlat - agentPosFlat).normalized;
        
        Vector3 agentForwardFlat = transform.forward;
        agentForwardFlat.y = 0;
        agentForwardFlat.Normalize();

        float angleToTarget = Vector3.Angle(agentForwardFlat, directionToTarget);
        if (angleToTarget <= 5f)
        {
            discreteActions[0] = 2;
        }
        else
        {
            Vector3 cross = Vector3.Cross(agentForwardFlat, directionToTarget);
            discreteActions[0] = cross.y > 0 ? 1 : 0;
        }
    }
}