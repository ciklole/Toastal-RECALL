using UnityEngine;

namespace ToastalRecall
{
    /// <summary>
    /// Single reset path used by manual respawns and hazards. Restores position,
    /// rotation, velocity, launch state and camera, then re-enables launching.
    /// </summary>
    [RequireComponent(typeof(BreadLaunchController))]
    public class BreadRespawnController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private BreadLaunchController launchController;
        [SerializeField] private BreadCameraFollow cameraFollow;
        [SerializeField] private Rigidbody breadRigidbody;

        [Header("Spawn")]
        [Tooltip("Optional respawn marker. If empty, the bread's starting transform is used.")]
        [SerializeField] private Transform spawnPoint;

        [Header("UI")]
        [SerializeField] private bool createRespawnButton = true;
        [Tooltip("Show the editable Respawn Canvas on death and wait for the player instead of resetting instantly.")]
        [SerializeField] private bool showRespawnScreen = true;

        [Tooltip("Brief grace period after a respawn so overlapping hazards cannot kill repeatedly.")]
        [SerializeField] private float respawnInvulnerability = 0.25f;

        public Vector3 SpawnPosition { get; private set; }
        public Quaternion SpawnRotation { get; private set; }
        public bool GameplayEnabled { get; private set; } = true;

        private bool _resetting;
        private float _nextHazardTime;

        private void Awake()
        {
            if (launchController == null)
                launchController = GetComponent<BreadLaunchController>();
            if (breadRigidbody == null)
                breadRigidbody = GetComponent<Rigidbody>();
            if (cameraFollow == null)
                cameraFollow = FindAnyObjectByType<BreadCameraFollow>();

            if (spawnPoint != null)
            {
                SpawnPosition = spawnPoint.position;
                SpawnRotation = spawnPoint.rotation;
            }
            else
            {
                SpawnPosition = transform.position;
                SpawnRotation = transform.rotation;
            }

            launchController.Launched += OnBreadLaunched;
        }

        private void Start()
        {
            if (createRespawnButton)
                GameplayHUD.EnsureFor(this);
        }

        private void OnDestroy()
        {
            if (launchController != null)
                launchController.Launched -= OnBreadLaunched;
        }

        private void OnBreadLaunched()
        {
            if (cameraFollow != null)
                cameraFollow.BeginFollowing(transform);
        }

        /// <summary>Manual respawn. Safe while idle, aiming, flying, or after collisions.</summary>
        public void Respawn()
        {
            if (!GameplayEnabled || _resetting)
                return;

            ResetBread();
        }

        /// <summary>Called by hazard objects. Shares the exact same reset path as Respawn().</summary>
        public void KillBread()
        {
            if (!GameplayEnabled || _resetting || Time.time < _nextHazardTime)
                return;

            if (showRespawnScreen)
            {
                BeginDeath();
                RespawnScreen.ShowFor(this);
                return;
            }

            ResetBread();
        }

        /// <summary>
        /// Locks the bread and HUD so the Respawn Canvas can take over without
        /// hazards firing repeatedly behind it.
        /// </summary>
        private void BeginDeath()
        {
            GameplayEnabled = false;

            if (launchController != null)
                launchController.LockInput();

            GameplayHUD.SetVisible(false);
        }

        /// <summary>Called by the Respawn Canvas button. Restores play and resets the bread.</summary>
        public void RespawnAfterDeath()
        {
            GameplayEnabled = true;
            _nextHazardTime = Time.time + respawnInvulnerability;

            ResetBread();
            GameplayHUD.SetVisible(true);
        }

        /// <summary>
        /// Locks hazards and manual respawns while the bread is settling on the goal.
        /// The Rigidbody is deliberately left simulated so it keeps its momentum.
        /// </summary>
        public void BeginLevelCompletion()
        {
            GameplayEnabled = false;
            if (launchController != null)
                launchController.LockInput();
            GameplayHUD.SetVisible(false);
        }

        /// <summary>Stops gameplay permanently for this attempt (level completed).</summary>
        public void DisableGameplay()
        {
            GameplayEnabled = false;
            if (launchController != null)
                launchController.DisableGameplay();
            if (cameraFollow != null)
                cameraFollow.StopFollowing();
            GameplayHUD.SetVisible(false);
        }

        private void ResetBread()
        {
            _resetting = true;

            if (breadRigidbody != null)
            {
                if (!breadRigidbody.isKinematic)
                {
                    breadRigidbody.linearVelocity = Vector3.zero;
                    breadRigidbody.angularVelocity = Vector3.zero;
                }

                breadRigidbody.isKinematic = true;
                breadRigidbody.useGravity = false;
            }

            transform.SetPositionAndRotation(SpawnPosition, SpawnRotation);
            Physics.SyncTransforms();

            if (launchController != null)
                launchController.ResetForRespawn();
            if (cameraFollow != null)
                cameraFollow.ResetCamera();

            _nextHazardTime = Time.time + respawnInvulnerability;
            _resetting = false;
        }
    }
}
