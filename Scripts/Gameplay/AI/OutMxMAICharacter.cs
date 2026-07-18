// Copyright © 2017-2024 Vault Break Studios Pty Ltd

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using OutGame; // Required to access INonPlayableCharacter

namespace MxMGameplay
{
    public class OutMxMAICharacter : MonoBehaviour, INonPlayableCharacter
    {
        [SerializeField] private Transform m_destinationTransform = null;
        [SerializeField] private float m_timeToChangeTarget = 5f;
        [SerializeField] private float m_patrolRadius = 30f;
        [SerializeField] private float m_destinationTolerance = 0.5f; // Added tolerance for reaching points

        private Vector3 m_lastDestination = Vector3.zero;
        private NavMeshAgent m_navAgent;
        private float m_moveTimer = 0f;

        #region INonPlayableCharacter Implementation
        public List<Transform> Destinations { get; set; } = new List<Transform>();
        public bool followDestinations { get; set; } = false;

        private int m_currentDestinationIndex = 0;

        public void FollowDestinations(List<Transform> destinations)
        {
            if (destinations == null || destinations.Count == 0)
            {
                followDestinations = false;
                Destinations?.Clear();
                return;
            }

            Destinations = destinations;
            followDestinations = true;
            m_currentDestinationIndex = 0;
            m_moveTimer = 0f;

            if (m_navAgent)
            {
                m_navAgent.SetDestination(Destinations[m_currentDestinationIndex].position);
            }
        }
        #endregion

        private void Start()
        {
            m_navAgent = GetComponent<NavMeshAgent>();

            // Only set initial destination if we aren't already following a list injected by a puzzle zone/spawner
            if (m_navAgent && m_destinationTransform && !followDestinations)
            {
                m_navAgent.SetDestination(m_destinationTransform.localPosition);
            }
        }

        public void Update()
        {
            // NEW LOGIC: Explicit destination looping
            if (followDestinations && Destinations != null && Destinations.Count > 0)
            {
                if (m_navAgent && !m_navAgent.pathPending && m_navAgent.remainingDistance <= m_destinationTolerance)
                {
                    m_moveTimer += Time.deltaTime;

                    if (m_moveTimer > m_timeToChangeTarget)
                    {
                        // Loop to the next destination in the list
                        m_currentDestinationIndex = (m_currentDestinationIndex + 1) % Destinations.Count;
                        m_navAgent.SetDestination(Destinations[m_currentDestinationIndex].position);
                        m_moveTimer = 0f;
                    }
                }
            }

            // ORIGINAL LOGIC: Random NavMesh roaming
            else
            {
                m_moveTimer += Time.deltaTime;

                if (m_navAgent && m_destinationTransform)
                {
                    if (m_moveTimer > m_timeToChangeTarget)
                    {
                        Vector3 destination = RandomNavmeshLocation(m_patrolRadius);
                        m_destinationTransform.position = destination;
                        m_navAgent.SetDestination(destination);

                        m_moveTimer = 0f;
                    }
                }
            }
        }

        public Vector3 RandomNavmeshLocation(float radius)
        {
            Vector3 randomDirection = Random.insideUnitSphere * radius;
            NavMeshHit hit;
            Vector3 finalPosition = Vector3.zero;
            if (NavMesh.SamplePosition(randomDirection, out hit, radius, 1))
            {
                finalPosition = hit.position;
            }
            return finalPosition;
        }
    }
}