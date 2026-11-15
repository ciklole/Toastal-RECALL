using System.Collections;
using UnityEngine;

namespace ToastalRecall
{
    /// <summary>
    /// Smoothly follows the bread after launch while preserving the camera's
    /// rotation and original depth, keeping the 2D-style presentation intact.
    /// </summary>
    public class BreadCameraFollow : MonoBehaviour
    {
        [Header("Target")]
        [SerializeField] private Transform target;

        [Header("Follow")]
        [Tooltip("Additional offset from the camera's original position relative to the bread.")]
        [SerializeField] private Vector3 followOffset = Vector3.zero;
        [Tooltip("Seconds for the camera to catch up. Lower is snappier.")]
        [SerializeField] private float followSmoothTime = 0.22f;
        [Tooltip("Delay before the camera starts moving after launch.")]
        [SerializeField] private float followDelay = 0.12f;
        [SerializeField] private float lookAheadScale = 0.08f;
        [SerializeField] private float maxLookAhead = 1.5f;

        [Header("Zoom")]
        [SerializeField] private bool enableScrollZoom = true;
        [Tooltip("Smallest perspective field of view. Lower values zoom in.")]
        [SerializeField, Range(10f, 60f)] private float minimumFieldOfView = 28f;
        [Tooltip("Largest perspective field of view before the map edge limits zooming out.")]
        [SerializeField, Range(30f, 100f)] private float maximumFieldOfView = 72f;
        [SerializeField, Min(0.1f)] private float zoomDegreesPerWheelStep = 4f;
        [Tooltip("Extra space kept between the camera view and the room boundary.")]
        [SerializeField, Min(0f)] private float mapEdgePadding = 0.35f;

        [Header("Pre-launch Map Pan")]
        [Tooltip("Hold the middle mouse button and drag to look around before launching the bread.")]
        [SerializeField] private bool enablePreLaunchPan = true;

        [Header("Bounds (world X/Y)")]
        [SerializeField] private bool useBounds = false;
        [SerializeField] private Vector2 minBounds = new Vector2(-50f, -50f);
        [SerializeField] private Vector2 maxBounds = new Vector2(50f, 50f);

        [Header("Wall / Obstruction")]
        [Tooltip("Pull the camera in front of walls instead of letting it clip through them.")]
        [SerializeField] private bool avoidObstruction = true;
        [Tooltip("Only colliders on the Wall layer stop the camera. Props and other objects between the bread and camera are ignored.")]
        [SerializeField] private bool useWallLayerOnly = true;
        [Tooltip("Layer used when Use Wall Layer Only is enabled.")]
        [SerializeField] private string wallLayerName = "Wall";
        [Tooltip("Tag used as a backup when collecting the room boundary bounds.")]
        [SerializeField] private string wallTagName = "wall";
        [Tooltip("Used only when Use Wall Layer Only is disabled. Leave empty to use the Wall layer.")]
        [SerializeField] private LayerMask obstructionMask = 0;
        [Tooltip("Thickness of the camera's collision probe. Larger = safer near thin walls.")]
        [SerializeField, Min(0.01f)] private float obstructionRadius = 0.35f;
        [Tooltip("Extra space kept between the camera and a wall.")]
        [SerializeField, Min(0f)] private float obstructionMargin = 0.2f;
        [Tooltip("Closest the camera may get to the bread when squeezed by geometry.")]
        [SerializeField, Min(0.05f)] private float minimumDistance = 1.5f;

        public bool IsFollowing { get; private set; }

        private static readonly RaycastHit[] ObstructionHits = new RaycastHit[16];

        private Camera _camera;
        private Vector3 _originalPosition;
        private Quaternion _originalRotation;
        private Vector3 _baseOffsetFromTarget;
        private Vector3 _velocity;
        private Coroutine _followRoutine;
        private Rigidbody _targetBody;
        private Bounds _mapBounds;
        private bool _hasMapBounds;
        private bool _canPanBeforeLaunch = true;
        private bool _isPanning;
        private float _panPlaneZ;
        private Vector2 _lastPanPointerPosition;

        private void Awake()
        {
            _camera = GetComponent<Camera>();
            if (_camera == null)
                _camera = GetComponentInChildren<Camera>();

            _originalPosition = transform.position;
            _originalRotation = transform.rotation;
            _panPlaneZ = target != null ? target.position.z : 0f;

            ResolveObstructionMask();
            CacheMapBounds();

            if (target != null)
                CacheTarget(target);
        }

        /// <summary>
        /// Defaults the probe to the dedicated Wall layer. This keeps props (cups,
        /// cabinets, food, etc.) from pushing the camera right up to the bread.
        /// </summary>
        private void ResolveObstructionMask()
        {
            if (!useWallLayerOnly && obstructionMask.value != 0)
                return;

            int wallLayer = LayerMask.NameToLayer(wallLayerName);
            obstructionMask = wallLayer >= 0 ? 1 << wallLayer : 0;
        }

        private void LateUpdate()
        {
            if (!IsFollowing || target == null)
                return;

            Vector3 desired = target.position + _baseOffsetFromTarget +
                              transform.right * followOffset.x +
                              transform.up * followOffset.y +
                              transform.forward * followOffset.z;

            if (_targetBody != null && lookAheadScale > 0f)
            {
                Vector3 lookAhead = _targetBody.linearVelocity * lookAheadScale;
                if (lookAhead.magnitude > maxLookAhead)
                    lookAhead = lookAhead.normalized * maxLookAhead;
                desired += lookAhead;
            }

            if (useBounds)
            {
                desired.x = Mathf.Clamp(desired.x, minBounds.x, maxBounds.x);
                desired.y = Mathf.Clamp(desired.y, minBounds.y, maxBounds.y);
            }

            transform.position = Vector3.SmoothDamp(transform.position, desired,
                ref _velocity, Mathf.Max(0.01f, followSmoothTime));
            transform.rotation = _originalRotation;

            if (avoidObstruction && target != null)
                transform.position = ResolveObstruction(target.position, transform.position);

            transform.position = ClampCameraToMap(transform.position);
        }

        private void Update()
        {
            UpdatePreLaunchPan();

            if (!enableScrollZoom || _camera == null)
                return;

            float maximum = GetMapLimitedMaximumFieldOfView();
            if (!_camera.orthographic && _camera.fieldOfView > maximum)
                _camera.fieldOfView = maximum;

            if (ToastalInput.IsPointerOverUI())
                return;

            float wheel = ToastalInput.ScrollDelta.y;
            if (Mathf.Abs(wheel) < 0.01f)
                return;

            // Input System mouse wheels commonly report 120 units per notch,
            // while some devices report one unit. Normalize both conventions.
            float steps = Mathf.Abs(wheel) >= 10f ? wheel / 120f : wheel;
            if (_camera.orthographic)
            {
                float minSize = Mathf.Max(0.1f, minimumFieldOfView * 0.1f);
                float maxSize = Mathf.Max(minSize, maximumFieldOfView * 0.1f);
                _camera.orthographicSize = Mathf.Clamp(
                    _camera.orthographicSize - steps * zoomDegreesPerWheelStep * 0.1f,
                    minSize, maxSize);
            }
            else
            {
                _camera.fieldOfView = Mathf.Clamp(
                    _camera.fieldOfView - steps * zoomDegreesPerWheelStep,
                    minimumFieldOfView, maximum);
            }

            transform.position = ClampCameraToMap(transform.position);
        }

        private void UpdatePreLaunchPan()
        {
            if (!enablePreLaunchPan || !_canPanBeforeLaunch || IsFollowing || _camera == null)
            {
                _isPanning = false;
                return;
            }

            if (ToastalInput.MiddleButtonPressedThisFrame && !ToastalInput.IsPointerOverUI())
            {
                _isPanning = true;
                _lastPanPointerPosition = ToastalInput.PointerPosition;
                _velocity = Vector3.zero;
            }

            if (!_isPanning)
                return;

            if (!ToastalInput.MiddleButtonHeld)
            {
                _isPanning = false;
                return;
            }

            Vector2 pointerPosition = ToastalInput.PointerPosition;
            if (TryGetPanPlanePoint(_lastPanPointerPosition, out Vector3 previousPoint) &&
                TryGetPanPlanePoint(pointerPosition, out Vector3 currentPoint))
            {
                // Dragging the view right moves the camera left across the XY map.
                Vector3 desiredPosition = transform.position - (currentPoint - previousPoint);
                transform.position = ClampCameraToMap(desiredPosition);
            }

            _lastPanPointerPosition = pointerPosition;
        }

        private bool TryGetPanPlanePoint(Vector2 screenPosition, out Vector3 point)
        {
            Plane gameplayPlane = new Plane(Vector3.forward, new Vector3(0f, 0f, _panPlaneZ));
            Ray ray = _camera.ScreenPointToRay(screenPosition);
            if (gameplayPlane.Raycast(ray, out float distance))
            {
                point = ray.GetPoint(distance);
                return true;
            }

            point = Vector3.zero;
            return false;
        }

        private void OnValidate()
        {
            followSmoothTime = Mathf.Max(0.01f, followSmoothTime);
            followDelay = Mathf.Max(0f, followDelay);
            maxLookAhead = Mathf.Max(0f, maxLookAhead);
            minimumFieldOfView = Mathf.Clamp(minimumFieldOfView, 10f, 89f);
            maximumFieldOfView = Mathf.Clamp(maximumFieldOfView, minimumFieldOfView, 100f);
            zoomDegreesPerWheelStep = Mathf.Max(0.1f, zoomDegreesPerWheelStep);
            mapEdgePadding = Mathf.Max(0f, mapEdgePadding);
        }

        private void CacheMapBounds()
        {
            int wallLayer = LayerMask.NameToLayer(wallLayerName);
            if (wallLayer < 0 && string.IsNullOrWhiteSpace(wallTagName))
                return;

            bool found = false;
            var colliders = FindObjectsByType<Collider>(FindObjectsInactive.Exclude);
            foreach (Collider collider in colliders)
            {
                if (collider == null || !IsWallBoundaryObject(collider.gameObject, wallLayer))
                    continue;
                EncapsulateMapBounds(collider.bounds, ref found);
            }

            // Some room shells are render-only. Their renderers still give us a
            // stable play-area boundary when the Wall layer has no colliders.
            if (!found)
            {
                var renderers = FindObjectsByType<Renderer>(FindObjectsInactive.Exclude);
                foreach (Renderer renderer in renderers)
                {
                    if (renderer == null || !IsWallBoundaryObject(renderer.gameObject, wallLayer))
                        continue;
                    EncapsulateMapBounds(renderer.bounds, ref found);
                }
            }

            _hasMapBounds = found;
        }

        private bool IsWallBoundaryObject(GameObject candidate, int wallLayer)
        {
            if (candidate == null)
                return false;

            if (wallLayer >= 0 && candidate.layer == wallLayer)
                return true;

            return !string.IsNullOrWhiteSpace(wallTagName) && candidate.CompareTag(wallTagName);
        }

        private void EncapsulateMapBounds(Bounds bounds, ref bool found)
        {
            if (!found)
            {
                _mapBounds = bounds;
                found = true;
            }
            else
            {
                _mapBounds.Encapsulate(bounds);
            }
        }

        private float GetMapLimitedMaximumFieldOfView()
        {
            float maximum = Mathf.Clamp(maximumFieldOfView, minimumFieldOfView, 100f);
            if (!_hasMapBounds || _camera == null)
                return maximum;

            Plane gameplayPlane = new Plane(Vector3.forward, new Vector3(0f, 0f, _panPlaneZ));
            Ray centerRay = _camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            if (!gameplayPlane.Raycast(centerRay, out float enter))
                return minimumFieldOfView;

            Vector3 viewCenter = centerRay.GetPoint(enter);
            float distance = Vector3.Dot(viewCenter - transform.position, transform.forward);
            if (distance <= 0.1f)
                return minimumFieldOfView;

            float availableX = Mathf.Min(viewCenter.x - _mapBounds.min.x,
                _mapBounds.max.x - viewCenter.x) - mapEdgePadding;
            float availableY = Mathf.Min(viewCenter.y - _mapBounds.min.y,
                _mapBounds.max.y - viewCenter.y) - mapEdgePadding;
            if (availableX <= 0f || availableY <= 0f)
                return minimumFieldOfView;

            float aspect = Mathf.Max(0.01f, _camera.aspect);
            float xCoefficient = distance * (aspect * Mathf.Abs(transform.right.x) +
                                                Mathf.Abs(transform.up.x));
            float yCoefficient = distance * (aspect * Mathf.Abs(transform.right.y) +
                                                Mathf.Abs(transform.up.y));
            float maxTangent = float.PositiveInfinity;
            if (xCoefficient > 0.0001f)
                maxTangent = Mathf.Min(maxTangent, availableX / xCoefficient);
            if (yCoefficient > 0.0001f)
                maxTangent = Mathf.Min(maxTangent, availableY / yCoefficient);

            if (float.IsInfinity(maxTangent) || maxTangent <= 0f)
                return minimumFieldOfView;

            float mapMaximum = 2f * Mathf.Atan(maxTangent) * Mathf.Rad2Deg;
            return Mathf.Clamp(Mathf.Min(maximum, mapMaximum), minimumFieldOfView, maximum);
        }

        private Vector3 ClampCameraToMap(Vector3 position)
        {
            if (!_hasMapBounds || _camera == null)
                return position;

            if (!TryGetViewBoundsOnMap(out Bounds viewBounds))
                return position;

            // Viewport rays are based on the current camera transform. Account for
            // the proposed pan before finding the smallest correction that keeps
            // the complete visible area inside the room bounds.
            float panX = position.x - transform.position.x;
            float panY = position.y - transform.position.y;
            float minX = viewBounds.min.x + panX;
            float maxX = viewBounds.max.x + panX;
            float minY = viewBounds.min.y + panY;
            float maxY = viewBounds.max.y + panY;

            float innerMinX = _mapBounds.min.x + mapEdgePadding;
            float innerMaxX = _mapBounds.max.x - mapEdgePadding;
            float innerMinY = _mapBounds.min.y + mapEdgePadding;
            float innerMaxY = _mapBounds.max.y - mapEdgePadding;

            position.x += GetBoundsCorrection(minX, maxX, innerMinX, innerMaxX);
            position.y += GetBoundsCorrection(minY, maxY, innerMinY, innerMaxY);
            return position;
        }

        private bool TryGetViewBoundsOnMap(out Bounds bounds)
        {
            bounds = default;
            if (_camera == null)
                return false;

            Plane gameplayPlane = new Plane(Vector3.forward, new Vector3(0f, 0f, _panPlaneZ));
            bool foundCorner = false;
            Vector3 min = new Vector3(float.PositiveInfinity, float.PositiveInfinity, _panPlaneZ);
            Vector3 max = new Vector3(float.NegativeInfinity, float.NegativeInfinity, _panPlaneZ);

            for (int x = 0; x <= 1; x++)
            {
                for (int y = 0; y <= 1; y++)
                {
                    Ray ray = _camera.ViewportPointToRay(new Vector3(x, y, 0f));
                    if (!gameplayPlane.Raycast(ray, out float enter))
                        continue;

                    Vector3 corner = ray.GetPoint(enter);
                    min.x = Mathf.Min(min.x, corner.x);
                    min.y = Mathf.Min(min.y, corner.y);
                    max.x = Mathf.Max(max.x, corner.x);
                    max.y = Mathf.Max(max.y, corner.y);
                    foundCorner = true;
                }
            }

            if (foundCorner)
                bounds.SetMinMax(min, max);

            return foundCorner;
        }

        private static float GetBoundsCorrection(float viewMin, float viewMax,
            float allowedMin, float allowedMax)
        {
            if (viewMax - viewMin >= allowedMax - allowedMin)
                return (allowedMin + allowedMax - viewMin - viewMax) * 0.5f;

            if (viewMin < allowedMin)
                return allowedMin - viewMin;
            if (viewMax > allowedMax)
                return allowedMax - viewMax;
            return 0f;
        }

        /// <summary>
        /// Casts a fat sphere from the bread toward the wanted camera position and
        /// shortens the offset so the camera stops just in front of any wall.
        /// </summary>
        private Vector3 ResolveObstruction(Vector3 pivot, Vector3 wanted)
        {
            if (obstructionMask.value == 0)
                return wanted;

            Vector3 delta = wanted - pivot;
            float distance = delta.magnitude;
            if (distance <= 0.001f)
                return wanted;

            Vector3 direction = delta / distance;
            int count = Physics.SphereCastNonAlloc(pivot, obstructionRadius, direction,
                ObstructionHits, distance, obstructionMask, QueryTriggerInteraction.Ignore);

            float allowed = distance;
            for (int i = 0; i < count; i++)
            {
                var hit = ObstructionHits[i];
                if (hit.collider == null || IsTargetCollider(hit.collider))
                    continue;

                if (hit.distance < allowed)
                    allowed = hit.distance;
            }

            if (allowed >= distance)
                return wanted;

            allowed = Mathf.Max(minimumDistance, allowed - obstructionMargin);
            return pivot + direction * Mathf.Min(allowed, distance);
        }

        private bool IsTargetCollider(Collider collider)
        {
            if (target == null || collider == null)
                return false;

            if (_targetBody != null && collider.attachedRigidbody == _targetBody)
                return true;

            return collider.transform == target || collider.transform.IsChildOf(target);
        }

        public void BeginFollowing(Transform newTarget)
        {
            if (newTarget == null)
                return;

            if (_camera == null)
                _camera = GetComponent<Camera>();

            CacheTarget(newTarget);
            // Pre-launch panning is for scouting only. Once the toast is thrown,
            // follow it from the original camera offset rather than carrying the
            // scouting pan into the chase camera.
            _baseOffsetFromTarget = _originalPosition - newTarget.position;
            _canPanBeforeLaunch = false;
            _isPanning = false;

            if (_followRoutine != null)
                StopCoroutine(_followRoutine);

            _velocity = Vector3.zero;
            _followRoutine = StartCoroutine(FollowAfterDelay());
        }

        public void StopFollowing()
        {
            IsFollowing = false;
            if (_followRoutine != null)
            {
                StopCoroutine(_followRoutine);
                _followRoutine = null;
            }
        }

        public void ResetCamera()
        {
            StopFollowing();
            _canPanBeforeLaunch = true;
            _isPanning = false;
            transform.SetPositionAndRotation(_originalPosition, _originalRotation);
            _velocity = Vector3.zero;
        }

        private void CacheTarget(Transform newTarget)
        {
            target = newTarget;
            _targetBody = newTarget.GetComponent<Rigidbody>();
            _baseOffsetFromTarget = transform.position - newTarget.position;
        }

        private IEnumerator FollowAfterDelay()
        {
            if (followDelay > 0f)
                yield return new WaitForSeconds(followDelay);

            IsFollowing = true;
            _followRoutine = null;
        }

        private void OnDisable()
        {
            StopFollowing();
        }
    }
}
