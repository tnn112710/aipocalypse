using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MB13.Aipocalypse.Core.EnvironmentSystem
{
    // Swaps materials on the renderers under an object for runtime copies (one per source material), so shared and vendor
    // material assets are never written. The owner destroys the copies with Destroy in its OnDestroy.
    public static class RuntimeMaterials
    {
        public static void CopyUnder(Component root, Predicate<Material> wanted, Func<Material, Material> makeCopy, List<Material> copies)
        {
            var made = new Dictionary<Material, Material>();
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                Material[] materials = renderer.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < materials.Length; i++)
                {
                    Material source = materials[i];
                    if (source == null || !wanted(source))
                        continue;
                    if (!made.TryGetValue(source, out Material copy))
                    {
                        copy = makeCopy(source);
                        made.Add(source, copy);
                        copies.Add(copy);
                    }
                    materials[i] = copy;
                    changed = true;
                }
                if (changed)
                    renderer.sharedMaterials = materials;
            }
        }

        public static void Destroy(List<Material> copies)
        {
            foreach (Material copy in copies)
                Object.Destroy(copy);
            copies.Clear();
        }
    }
}
