using System;
using UnityEngine;

namespace ToastalRecall
{
    /// <summary>
    /// XY-plane bread launcher. Drag toward the intended travel direction; the bread
    /// stays kinematic while aiming, then launches with a physics impulse. Rotation
    /// remains unconstrained so the bread can tumble freely in 3D.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class BreadLaunchController : MonoBehaviour
    {
        public enum LaunchState
        {
            Ready,
            Aiming,
            Flying,
            Disabled
        }

        [Header("References")]
        [SerializeField] private Camera gameplayCamera;
        [SerializeField] private Rigidbody breadRigidbody;
        [Tooltip("Optional. If empty, any collider on this object or its children is used.")]
        [SerializeField] private Collider interactionCollider;
        [SerializeField] private LayerMask clickableLayers = ~0;

        [Header("Launch")]
        [Tooltip("Impulse applied at the minimum drag distance (Newton-seconds).")]
        [SerializeField] private float minLaunchForce = 3f;
        [Tooltip("Impulse applied at the maximum drag distance (Newton-seconds).")]
        [SerializeField] private float maxLaunchForce = 16f;
        [Tooltip("Multiplies drag distance. Higher = stronger pull for the same mouse movement.")]
        [SerializeField, Range(0.1f, 3f)] private float dragSensitivity = 1f;
        [Tooltip("Maximum pull distance in world units.")]
        [SerializeField] private float maxDragDistance = 3f;
        [Tooltip("Pull distances below this cancel the launch.")]
        [SerializeField] private float minimumDragToLaunch = 0.12f;
        [Tooltip("Optional delay before another launch is allowed after a launch/respawn.")]
        [SerializeField] private float launchCooldown = 0.2f;

        [Header("Rotation")]
        [SerializeField] private bool applyLaunchSpin = true;
        [SerializeField] private float spinPerImpulse = 0.45f;
        [SerializeField] private float maxLaunchSpin = 8f;

        [Header("Aim Visuals")]
        [SerializeField] private bool showAimLine = true;
        [SerializeField] private bool showTrajectoryPreview = true;
        [SerializeField] private int trajectorySamples = 26;
        [SerializeField] private float trajectoryTimeStep = 0.06f;
        [SerializeField] private float aimLineWidth = 0.035f;
        [SerializeField] private Color aimColor = new Color(1f, 0.85f, 0.45f, 0.95f);
        [SerializeField] private Color trajectoryColor = new Color(1f, 0.95f, 0.75f, 0.85f);

        [Header("Physics Safety")]
        [SerializeField] private bool useContinuousCollision = true;
        [Tooltip("Imported bread meshes often use concave MeshColliders, which dynamic Rigidbodies cannot use. This makes them convex at runtime.")]
        [SerializeField] private bool makeMeshCollidersConvex = true;
        [SerializeField] private float maxSpeed = 60f;
        [Tooltip("Multiplies the project's Physics.gravity for this bread only. 1 = normal gravity.")]
        [SerializeField, Min(0f)] private float gravityMultiplier = 1f;

        [Header("Bounce")]
        [Tooltip("How much impact energy the bread keeps when it hits a surface. Higher values bounce more.")]
        [SerializeField, Range(0f, 1f)] private float bounciness = 0.8f;
        [SerializeField, Range(0f, 1f)] private float bounceStaticFriction = 0.25f;
        [SerializeField, Range(0f, 1f)] private float bounceDynamicFriction = 0.15f;

        public event Action Launched;

        public LaunchState State => _state;
        public bool IsReady => _state == LaunchState.Ready;
        public float CurrentPullDistance => _currentPullDistance;
        public float CurrentLaunchForce => _currentLaunchForce;

        private LaunchState _state = LaunchState.Ready;
        private Plane _aimPlane;
        private Vector3 _dragStartWorld;
        private bool _inputLocked;
        private float _currentPullDistance;
        private float _currentLaunchForce;
        private Vector3 _pendingImpulse;
        private float _nextLaunchTime;

        private LineRenderer _aimLine;
        private LineRenderer _trajectoryLine;
        private Material _lineMaterial;
        private PhysicsMaterial _bounceMaterial;
        private Collider[] _breadColliders;
        private PhysicsMaterial[] _originalColliderMaterials;
        private RigidbodyConstraints _originalConstraints;

        private void Awake()
        {
            if (breadRigidbody == null)
                breadRigidbody = GetComponent<Rigidbody>();
            if (interactionCollider == null)
                interactionCollider = GetComponentInChildren<Collider>();
            if (gameplayCamera == null)
                gameplayCamera = Camera.main;

            if (breadRigidbody != null)
            {
                _originalConstraints = breadRigidbody.constraints;
                // Keep the bread on its world XY gameplay plane while leaving all
                // rotation axes free so impacts can tumble it in 3D.
                breadRigidbody.constraints = RigidbodyConstraints.FreezePositionZ;
                breadRigidbody.interpolation = RigidbodyInterpolation.Interpolate;
                if (useContinuousCollision)
                    breadRigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                breadRigidbody.isKinematic = true;
                breadRigidbody.useGravity = false;
            }

            if (makeMeshCollidersConvex)
                MakeMeshCollidersConvex();

            ApplyBounceMaterial();

            CreateAimVisuals();
        }

        /// <summary>
        /// A concave MeshCollider is ignored by a dynamic Rigidbody (and Unity logs a
        /// warning). Bread meshes should use convex colliders, so fix them at runtime
        /// without touching the imported asset.
        /// </summary>
        private void MakeMeshCollidersConvex()
        {
            foreach (var meshCollider in GetComponentsInChildren<MeshCollider>(true))
            {
                if (meshCollider != null && !meshCollider.convex)
                    meshCollider.convex = true;
            }
        }

        /// <summary>
        /// Adds bounce to the bread's colliders at runtime, leaving imported meshes
        /// and scene assets untouched. Maximum combine lets it bounce on default surfaces.
        /// </summary>
        private void ApplyBounceMaterial()
        {
            _breadColliders = GetComponentsInChildren<Collider>(true);
            _originalColliderMaterials = new PhysicsMaterial[_breadColliders.Length];
            _bounceMaterial = new PhysicsMaterial("Bread Bounce")
            {
                bounciness = bounciness,
                staticFriction = bounceStaticFriction,
                dynamicFriction = bounceDynamicFriction,
                bounceCombine = PhysicsMaterialCombine.Maximum,
                frictionCombine = PhysicsMaterialCombine.Average
            };

            for (int i = 0; i < _breadColliders.Length; i++)
            {
                Collider breadCollider = _breadColliders[i];
                if (breadCollider == null)
                    continue;

                _originalColliderMaterials[i] = breadCollider.sharedMaterial;
                breadCollider.sharedMaterial = _bounceMaterial;
            }
        }

        private void Start()
        {
            if (gameplayCamera == null)
                gameplayCamera = Camera.main;

            _state = LaunchState.Ready;
        }

        private void OnValidate()
        {
            minLaunchForce = Mathf.Max(0f, minLaunchForce);
            maxLaunchForce = Mathf.Max(minLaunchForce, maxLaunchForce);
            maxDragDistance = Mathf.Max(0.01f, maxDragDistance);
            minimumDragToLaunch = Mathf.Clamp(minimumDragToLaunch, 0f, maxDragDistance);
            dragSensitivity = Mathf.Clamp(dragSensitivity, 0.1f, 3f);
            launchCooldown = Mathf.Max(0f, launchCooldown);
            trajectorySamples = Mathf.Clamp(trajectorySamples, 2, 80);
            trajectoryTimeStep = Mathf.Max(0.005f, trajectoryTimeStep);
            aimLineWidth = Mathf.Max(0.001f, aimLineWidth);
            spinPerImpulse = Mathf.Max(0f, spinPerImpulse);
            maxLaunchSpin = Mathf.Max(0f, maxLaunchSpin);
            maxSpeed = Mathf.Max(0f, maxSpeed);
            gravityMultiplier = Mathf.Max(0f, gravityMultiplier);
            bounciness = Mathf.Clamp01(bounciness);
            bounceStaticFriction = Mathf.Clamp01(bounceStaticFriction);
            bounceDynamicFriction = Mathf.Clamp01(bounceDynamicFriction);
        }

        private void Update()
        {
            if (_inputLocked)
                return;

            if (_state == LaunchState.Ready)
            {
                TryBeginAim();
                return;
            }

            if (_state != LaunchState.Aiming)
                return;

            if (ToastalInput.PointerReleasedThisFrame)
            {
                FinishAim(true);
                return;
            }

            if (!ToastalInput.PointerHeld)
            {
                CancelAim();
                return;
            }

            UpdateAim();
        }

        private void FixedUpdate()
        {
            if (_state != LaunchState.Flying || breadRigidbody == null)
                return;

            if (!Mathf.Approximately(gravityMultiplier, 1f))
                breadRigidbody.AddForce(Physics.gravity * (gravityMultiplier - 1f), ForceMode.Acceleration);

            // Keep translation on the world XY plane even if a collision or custom
            // gravity introduces depth velocity. Rigidbody rotation is left free.
            Vector3 velocity = breadRigidbody.linearVelocity;
            velocity.z = 0f;

            if (maxSpeed <= 0f)
            {
                breadRigidbody.linearVelocity = velocity;
                return;
            }

            float sqrMax = maxSpeed * maxSpeed;
            if (velocity.sqrMagnitude > sqrMax)
                velocity = velocity.normalized * maxSpeed;

            breadRigidbody.linearVelocity = velocity;
        }

        private void TryBeginAim()
        {
            if (Time.time < _nextLaunchTime || gameplayCamera == null)
                return;
            if (!ToastalInput.PointerPressedThisFrame || ToastalInput.IsPointerOverUI())
                return;

            Ray ray = gameplayCamera.ScreenPointToRay(ToastalInput.PointerPosition);
            if (!Physics.Raycast(ray, out RaycastHit hit, 1000f, clickableLayers,
                    QueryTriggerInteraction.Ignore))
                return;

            if (!IsBreadCollider(hit.collider))
                return;

            _aimPlane = new Plane(Vector3.forward, transform.position);
            if (!TryGetPlanePoint(ray, out _dragStartWorld))
                return;

            _state = LaunchState.Aiming;
            _currentPullDistance = 0f;
            _currentLaunchForce = minLaunchForce;
            _pendingImpulse = Vector3.zero;
            UpdateAimVisuals();
        }

        private bool IsBreadCollider(Collider hitCollider)
        {
            if (hitCollider == null)
                return false;

            if (interactionCollider != null && hitCollider == interactionCollider)
                return true;

            if (hitCollider.attachedRigidbody == breadRigidbody)
                return true;

            return hitCollider.transform == transform || hitCollider.transform.IsChildOf(transform);
        }

        private void UpdateAim()
        {
            Ray ray = gameplayCamera.ScreenPointToRay(ToastalInput.PointerPosition);
            if (!TryGetPlanePoint(ray, out Vector3 pointerWorld))
                return;

            Vector3 pull = Vector3.ProjectOnPlane(pointerWorld - _dragStartWorld,
                Vector3.forward);

            float rawDistance = pull.magnitude * dragSensitivity;
            _currentPullDistance = Mathf.Min(rawDistance, maxDragDistance);

            if (_currentPullDistance <= 0.0001f)
            {
                _currentLaunchForce = minLaunchForce;
                _pendingImpulse = Vector3.zero;
                UpdateAimVisuals();
                return;
            }

            float ratio = Mathf.Clamp01(_currentPullDistance / Mathf.Max(0.0001f, maxDragDistance));
            _currentLaunchForce = Mathf.Lerp(minLaunchForce, maxLaunchForce, ratio);

            // Pulling back aims the bread in the opposite direction, like a slingshot.
            Vector3 launchDirection = -pull.normalized;
            _pendingImpulse = launchDirection * _currentLaunchForce;
            UpdateAimVisuals();
        }

        private bool TryGetPlanePoint(Ray ray, out Vector3 point)
        {
            if (_aimPlane.Raycast(ray, out float enter))
            {
                point = ray.GetPoint(enter);
                return true;
            }

            point = Vector3.zero;
            return false;
        }

        private void FinishAim(bool released)
        {
            if (_state != LaunchState.Aiming)
                return;

            if (!released || _currentPullDistance < minimumDragToLaunch ||
                _pendingImpulse.sqrMagnitude <= 0.0001f)
            {
                CancelAim();
                return;
            }

            Launch(_pendingImpulse);
        }

        private void CancelAim()
        {
            _state = LaunchState.Ready;
            _pendingImpulse = Vector3.zero;
            _currentPullDistance = 0f;
            HideAimVisuals();
        }

        private void Launch(Vector3 impulse)
        {
            if (breadRigidbody == null)
            {
                CancelAim();
                return;
            }

            _state = LaunchState.Flying;
            _nextLaunchTime = Time.time + launchCooldown;

            breadRigidbody.isKinematic = false;
            breadRigidbody.useGravity = true;
            if (useContinuousCollision)
                breadRigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            breadRigidbody.linearVelocity = Vector3.zero;
            breadRigidbody.angularVelocity = Vector3.zero;
            impulse = Vector3.ProjectOnPlane(impulse, Vector3.forward);
            breadRigidbody.AddForce(impulse, ForceMode.Impulse);

            if (applyLaunchSpin)
            {
                Vector3 viewAxis = gameplayCamera != null
                    ? gameplayCamera.transform.forward
                    : Vector3.forward;
                Vector3 cameraRight = gameplayCamera != null
                    ? gameplayCamera.transform.right
                    : Vector3.right;

                float spinSign = Vector3.Dot(impulse, cameraRight) >= 0f ? -1f : 1f;
                float spin = Mathf.Clamp(impulse.magnitude * spinPerImpulse, 0f, maxLaunchSpin);
                breadRigidbody.angularVelocity = viewAxis * (spin * spinSign);
            }

            HideAimVisuals();
            Launched?.Invoke();
        }

        /// <summary>Returns the bread to a launchable state after a respawn.</summary>
        public void ResetForRespawn()
        {
            _inputLocked = false;
            _state = LaunchState.Ready;
            _pendingImpulse = Vector3.zero;
            _currentPullDistance = 0f;
            _currentLaunchForce = minLaunchForce;
            _nextLaunchTime = Time.time + launchCooldown;

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

            HideAimVisuals();
        }

        /// <summary>
        /// Blocks new aiming/launches while leaving the current Rigidbody simulation
        /// untouched. Used while the bread settles after reaching the goal.
        /// </summary>
        public void LockInput()
        {
            _inputLocked = true;
            if (_state == LaunchState.Aiming)
                CancelAim();
            HideAimVisuals();
        }

        /// <summary>Stops the bread and blocks further input (used on level completion).</summary>
        public void DisableGameplay()
        {
            _inputLocked = true;
            _state = LaunchState.Disabled;
            _pendingImpulse = Vector3.zero;
            _currentPullDistance = 0f;

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

            HideAimVisuals();
        }

        public void SetReadyAfterCooldown()
        {
            _state = LaunchState.Ready;
            _nextLaunchTime = Time.time + launchCooldown;
        }

        private void UpdateAimVisuals()
        {
            if (!showAimLine && !showTrajectoryPreview)
                return;

            float ratio = Mathf.Clamp01(_currentPullDistance / Mathf.Max(0.0001f, maxDragDistance));
            bool hasPull = _currentPullDistance > minimumDragToLaunch && _pendingImpulse.sqrMagnitude > 0.0001f;

            if (_aimLine != null)
            {
                _aimLine.enabled = showAimLine && hasPull;
                if (_aimLine.enabled)
                {
                    Vector3 direction = _pendingImpulse.normalized;
                    float length = 0.7f + ratio * 1.8f;
                    _aimLine.positionCount = 2;
                    _aimLine.SetPosition(0, transform.position);
                    _aimLine.SetPosition(1, transform.position + direction * length);
                    _aimLine.startColor = aimColor;
                    _aimLine.endColor = new Color(aimColor.r, aimColor.g, aimColor.b, 0.15f);
                }
            }

            if (_trajectoryLine != null)
            {
                _trajectoryLine.enabled = showTrajectoryPreview && hasPull;
                if (_trajectoryLine.enabled)
                    UpdateTrajectoryPreview();
            }
        }

        private void UpdateTrajectoryPreview()
        {
            if (breadRigidbody == null)
                return;

            float mass = Mathf.Max(0.01f, breadRigidbody.mass);
            Vector3 velocity = Vector3.ProjectOnPlane(_pendingImpulse / mass, Vector3.forward);
            Vector3 position = transform.position;

            int count = Mathf.Clamp(trajectorySamples, 2, 80);
            _trajectoryLine.positionCount = count;
            _trajectoryLine.SetPosition(0, position);

            int used = 1;
            for (int i = 1; i < count; i++)
            {
                Vector3 previous = position;
                velocity += Vector3.ProjectOnPlane(Physics.gravity, Vector3.forward) *
                            gravityMultiplier * trajectoryTimeStep;
                position += velocity * trajectoryTimeStep;

                if (Physics.Linecast(previous, position, out RaycastHit hit, clickableLayers,
                        QueryTriggerInteraction.Ignore) &&
                    !IsBreadCollider(hit.collider))
                {
                    _trajectoryLine.SetPosition(used++, hit.point);
                    break;
                }

                _trajectoryLine.SetPosition(used++, position);
            }

            if (used < count)
                _trajectoryLine.positionCount = used;

            _trajectoryLine.startColor = trajectoryColor;
            _trajectoryLine.endColor = new Color(trajectoryColor.r, trajectoryColor.g,
                trajectoryColor.b, 0.10f);
        }

        private void CreateAimVisuals()
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null)
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
                shader = Shader.Find("Unlit/Color");

            if (shader != null)
                _lineMaterial = new Material(shader) { color = Color.white };

            _aimLine = CreateLineRenderer("Aim Line", aimColor);
            _trajectoryLine = CreateLineRenderer("Trajectory Preview", trajectoryColor);
        }

        private LineRenderer CreateLineRenderer(string name, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);

            var line = go.AddComponent<LineRenderer>();
            if (_lineMaterial != null)
                line.material = _lineMaterial;
            line.useWorldSpace = true;
            line.widthMultiplier = aimLineWidth;
            line.numCapVertices = 4;
            line.numCornerVertices = 2;
            line.alignment = LineAlignment.View;
            line.textureMode = LineTextureMode.Stretch;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.startColor = color;
            line.endColor = new Color(color.r, color.g, color.b, 0.1f);
            line.positionCount = 0;
            line.enabled = false;
            return line;
        }

        private void HideAimVisuals()
        {
            if (_aimLine != null)
                _aimLine.enabled = false;
            if (_trajectoryLine != null)
                _trajectoryLine.enabled = false;
        }

        private void OnDestroy()
        {
            if (_breadColliders != null && _originalColliderMaterials != null)
            {
                int count = Mathf.Min(_breadColliders.Length, _originalColliderMaterials.Length);
                for (int i = 0; i < count; i++)
                {
                    Collider breadCollider = _breadColliders[i];
                    if (breadCollider != null && breadCollider.sharedMaterial == _bounceMaterial)
                        breadCollider.sharedMaterial = _originalColliderMaterials[i];
                }
            }

            if (_bounceMaterial != null)
                Destroy(_bounceMaterial);
            if (breadRigidbody != null)
                breadRigidbody.constraints = _originalConstraints;
            if (_lineMaterial != null)
                Destroy(_lineMaterial);
        }
    }
}
