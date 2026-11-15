using UnityEngine;

namespace ToastalRecall
{
    /// <summary>
    /// Attach to the finish object. Any bread collider entering it completes the
    /// level. The object needs a Collider on the same GameObject (trigger or solid).
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class LevelGoal : MonoBehaviour
    {
        [SerializeField] private HazardTriggerMode triggerMode = HazardTriggerMode.CollisionOrTrigger;
        [SerializeField] private bool completeOnlyOnce = true;

        [Tooltip("Seconds the bread keeps its normal physics after touching the goal, before gameplay freezes.")]
        [SerializeField, Range(0f, 10f)] private float settleTime = 4f;

        private bool _triggered;

        private void OnCollisionEnter(Collision collision)
        {
            if (_triggered && completeOnlyOnce)
                return;
            if (triggerMode == HazardTriggerMode.Collision ||
                triggerMode == HazardTriggerMode.CollisionOrTrigger)
                TryComplete(collision.collider);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_triggered && completeOnlyOnce)
                return;
            if (triggerMode == HazardTriggerMode.Trigger ||
                triggerMode == HazardTriggerMode.CollisionOrTrigger)
                TryComplete(other);
        }

        private void TryComplete(Collider other)
        {
            if (other == null)
                return;

            var bread = other.GetComponentInParent<BreadLaunchController>();
            if (bread == null)
                return;

            if (_triggered && completeOnlyOnce)
                return;

            _triggered = true;

            // Lock hazards/respawns right away, but leave the Rigidbody simulating
            // so the bread visibly tumbles into the goal before it freezes.
            var respawn = other.GetComponentInParent<BreadRespawnController>();
            if (respawn != null)
                respawn.BeginLevelCompletion();

            LevelCompletionManager.Instance.BeginCompletion(settleTime);
        }
    }
}
