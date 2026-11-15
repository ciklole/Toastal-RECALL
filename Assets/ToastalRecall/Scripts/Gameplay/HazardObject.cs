using UnityEngine;

namespace ToastalRecall
{
    public enum HazardTriggerMode
    {
        Collision,
        Trigger,
        CollisionOrTrigger
    }

    /// <summary>
    /// Attach to any object that should kill the bread. Normal collisions with
    /// other objects are unaffected. The object needs a Collider on the same
    /// GameObject; for trigger mode, enable Is Trigger on that collider.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class HazardObject : MonoBehaviour
    {
        [SerializeField] private HazardTriggerMode triggerMode = HazardTriggerMode.CollisionOrTrigger;

        private void OnCollisionEnter(Collision collision)
        {
            if (triggerMode == HazardTriggerMode.Collision ||
                triggerMode == HazardTriggerMode.CollisionOrTrigger)
                TryKill(collision.collider);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (triggerMode == HazardTriggerMode.Trigger ||
                triggerMode == HazardTriggerMode.CollisionOrTrigger)
                TryKill(other);
        }

        private void TryKill(Collider other)
        {
            if (other == null)
                return;

            var respawn = other.GetComponentInParent<BreadRespawnController>();
            if (respawn == null)
                return;

            respawn.KillBread();
        }
    }
}
