using UnityEngine;

namespace SamusMod.Misc
{
    // Remembers the model's original scale so the configured scale is never applied on top of itself.
    public class SamusModelScale : MonoBehaviour
    {
        public Vector3 baseScale = Vector3.one;
    }
}
