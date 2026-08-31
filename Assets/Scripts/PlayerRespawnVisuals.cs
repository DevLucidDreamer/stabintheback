using System.Collections.Generic;
using Mirror;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>로컬 사망 카메라와 관찰자에게 보이는 리스폰 보호 점멸.</summary>
public sealed class PlayerRespawnVisuals : MonoBehaviour
{
    private const float BlinkInterval = 0.15f;
    private PlayerHealth health;
    private Transform view;
    private Vector3 savedViewPosition;
    private Quaternion savedViewRotation;
    private Transform corpseRoot;
    private Transform followBone;
    private Vector3 focus;
    private Vector3 viewOffset;
    private bool deathView;
    private readonly RaycastHit[] cameraHits = new RaycastHit[24];
    private readonly List<MaterialSet> materialSets = new List<MaterialSet>();
    private bool? faded;

    private sealed class MaterialSet
    {
        public Renderer renderer;
        public Material[] original;
        public Material[] transparent;
    }

    private void Awake() => health = GetComponent<PlayerHealth>();

    public bool BeginDeathView(GameObject corpse, Vector3 deathPosition, Quaternion deathRotation)
    {
        if (deathView) return false;
        PlayerController player = GetComponent<PlayerController>();
        view = player != null ? player.CameraTransform : null;
        if (view == null) return false;

        savedViewPosition = view.localPosition;
        savedViewRotation = view.localRotation;
        corpseRoot = corpse != null ? corpse.transform : null;
        if (corpse != null)
        {
            Rigidbody body = corpse.GetComponentInChildren<Rigidbody>();
            followBone = body != null ? body.transform : corpseRoot;
        }
        focus = deathPosition + Vector3.up;
        viewOffset = deathRotation * new Vector3(1.1f, 1.7f, -3.2f);
        deathView = true;
        UpdateDeathCamera();
        return true;
    }

    public bool EndDeathView()
    {
        if (!deathView) return false;
        deathView = false;
        if (view != null)
        {
            view.localPosition = savedViewPosition;
            view.localRotation = savedViewRotation;
        }
        view = null;
        corpseRoot = followBone = null;
        return true;
    }

    private void LateUpdate()
    {
        if (deathView) UpdateDeathCamera();
        if (health == null || !health.isClient) return;
        UpdateProtection(health.IsSpawnProtected, health.ProtectionEndsAt - NetworkTime.time);
    }

    private void UpdateDeathCamera()
    {
        if (view == null) return;
        if (followBone != null)
            focus = Vector3.Lerp(focus, followBone.position + Vector3.up * 0.35f, Time.unscaledDeltaTime * 12f);

        float distance = viewOffset.magnitude;
        Vector3 direction = viewOffset / distance;
        int count = Physics.SphereCastNonAlloc(focus, 0.18f, direction, cameraHits, distance,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            Transform hit = cameraHits[i].collider.transform;
            if (hit.IsChildOf(transform) || (corpseRoot != null && hit.IsChildOf(corpseRoot))) continue;
            distance = Mathf.Min(distance, Mathf.Max(0.2f, cameraHits[i].distance - 0.08f));
        }
        view.position = focus + direction * distance;
        view.rotation = Quaternion.LookRotation(focus - view.position, Vector3.up);
    }

    private void UpdateProtection(bool active, double remaining)
    {
        if (!active)
        {
            ClearMaterials();
            return;
        }

        if (materialSets.Count == 0) CacheMaterials();
        bool halfTransparent = ((int)System.Math.Ceiling(remaining / BlinkInterval) & 1) == 0;
        if (faded == halfTransparent) return;
        faded = halfTransparent;
        foreach (MaterialSet set in materialSets)
            if (set.renderer != null)
                set.renderer.sharedMaterials = halfTransparent ? set.transparent : set.original;
    }

    private void CacheMaterials()
    {
        Transform avatar = transform.Find("RemoteAvatar");
        if (avatar == null) return;
        foreach (Renderer renderer in avatar.GetComponentsInChildren<Renderer>(true))
        {
            Material[] original = renderer.sharedMaterials;
            var transparent = new Material[original.Length];
            for (int i = 0; i < original.Length; i++)
                transparent[i] = original[i] != null ? CreateTransparent(original[i]) : null;
            materialSets.Add(new MaterialSet { renderer = renderer, original = original, transparent = transparent });
        }
    }

    private static Material CreateTransparent(Material source)
    {
        var material = new Material(source) { name = source.name + " (Respawn 50%)" };
        // Imported avatars may use either URP Lit or the built-in Standard shader.
        SetFloat(material, "_Surface", 1f);
        SetFloat(material, "_Blend", 0f);
        SetFloat(material, "_Mode", 2f);
        SetFloat(material, "_SrcBlend", (float)BlendMode.SrcAlpha);
        SetFloat(material, "_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        SetFloat(material, "_SrcBlendAlpha", (float)BlendMode.One);
        SetFloat(material, "_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
        SetFloat(material, "_ZWrite", 0f);
        SetFloat(material, "_AlphaClip", 0f);
        material.SetOverrideTag("RenderType", "Transparent");
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.EnableKeyword("_ALPHABLEND_ON");
        material.DisableKeyword("_ALPHATEST_ON");
        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        material.DisableKeyword("_ALPHAMODULATE_ON");
        material.SetShaderPassEnabled("ShadowCaster", false);
        material.renderQueue = (int)RenderQueue.Transparent;
        foreach (string property in new[] { "_BaseColor", "_Color" })
        {
            if (!material.HasProperty(property)) continue;
            Color color = material.GetColor(property);
            color.a *= 0.5f;
            material.SetColor(property, color);
        }
        return material;
    }

    private static void SetFloat(Material material, string property, float value)
    {
        if (material.HasProperty(property)) material.SetFloat(property, value);
    }

    private void ClearMaterials()
    {
        foreach (MaterialSet set in materialSets)
        {
            if (set.renderer != null) set.renderer.sharedMaterials = set.original;
            foreach (Material material in set.transparent)
            {
                if (material == null) continue;
                if (Application.isPlaying) Destroy(material);
                else DestroyImmediate(material);
            }
        }
        materialSets.Clear();
        faded = null;
    }

    private void OnDisable()
    {
        EndDeathView();
        ClearMaterials();
    }
}
