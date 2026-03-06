using UnityEditor;
using UnityEngine;

namespace SeweralIdeas.CanvasSplines.Editor
{
    [CustomEditor(typeof(RectSpline))]
    public class RectSplineEditor : UnityEditor.Editor
    {
        private SerializedProperty _splineProp;
        private SerializedProperty _knotsProp;

        private const float HandleSize = 0.05f;
        private const float TangentHandleSize = 0.035f;

        private static readonly Color PositionHandleColor = Color.white;
        private static readonly Color TangentInColor = new(0.2f, 0.6f, 1f);
        private static readonly Color TangentOutColor = new(1f, 0.4f, 0.2f);
        private static readonly Color TangentLineColor = new(1f, 1f, 1f, 0.4f);

        private void OnEnable()
        {
            _splineProp = serializedObject.FindProperty("_spline");
            _knotsProp = _splineProp.FindPropertyRelative("_knots");
        }

        private void OnSceneGUI()
        {
            var rectSpline = (RectSpline)target;
            RectTransform rt = rectSpline.RectTransform;
            if (rt == null)
                return;

            serializedObject.Update();
            Rect rect = rt.rect;

            int knotCount = _knotsProp.arraySize;
            for (int i = 0; i < knotCount; i++)
            {
                SerializedProperty knotProp = _knotsProp.GetArrayElementAtIndex(i);
                SerializedProperty positionProp = knotProp.FindPropertyRelative("position");
                SerializedProperty tangentInProp = knotProp.FindPropertyRelative("tangentIn");
                SerializedProperty tangentOutProp = knotProp.FindPropertyRelative("tangentOut");

                Vector2 normalizedPos = positionProp.vector2Value;
                Vector2 normalizedTangentIn = tangentInProp.vector2Value;
                Vector2 normalizedTangentOut = tangentOutProp.vector2Value;

                // Convert normalized to rect-local
                Vector2 localPos = NormalizedToRectLocal(normalizedPos, rect);
                Vector2 localTangentInPos = NormalizedToRectLocal(normalizedPos + normalizedTangentIn, rect);
                Vector2 localTangentOutPos = NormalizedToRectLocal(normalizedPos + normalizedTangentOut, rect);

                // Convert to world space
                Vector3 worldPos = rt.TransformPoint(localPos);
                Vector3 worldTangentIn = rt.TransformPoint(localTangentInPos);
                Vector3 worldTangentOut = rt.TransformPoint(localTangentOutPos);

                float screenScale = HandleUtility.GetHandleSize(worldPos);

                // Draw tangent lines
                Handles.color = TangentLineColor;
                Handles.DrawLine(worldPos, worldTangentIn);
                Handles.DrawLine(worldPos, worldTangentOut);

                // Position handle
                Handles.color = PositionHandleColor;
                EditorGUI.BeginChangeCheck();
                Vector3 newWorldPos = Handles.FreeMoveHandle(worldPos, screenScale * HandleSize, Vector3.zero, Handles.DotHandleCap);
                if (EditorGUI.EndChangeCheck())
                {
                    Vector2 newLocalPos = rt.InverseTransformPoint(newWorldPos);
                    Vector2 newNormalized = RectLocalToNormalized(newLocalPos, rect);
                    positionProp.vector2Value = newNormalized;
                }

                // Tangent In handle
                Handles.color = TangentInColor;
                EditorGUI.BeginChangeCheck();
                Vector3 newWorldTangentIn = Handles.FreeMoveHandle(worldTangentIn, screenScale * TangentHandleSize, Vector3.zero, Handles.DotHandleCap);
                if (EditorGUI.EndChangeCheck())
                {
                    Vector2 newLocalTangentIn = rt.InverseTransformPoint(newWorldTangentIn);
                    Vector2 newNormalizedTangentIn = RectLocalToNormalized(newLocalTangentIn, rect) - positionProp.vector2Value;
                    tangentInProp.vector2Value = newNormalizedTangentIn;
                }

                // Tangent Out handle
                Handles.color = TangentOutColor;
                EditorGUI.BeginChangeCheck();
                Vector3 newWorldTangentOut = Handles.FreeMoveHandle(worldTangentOut, screenScale * TangentHandleSize, Vector3.zero, Handles.DotHandleCap);
                if (EditorGUI.EndChangeCheck())
                {
                    Vector2 newLocalTangentOut = rt.InverseTransformPoint(newWorldTangentOut);
                    Vector2 newNormalizedTangentOut = RectLocalToNormalized(newLocalTangentOut, rect) - positionProp.vector2Value;
                    tangentOutProp.vector2Value = newNormalizedTangentOut;
                }

                // Label
                Handles.color = Color.white;
                Handles.Label(worldPos + Vector3.up * screenScale * 0.1f, i.ToString());
            }

            if (serializedObject.ApplyModifiedProperties())
            {
                // Trigger LUT rebuild and Changed event
                EditorUtility.SetDirty(target);
            }
        }

        private static Vector2 NormalizedToRectLocal(Vector2 normalized, Rect rect)
        {
            return new Vector2(
                rect.x + normalized.x * rect.width,
                rect.y + normalized.y * rect.height);
        }

        private static Vector2 RectLocalToNormalized(Vector2 localPos, Rect rect)
        {
            return new Vector2(
                (localPos.x - rect.x) / rect.width,
                (localPos.y - rect.y) / rect.height);
        }
    }
}
