using System.Collections.Generic;
using UnityEngine;

namespace Neanderthal
{
    /// <summary>Destroys runtime-created meshes/textures/materials together with their GameObject.</summary>
    public class OwnedAssets : MonoBehaviour
    {
        public readonly List<Object> Assets = new List<Object>();

        void OnDestroy()
        {
            foreach (var a in Assets)
                if (a != null) Destroy(a);
        }
    }
}
