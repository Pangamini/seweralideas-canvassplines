using System;
using UnityEngine;

namespace SeweralIdeas.CanvasSplines
{
    [Serializable]
    public struct BezierKnot
    {
        /// <summary>Position in normalized (0,0)-(1,1) rect space.</summary>
        public Vector2 position;

        /// <summary>Incoming tangent, relative to position.</summary>
        public Vector2 tangentIn;

        /// <summary>Outgoing tangent, relative to position.</summary>
        public Vector2 tangentOut;
    }
}
