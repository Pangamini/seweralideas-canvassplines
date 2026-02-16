using System;
using UnityEngine;

namespace SeweralIdeas.CanvasSplines
{
    [RequireComponent(typeof(RectTransform))]
    public class RectSpline : MonoBehaviour
    {
        [SerializeField] private Spline _spline = new();
        
        private const int LutSamples = 128;
        private RectTransform _rectTransform;
        private float[] _cumulativeDistances;
        private Vector2[] _lutNormalizedPositions;
        private float _totalLength;
        private bool _lutDirty = true;
        
        public event Action Changed;

        public RectTransform RectTransform
        {
            get
            {
                if (_rectTransform == null)
                    _rectTransform = (RectTransform)transform;
                return _rectTransform;
            }
        }

        /// <summary>Returns position in the RectTransform's local space (rect-local units).</summary>
        public Vector2 EvaluatePosition(float t)
        {
            Vector2 normalized = _spline.EvaluatePosition(t);
            return NormalizedToRectLocal(normalized);
        }

        /// <summary>Returns tangent in the RectTransform's local space (rect-local units).</summary>
        public Vector2 EvaluateTangent(float t)
        {
            Vector2 normalizedTangent = _spline.EvaluateTangent(t);
            Rect rect = RectTransform.rect;
            return new Vector2(
                normalizedTangent.x * rect.width,
                normalizedTangent.y * rect.height);
        }

        /// <summary>Arc length in rect-local units.</summary>
        public float GetSplineLength()
        {
            EnsureLut();
            return _totalLength;
        }

        /// <summary>
        /// Converts a distance along the spline (in rect-local units) to the raw spline parameter t.
        /// </summary>
        public float DistanceToT(float distance)
        {
            EnsureLut();
            float[] lut = _cumulativeDistances;
            float totalLength = GetSplineLength();

            if (totalLength <= 0f) return 0f;
            distance = Mathf.Clamp(distance, 0f, totalLength);

            // Binary search for the bracket
            int lo = 0, hi = LutSamples;
            while (lo < hi - 1)
            {
                int mid = (lo + hi) / 2;
                if (lut[mid] <= distance)
                    lo = mid;
                else
                    hi = mid;
            }

            float segDist = lut[hi] - lut[lo];
            float frac = segDist > 0f ? (distance - lut[lo]) / segDist : 0f;
            return (lo + frac) / LutSamples;
        }

        /// <summary>
        /// Converts a distance fraction (0-1, as used by FillStart/FillEnd) to raw spline parameter t.
        /// </summary>
        public float DistanceFractionToT(float distanceFraction)
        {
            return DistanceToT(distanceFraction * GetSplineLength());
        }

        /// <summary>
        /// Converts a raw spline parameter t to a distance fraction (0-1, uniform along arc length).
        /// </summary>
        public float TToDistanceFraction(float t)
        {
            EnsureLut();
            float totalLength = _totalLength;
            if (totalLength <= 0f) return 0f;

            t = Mathf.Clamp01(t);
            float fi = t * LutSamples;
            int lo = Mathf.Min((int)fi, LutSamples - 1);
            int hi = lo + 1;
            float frac = fi - lo;

            float dist = Mathf.Lerp(_cumulativeDistances[lo], _cumulativeDistances[hi], frac);
            return dist / totalLength;
        }

        private void EnsureLut()
        {
            if (!_lutDirty && _cumulativeDistances != null)
                return;

            RebuildLut();
        }

        private void RebuildLut()
        {
            _lutDirty = false;

            if (_spline == null || _spline.Knots.Length < 2)
            {
                _totalLength = 0f;
                _cumulativeDistances = null;
                return;
            }

            if (_cumulativeDistances is not { Length: LutSamples + 1 })
                _cumulativeDistances = new float[LutSamples + 1];
            if (_lutNormalizedPositions is not { Length: LutSamples + 1 })
                _lutNormalizedPositions = new Vector2[LutSamples + 1];

            Vector2 firstPos = _spline.EvaluatePosition(0f);
            _cumulativeDistances[0] = 0f;
            _lutNormalizedPositions[0] = firstPos;

            Vector2 prev = firstPos;
            for (int i = 1; i <= LutSamples; i++)
            {
                float t = (float)i / LutSamples;
                Vector2 curr = _spline.EvaluatePosition(t);
                _lutNormalizedPositions[i] = curr;
                _cumulativeDistances[i] = _cumulativeDistances[i - 1] + Vector2.Distance(prev, curr);
                prev = curr;
            }

            _totalLength = _cumulativeDistances[LutSamples];
        }

        public float NormalizedScalarToRectLocal(float normalized)
        {
            Rect rect = RectTransform.rect;
            return normalized * Mathf.Min(rect.width, rect.height);
        }

        private Vector2 NormalizedToRectLocal(Vector2 normalized)
        {
            Rect rect = RectTransform.rect;
            return new Vector2(
                rect.x + normalized.x * rect.width,
                rect.y + normalized.y * rect.height);
        }

        private static Vector2 NormalizedToRectLocal(Vector2 normalized, Rect rect)
        {
            return new Vector2(
                rect.x + normalized.x * rect.width,
                rect.y + normalized.y * rect.height);
        }
        /// <summary>
        /// Converts a rect-local position to spline parameter t using the baked LUT
        /// with local refinement. Searches the full spline range.
        /// </summary>
        public float PositionToT(Vector2 localPosition)
        {
            return PositionToT(localPosition, 0f, 1f, out _);
        }

        /// <summary>
        /// Converts a rect-local position to spline parameter t, constrained to [minT, maxT].
        /// Uses baked LUT positions — no bezier re-evaluation.
        /// </summary>
        public float PositionToT(Vector2 localPosition, float minT, float maxT, out float distance)
        {
            EnsureLut();

            if (_spline == null || _spline.Knots.Length < 2)
            {
                distance = float.MaxValue;
                return 0f;
            }

            Rect rect = RectTransform.rect;

            int iMin = Mathf.Clamp(Mathf.FloorToInt(minT * LutSamples), 0, LutSamples);
            int iMax = Mathf.Clamp(Mathf.CeilToInt(maxT * LutSamples), 0, LutSamples);

            int bestIndex = iMin;
            float bestSqrDist = float.PositiveInfinity;

            // --- Coarse LUT search (baked positions, no bezier eval) ---
            for (int i = iMin; i <= iMax; i++)
            {
                Vector2 p = NormalizedToRectLocal(_lutNormalizedPositions[i], rect);
                float sqrDist = (p - localPosition).sqrMagnitude;
                if (sqrDist < bestSqrDist)
                {
                    bestSqrDist = sqrDist;
                    bestIndex = i;
                }
            }

            // --- Local refinement between neighboring LUT samples ---
            int lo = Mathf.Max(bestIndex - 1, iMin);
            int hi = Mathf.Min(bestIndex + 1, iMax);

            Vector2 p0 = NormalizedToRectLocal(_lutNormalizedPositions[lo], rect);
            Vector2 p1 = NormalizedToRectLocal(_lutNormalizedPositions[hi], rect);

            Vector2 dir = p1 - p0;
            float lenSqr = dir.sqrMagnitude;

            float resultT;
            if (lenSqr > 0f)
            {
                float u = Mathf.Clamp01(Vector2.Dot(localPosition - p0, dir) / lenSqr);
                resultT = Mathf.Lerp((float)lo / LutSamples, (float)hi / LutSamples, u);
                Vector2 projected = Vector2.Lerp(p0, p1, u);
                distance = Vector2.Distance(localPosition, projected);
            }
            else
            {
                resultT = (float)bestIndex / LutSamples;
                distance = Mathf.Sqrt(bestSqrDist);
            }

            return Mathf.Clamp(resultT, minT, maxT);
        }

        /// <summary>
        /// Returns the baked rect-local position at parameter t using the LUT (no bezier evaluation).
        /// </summary>
        public Vector2 GetBakedPosition(float t)
        {
            EnsureLut();

            if (_lutNormalizedPositions == null)
                return Vector2.zero;

            t = Mathf.Clamp01(t);
            float fi = t * LutSamples;
            int lo = Mathf.Min((int)fi, LutSamples - 1);
            int hi = lo + 1;
            float frac = fi - lo;

            Vector2 normalized = Vector2.Lerp(_lutNormalizedPositions[lo], _lutNormalizedPositions[hi], frac);
            return NormalizedToRectLocal(normalized);
        }
        
        protected void OnDrawGizmosSelected()
        {
            if (_spline == null)
                return;

            RectTransform rt = RectTransform;

            // Draw rect bounds
            Gizmos.color = Color.yellow;
            Vector3[] rectCorners = new Vector3[4];
            rt.GetWorldCorners(rectCorners);
            for (int i = 0; i < 4; i++)
                Gizmos.DrawLine(rectCorners[i], rectCorners[(i + 1) % 4]);

            const int samples = 64;

            if (_spline == null)
                return;

            Gizmos.color = Color.red;

            Vector2 prevLocal = EvaluatePosition(0f);
            Vector3 prev = rt.TransformPoint(prevLocal);

            for (int i = 1; i <= samples; i++)
            {
                float t = (float)i / samples;
                Vector2 currLocal = EvaluatePosition(t);
                Vector3 curr = rt.TransformPoint(currLocal);

                Gizmos.DrawLine(prev, curr);

                Vector2 tangentLocal = EvaluateTangent(t);
                Vector3 tangent = rt.TransformDirection(new Vector3(tangentLocal.x, tangentLocal.y, 0f)).normalized;
                Gizmos.DrawLine(curr, curr + tangent * 10f);

                prev = curr;
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            _lutDirty = true;
            Changed?.Invoke();
        }
#endif
    }
}
