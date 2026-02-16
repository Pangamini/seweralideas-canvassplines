using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace SeweralIdeas.CanvasSplines
{
    [RequireComponent(typeof(CanvasRenderer))]
    public class SplineGraphic : MaskableGraphic
    {
        protected override void OnEnable()
        {
            base.OnEnable();
            SubscribeChanged();
        }

        protected override void OnDisable()
        {
            UnsubscribeChanged();
            base.OnDisable();
        }

        private void SubscribeChanged()
        {
            if (_spline != null)
                _spline.Changed += OnChanged;
        }

        private void UnsubscribeChanged()
        {
            if (_spline != null)
                _spline.Changed -= OnChanged;
        }

        private void OnChanged()
        {
            SetVerticesDirty();
        }

        [FormerlySerializedAs("_splineContainer")]
        [Header("Spline Settings")]
        [SerializeField] private RectSpline _spline;
        [SerializeField] private float _width = 0.1f;

        [Header("Fill Interval")]
        [SerializeField, Range(0f, 1f)] private float _fillStart = 0f;
        [SerializeField, Range(0f, 1f)] private float _fillEnd = 1f;

        [Header("Caps")]
        [SerializeField] private bool _enableCaps = true;

        [Header("Mesh Quality")]
        [SerializeField] private int _segmentsPerUnit = 10;
        [SerializeField] private int _minSegments = 10;
        [SerializeField] private int _maxSegments = 500;

        private readonly List<Vector3> _vertices = new List<Vector3>();
        private readonly List<Color32> _colors = new List<Color32>();
        private readonly List<int> _triangles = new List<int>();
        private readonly List<Vector4> _uvs = new List<Vector4>();

        public RectSpline Spline
        {
            get => _spline;
            set
            {
                UnsubscribeChanged();
                _spline = value;
                SubscribeChanged();
                SetVerticesDirty();
            }
        }

        public float Width
        {
            get => _width;
            set
            {
                _width = value;
                SetVerticesDirty();
            }
        }

        public float FillStart
        {
            get => _fillStart;
            set
            {
                _fillStart = Mathf.Clamp01(value);
                SetVerticesDirty();
            }
        }

        public float FillEnd
        {
            get => _fillEnd;
            set
            {
                _fillEnd = Mathf.Clamp01(value);
                SetVerticesDirty();
            }
        }

        public bool EnableCaps
        {
            get => _enableCaps;
            set
            {
                _enableCaps = value;
                SetVerticesDirty();
            }
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();

            if (_spline == null)
                return;

            if (_fillStart > _fillEnd)
                return;

            if (_fillStart >= _fillEnd && !_enableCaps)
                return;

            GenerateSplineMesh(vh);
        }

        /// <summary>
        /// Converts a rect-local position from the container's RectTransform
        /// into this graphic's local space.
        /// </summary>
        private Vector2 ContainerToLocal(Vector2 containerRectLocal)
        {
            Vector3 world = _spline.RectTransform.TransformPoint(containerRectLocal);
            Vector3 local = transform.InverseTransformPoint(world);
            return new Vector2(local.x, local.y);
        }

        /// <summary>
        /// Converts a rect-local direction from the container's RectTransform
        /// into this graphic's local space.
        /// </summary>
        private Vector2 ContainerDirectionToLocal(Vector2 containerRectLocalDir)
        {
            Vector3 world = _spline.RectTransform.TransformDirection(new Vector3(containerRectLocalDir.x, containerRectLocalDir.y, 0f));
            Vector3 local = transform.InverseTransformDirection(world);
            return new Vector2(local.x, local.y);
        }

        private void GenerateSplineMesh(VertexHelper vh)
        {
            _vertices.Clear();
            _colors.Clear();
            _triangles.Clear();
            _uvs.Clear();

            float widthPixels = _spline.NormalizedScalarToRectLocal(_width);

            float splineLength = _spline.GetSplineLength();
            int segmentCount = Mathf.Clamp(
                Mathf.RoundToInt(splineLength * _segmentsPerUnit),
                _minSegments,
                _maxSegments
            );

            float startDist = _fillStart * splineLength;
            float endDist = _fillEnd * splineLength;

            int vertexOffset = 0;

            // Generate start cap if enabled
            if (_enableCaps)
            {
                float splineT = _spline.DistanceToT(startDist);
                Vector2 localPos = ContainerToLocal(_spline.EvaluatePosition(splineT));
                Vector2 localTangent = EvaluateTangentSafe(splineT).normalized;
                Vector2 perpendicular = new Vector2(-localTangent.y, localTangent.x);

                float radius = widthPixels * 0.5f;

                Vector2 capStart = localPos - localTangent * radius;

                Vector3 capBackLeft = capStart - perpendicular * radius;
                Vector3 capBackRight = capStart + perpendicular * radius;
                Vector3 capFrontLeft = localPos - perpendicular * radius;
                Vector3 capFrontRight = localPos + perpendicular * radius;

                _vertices.Add(capBackLeft);
                _vertices.Add(capBackRight);
                _vertices.Add(capFrontLeft);
                _vertices.Add(capFrontRight);

                Vector3 tangent3 = localTangent;
                Vector3 perp3 = perpendicular;
                _uvs.Add(EncodeCapDistanceUV(capBackLeft - (Vector3)localPos, tangent3, perp3, radius, false));
                _uvs.Add(EncodeCapDistanceUV(capBackRight - (Vector3)localPos, tangent3, perp3, radius, false));
                _uvs.Add(EncodeCapDistanceUV(capFrontLeft - (Vector3)localPos, tangent3, perp3, radius, false));
                _uvs.Add(EncodeCapDistanceUV(capFrontRight - (Vector3)localPos, tangent3, perp3, radius, false));

                _colors.Add(color);
                _colors.Add(color);
                _colors.Add(color);
                _colors.Add(color);

                _triangles.Add(0);
                _triangles.Add(2);
                _triangles.Add(1);

                _triangles.Add(1);
                _triangles.Add(2);
                _triangles.Add(3);

                vertexOffset = 4;
            }

            // Generate vertices along the spline (skip when fill range is zero)
            bool hasBody = startDist < endDist;
            if (!hasBody) segmentCount = 0;
            for (int i = 0; i <= segmentCount; i++)
            {
                float segmentFrac = (float)i / segmentCount;
                float currentDist = Mathf.Lerp(startDist, endDist, segmentFrac);
                
                float splineT = _spline.DistanceToT(currentDist);
                Vector2 localPos = ContainerToLocal(_spline.EvaluatePosition(splineT));
                Vector2 localTangent = EvaluateTangentSafe(splineT).normalized;
                
                Vector2 perpendicular = new Vector2(-localTangent.y, localTangent.x);

                Vector3 leftVertex = (Vector3)(localPos - perpendicular * (widthPixels * 0.5f));
                Vector3 rightVertex = (Vector3)(localPos + perpendicular * (widthPixels * 0.5f));

                _vertices.Add(leftVertex);
                _vertices.Add(rightVertex);

                _uvs.Add(EncodeDistanceUV(splineT, -1f));
                _uvs.Add(EncodeDistanceUV(splineT, 1f));

                _colors.Add(color);
                _colors.Add(color);
            }

            // Generate triangles for spline body
            int splineVertexStart = vertexOffset;
            for (int i = 0; i < segmentCount; i++)
            {
                int baseIndex = splineVertexStart + i * 2;

                _triangles.Add(baseIndex);
                _triangles.Add(baseIndex + 2);
                _triangles.Add(baseIndex + 1);

                _triangles.Add(baseIndex + 1);
                _triangles.Add(baseIndex + 2);
                _triangles.Add(baseIndex + 3);
            }

            // Generate end cap if enabled
            if (_enableCaps)
            {
                float splineT = _spline.DistanceToT(endDist);
                Vector2 localPos = ContainerToLocal(_spline.EvaluatePosition(splineT));
                Vector2 localTangent = EvaluateTangentSafe(splineT).normalized;
                Vector2 perpendicular = new Vector2(-localTangent.y, localTangent.x);
                
                // float normalizedT = endDist / splineLength;
                // Vector2 localPos = ContainerToLocal(_splineContainer.EvaluatePosition(_splineIndex, normalizedT));
                // Vector2 localTangent = EvaluateTangentSafe(normalizedT).normalized;
                // Vector2 perpendicular = new Vector2(-localTangent.y, localTangent.x);

                float radius = widthPixels * 0.5f;

                Vector2 capEnd = localPos + localTangent * radius;

                Vector3 capFrontLeft = localPos - perpendicular * radius;
                Vector3 capFrontRight = localPos + perpendicular * radius;
                Vector3 capBackLeft = capEnd - perpendicular * radius;
                Vector3 capBackRight = capEnd + perpendicular * radius;

                int endCapStart = _vertices.Count;

                _vertices.Add(capFrontLeft);
                _vertices.Add(capFrontRight);
                _vertices.Add(capBackLeft);
                _vertices.Add(capBackRight);

                Vector3 tangent3 = localTangent;
                Vector3 perp3 = perpendicular;
                _uvs.Add(EncodeCapDistanceUV(capFrontLeft - (Vector3)localPos, tangent3, perp3, radius, true));
                _uvs.Add(EncodeCapDistanceUV(capFrontRight - (Vector3)localPos, tangent3, perp3, radius, true));
                _uvs.Add(EncodeCapDistanceUV(capBackLeft - (Vector3)localPos, tangent3, perp3, radius, true));
                _uvs.Add(EncodeCapDistanceUV(capBackRight - (Vector3)localPos, tangent3, perp3, radius, true));

                _colors.Add(color);
                _colors.Add(color);
                _colors.Add(color);
                _colors.Add(color);

                _triangles.Add(endCapStart);
                _triangles.Add(endCapStart + 2);
                _triangles.Add(endCapStart + 1);

                _triangles.Add(endCapStart + 1);
                _triangles.Add(endCapStart + 2);
                _triangles.Add(endCapStart + 3);
            }

            // Add to vertex helper
            for (int i = 0; i < _vertices.Count; i++)
            {
                vh.AddVert(_vertices[i], _colors[i], _uvs[i]);
            }

            for (int i = 0; i < _triangles.Count; i += 3)
            {
                vh.AddTriangle(_triangles[i], _triangles[i + 1], _triangles[i + 2]);
            }
        }

        private Vector2 EvaluateTangentSafe(float t)
        {
            Vector2 tangent = ContainerDirectionToLocal(_spline.EvaluateTangent(t));
            if (tangent.sqrMagnitude < 0.0001f)
            {
                const float epsilon = 0.001f;
                if (t < 0.5f)
                    tangent = ContainerDirectionToLocal(_spline.EvaluateTangent(t + epsilon));
                else
                    tangent = ContainerDirectionToLocal(_spline.EvaluateTangent(t - epsilon));
            }
            if (tangent.sqrMagnitude < 0.0001f)
            {
                const float epsilon = 0.001f;
                float tA = Mathf.Clamp01(t - epsilon);
                float tB = Mathf.Clamp01(t + epsilon);
                tangent = ContainerToLocal(_spline.EvaluatePosition(tB))
                        - ContainerToLocal(_spline.EvaluatePosition(tA));
            }
            return tangent;
        }

        private static Vector4 EncodeDistanceUV(float alongSpline, float perpendicular)
        {
            return new Vector4(alongSpline, perpendicular, 0.5f, 0f);
        }

        private static Vector4 EncodeCapDistanceUV(Vector3 offsetFromEndpoint, Vector3 tangent, Vector3 perpendicular, float radius, bool isEndCap)
        {
            float alongTangent = Vector3.Dot(offsetFromEndpoint, tangent) / radius;
            float alongPerpendicular = Vector3.Dot(offsetFromEndpoint, perpendicular) / radius;

            return new Vector4(alongTangent, alongPerpendicular, isEndCap ? 1f : 0f, 0f);
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();

            if (_fillStart > _fillEnd)
            {
                _fillStart = _fillEnd;
            }

            SetVerticesDirty();
        }
#endif
    }
}
