using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class HighlightingService : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("The material with the Inverted Hull / Fresnel shader.")]
    [SerializeField] private Material outlineMaterial;

    private readonly Dictionary<GameObject, List<Renderer>> _activeOutlines = new Dictionary<GameObject, List<Renderer>>();
    private int _colorPropID;

    void Awake()
    {
        // make sure this string exactly matches the variable exposed in the shader graph 
        _colorPropID = Shader.PropertyToID("_OutlineColor");
    }

    public void EnableHighlight(GameObject target, Color? color = null)
    {
        if (target == null) return;

        if (!_activeOutlines.ContainsKey(target))
        {
            CreateHullsForTarget(target);
        }

        if (_activeOutlines.TryGetValue(target, out List<Renderer> hulls))
        {
            foreach (var hull in hulls)
            {
                if (hull == null) continue;
                hull.enabled = true;

                if (color.HasValue)
                {
                    MaterialPropertyBlock mpb = new MaterialPropertyBlock();
                    hull.GetPropertyBlock(mpb);
                    mpb.SetColor(_colorPropID, color.Value);
                    hull.SetPropertyBlock(mpb);
                }
                else
                {
                    hull.SetPropertyBlock(null);
                }
            }
        }
    }

    public void DisableHighlight(GameObject target)
    {
        if (target == null || !_activeOutlines.TryGetValue(target, out List<Renderer> hulls)) return;

        foreach (var hull in hulls)
        {
            if (hull != null) hull.enabled = false;
        }
    }

    private void CreateHullsForTarget(GameObject target)
    {
        List<Renderer> newHulls = new List<Renderer>();
        
        Renderer[] childRenderers = target.GetComponentsInChildren<Renderer>(true);

        Debug.Log($"[Highlighting] Initializing {target.name}. Found {childRenderers.Length} renderers.");

        foreach (var sourceRenderer in childRenderers)
        {
            if (sourceRenderer.gameObject.name.Contains("_OutlineHull")) continue;
            
            Mesh sharedMesh = null;
            if (sourceRenderer is MeshRenderer)
                sharedMesh = sourceRenderer.GetComponent<MeshFilter>()?.sharedMesh;
            else if (sourceRenderer is SkinnedMeshRenderer smr)
                sharedMesh = smr.sharedMesh;

            if (sharedMesh == null) continue;

            // we duplicate the mesh and push the vertices outward to create a fake outline
            GameObject hullObj = new GameObject("_OutlineHull_" + sourceRenderer.gameObject.name);
            hullObj.transform.SetParent(sourceRenderer.transform, false);
            
            hullObj.transform.localPosition = Vector3.zero;
            hullObj.transform.localRotation = Quaternion.identity;
            hullObj.transform.localScale = Vector3.one;

            MeshFilter hullFilter = hullObj.AddComponent<MeshFilter>();
            MeshRenderer hullRenderer = hullObj.AddComponent<MeshRenderer>();

            hullFilter.sharedMesh = sharedMesh;

            // some of our blender models have multiple materials on one mesh. 
            // if we don't match the material slot count on the duplicate hull, unity freaks out and the outline breaks.
            int materialCount = sourceRenderer.sharedMaterials.Length;
            Material[] hullMaterials = new Material[materialCount];

            for (int i = 0; i < materialCount; i++)
            {
                hullMaterials[i] = outlineMaterial;
            }

            hullRenderer.sharedMaterials = hullMaterials;

            hullRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            hullRenderer.receiveShadows = false;
            hullRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;

            newHulls.Add(hullRenderer);
            Debug.Log($"[Highlighting] Hull created for {sourceRenderer.name} with {materialCount} material slots.");
        }

        _activeOutlines[target] = newHulls;
    }
}