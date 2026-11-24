using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Unity.FPS.Gameplay
{


    public class LadderZone : MonoBehaviour
    {
        // Ladders that managed to connect the ground navmesh with the navmesh on top of them
        public static readonly List<LadderZone> NavLinkedLadders = new List<LadderZone>();

        [Header("NavMesh Link")]
        [Tooltip("Create a navmesh link at runtime so bots can path through this ladder")]
        public bool CreateNavMeshLink = true;
        [Tooltip("Max distance from the ladder foot to look for the ground navmesh")]
        public float BottomSearchRadius = 1.5f;
        [Tooltip("Max distance from the ladder top to look for the upper navmesh (roof)")]
        public float TopSearchRadius = 2f;
        [Tooltip("Minimum height difference between both ends for the link to be valid")]
        public float MinClimbHeight = 1.5f;

        public Vector3 BottomPoint { get; private set; }
        public Vector3 TopPoint { get; private set; }

        // Horizontal center of the climbable trigger, at the given height
        public Vector3 GetClimbPoint(float height)
        {
            Vector3 center = m_Trigger != null ? m_Trigger.bounds.center : transform.position;
            return new Vector3(center.x, height, center.z);
        }

        private Collider m_Trigger;
        private NavMeshLinkInstance m_LinkInstance;

        private void Start()
        {
            m_Trigger = GetComponent<Collider>();

            if (CreateNavMeshLink)
                TryCreateNavMeshLink();
        }

        private void OnDestroy()
        {
            NavLinkedLadders.Remove(this);

            if (NavMesh.IsLinkValid(m_LinkInstance))
                NavMesh.RemoveLink(m_LinkInstance);
        }

        private void TryCreateNavMeshLink()
        {
            if (m_Trigger == null)
                return;

            Bounds bounds = m_Trigger.bounds;
            Vector3 bottomProbe = new Vector3(bounds.center.x, transform.position.y, bounds.center.z);
            Vector3 topProbe = new Vector3(bounds.center.x, bounds.max.y, bounds.center.z);

            if (!NavMesh.SamplePosition(bottomProbe, out NavMeshHit bottomHit, BottomSearchRadius, NavMesh.AllAreas) ||
                !NavMesh.SamplePosition(topProbe, out NavMeshHit topHit, TopSearchRadius, NavMesh.AllAreas) ||
                topHit.position.y - bottomHit.position.y < MinClimbHeight)
            {
                Debug.LogWarning($"[LadderZone] Could not find navmesh at both ends of ladder '{name}', no navmesh link created.", this);
                return;
            }

            BottomPoint = bottomHit.position;
            TopPoint = topHit.position;

            NavMeshLinkData linkData = new NavMeshLinkData
            {
                startPosition = BottomPoint,
                endPosition = TopPoint,
                width = 0f,
                costModifier = -1f,
                bidirectional = true,
                area = 0,
                agentTypeID = NavMesh.GetSettingsByIndex(0).agentTypeID,
            };

            m_LinkInstance = NavMesh.AddLink(linkData);
            if (NavMesh.IsLinkValid(m_LinkInstance))
                NavLinkedLadders.Add(this);
        }

        // Returns the ladder whose navmesh link joins both points (in any direction)
        public static bool TryGetLadderForSegment(Vector3 from, Vector3 to, float tolerance, out LadderZone ladder, out bool goingUp)
        {
            float sqrTolerance = tolerance * tolerance;
            foreach (LadderZone candidate in NavLinkedLadders)
            {
                if (candidate == null)
                    continue;

                if ((from - candidate.BottomPoint).sqrMagnitude < sqrTolerance && (to - candidate.TopPoint).sqrMagnitude < sqrTolerance)
                {
                    ladder = candidate;
                    goingUp = true;
                    return true;
                }

                if ((from - candidate.TopPoint).sqrMagnitude < sqrTolerance && (to - candidate.BottomPoint).sqrMagnitude < sqrTolerance)
                {
                    ladder = candidate;
                    goingUp = false;
                    return true;
                }
            }

            ladder = null;
            goingUp = false;
            return false;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.TryGetComponent(out PlayerCharacterController player))
            {
                player.SetOnLadderState(true);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.TryGetComponent(out PlayerCharacterController player))
            {
                player.SetOnLadderState(false);
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (!NavMesh.IsLinkValid(m_LinkInstance))
                return;

            Gizmos.color = Color.cyan;
            Gizmos.DrawSphere(BottomPoint, 0.15f);
            Gizmos.DrawSphere(TopPoint, 0.15f);
            Gizmos.DrawLine(BottomPoint, TopPoint);
        }
    }
}
