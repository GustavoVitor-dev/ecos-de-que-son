using UnityEngine;

/// Nuvem de moscas em cima de alguma coisa morta.
///
/// Custa quase nada (partículas pretas minúsculas com ruído) e faz o trabalho
/// que nenhum modelo faz sozinho: diz que aquilo está apodrecendo há dias, e
/// diz de longe, antes de o jogador chegar perto o bastante pra ver o corpo.
public static class Moscas
{
    static Material material;
    static AudioClip zumbido;

    /// Põe a nuvem em volta de um alvo. Segue o alvo e não some.
    public static ParticleSystem Em(Transform alvo, float raio = 0.9f, int quantas = 26)
    {
        if (alvo == null) return null;
        var go = new GameObject("Moscas");
        go.transform.SetParent(alvo, false);
        go.transform.localPosition = Vector3.up * 0.35f;

        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(2.5f, 6f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.25f, 0.8f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.045f);
        // ponto ESCURO nao aparece em cima de chao escuro: elas sao cinza
        // sujo, que e como mosca lê contra a lama na luz da lanterna
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.3f, 0.29f, 0.26f, 0.95f), new Color(0.16f, 0.16f, 0.14f, 0.9f));
        main.gravityModifier = 0f;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.maxParticles = quantas * 2;

        var em = ps.emission;
        em.rateOverTime = quantas * 0.4f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = raio;

        // o voo errático: é isto que faz parecer mosca e não poeira
        var ruido = ps.noise;
        ruido.enabled = true;
        ruido.strength = 1.4f;
        ruido.frequency = 1.8f;
        ruido.scrollSpeed = 1.2f;
        ruido.damping = false;

        // puxa de volta pro corpo: elas orbitam em vez de ir embora
        var forca = ps.forceOverLifetime;
        forca.enabled = true;
        forca.space = ParticleSystemSimulationSpace.Local;
        forca.x = new ParticleSystem.MinMaxCurve(-0.35f, 0.35f);
        forca.y = new ParticleSystem.MinMaxCurve(-0.25f, 0.25f);
        forca.z = new ParticleSystem.MinMaxCurve(-0.35f, 0.35f);

        var r = ps.GetComponent<ParticleSystemRenderer>();
        r.renderMode = ParticleSystemRenderMode.Billboard;
        r.sharedMaterial = Material();
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;

        // o zumbido vem junto: mosca se ouve antes de se ver
        Zumbir(go.transform, raio);

        ps.Play();
        return ps;
    }

    /// Zumbido em laço, preso no corpo, que morre com a distância.
    static void Zumbir(Transform onde, float raio)
    {
        if (zumbido == null)
            zumbido = UnityEngine.Resources.Load<AudioClip>("Moscas");
#if UNITY_EDITOR
        if (zumbido == null)
            zumbido = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Game/Audio/Moscas.wav");
#endif
        if (zumbido == null) return;
        var a = onde.gameObject.AddComponent<AudioSource>();
        a.clip = zumbido;
        a.loop = true;
        a.spatialBlend = 1f;
        a.volume = 0.55f;
        a.minDistance = Mathf.Max(0.8f, raio);
        a.maxDistance = 14f;
        a.rolloffMode = AudioRolloffMode.Linear;
        a.dopplerLevel = 0f;
        a.Play();
    }

    static Material Material()
    {
        if (material != null) return material;
        material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
        var t = new Texture2D(8, 8, TextureFormat.RGBA32, false);
        var px = new Color[64];
        for (int y = 0; y < 8; y++)
            for (int x = 0; x < 8; x++)
            {
                float d = Mathf.Sqrt((x - 3.5f) * (x - 3.5f) + (y - 3.5f) * (y - 3.5f)) / 3.5f;
                px[y * 8 + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(1.2f - d * 1.4f));
            }
        t.SetPixels(px); t.Apply();
        material.SetTexture("_BaseMap", t);
        material.SetColor("_BaseColor", Color.white);
        material.SetFloat("_Surface", 1f);
        material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        material.SetFloat("_ZWrite", 0f);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        return material;
    }
}
