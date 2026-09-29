using System.Collections;
using UnityEngine;

/// Sangue: o jato do corte, a poça que cresce embaixo do corpo e os respingos
/// que sobram na lama.
///
/// Tudo é feito em código (partícula com textura gerada, poça com mancha
/// gerada) porque nenhum pacote do projeto tem VFX de sangue — e porque assim
/// a cor, o tamanho e o tempo ficam presos ao que a cena precisa: escuro,
/// pouco brilho, crescimento lento. Sangue vermelho-vivo e brilhante viraria
/// desenho; aqui ele é quase preto, do jeito que fica na chuva à noite.
public static class Sangue
{
    static Material matJato, matPoca;
    static Texture2D texGota, texPoca;

    // ---------------------------------------------------------------- jato

    /// Jato curto e forte, na direção da ferida. Volta o sistema pra quem
    /// quiser continuar sangrando (o corpo caído, a moita).
    public static ParticleSystem Jato(Vector3 pos, Vector3 dir, float forca = 1f, float duracao = 1.2f, Transform pai = null)
    {
        var go = new GameObject("Sangue_Jato");
        if (pai != null) go.transform.SetParent(pai, true);
        go.transform.position = pos;
        go.transform.rotation = Quaternion.LookRotation(dir.sqrMagnitude > 0.001f ? dir : Vector3.forward, Vector3.up);

        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.duration = duracao;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.6f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.2f * forca, 3.8f * forca);
        // gota de 1,5 cm some numa camera que ve 12 m de altura: aqui elas tem
        // 6 a 18 cm. Nao e realismo de gota, e legibilidade — sangue que nao
        // aparece na tela nao existe pro jogador.
        main.startSize = new ParticleSystem.MinMaxCurve(0.06f * (0.6f + forca), 0.18f * (0.6f + forca));
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.3f, 0.035f, 0.025f), new Color(0.16f, 0.018f, 0.014f));
        main.gravityModifier = 1.3f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 300;
        main.playOnAwake = true;

        var em = ps.emission;
        em.rateOverTime = 55f * forca;
        em.SetBursts(new[]
        {
            new ParticleSystem.Burst(0f, (short)(45 * forca), (short)(70 * forca)),
            new ParticleSystem.Burst(0.18f, (short)(12 * forca), (short)(26 * forca)),
        });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 22f;
        shape.radius = 0.03f;

        // as tres curvas TEM que estar no mesmo modo, senao o Unity recusa
        // ("Particle Velocity curves must all be in the same mode")
        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.World;
        vel.x = new ParticleSystem.MinMaxCurve(-0.4f, 0.4f);
        vel.y = new ParticleSystem.MinMaxCurve(-0.1f, 0.1f);
        vel.z = new ParticleSystem.MinMaxCurve(-0.4f, 0.4f);

        var tam = ps.sizeOverLifetime;
        tam.enabled = true;
        tam.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.7f, 0.85f), new Keyframe(1f, 0.2f)));

        var col = ps.collision;
        col.enabled = true;
        col.type = ParticleSystemCollisionType.World;
        col.mode = ParticleSystemCollisionMode.Collision3D;
        col.dampen = 0.8f;
        col.bounce = 0.05f;
        col.lifetimeLoss = 0.6f;

        var r = ps.GetComponent<ParticleSystemRenderer>();
        // esticada na direcao do movimento: respingo e risco, nao bolinha
        r.renderMode = ParticleSystemRenderMode.Stretch;
        r.velocityScale = 0.12f;
        r.lengthScale = 1.6f;
        r.sharedMaterial = MatJato();
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;

        ps.Play();
        Object.Destroy(go, duracao + 2.5f);
        return ps;
    }

    // ---------------------------------------------------------------- poça

    /// Mancha no chão que cresce devagar embaixo do corpo. Segue o terreno
    /// (fica rente, um dedo acima) e escurece enquanto seca.
    public static void Poca(Vector3 centro, float raioFinal = 0.75f, float tempo = 6f, Transform pai = null)
    {
        var alvo = Chao(centro);
        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = "Sangue_Poca";
        Object.Destroy(go.GetComponent<Collider>());
        if (pai != null) go.transform.SetParent(pai, true);
        go.transform.position = alvo + Vector3.up * 0.02f;
        go.transform.rotation = Quaternion.Euler(90f, Random.Range(0f, 360f), 0f);
        go.transform.localScale = Vector3.one * 0.05f;
        var r = go.GetComponent<MeshRenderer>();
        r.sharedMaterial = MatPoca();
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;

        var c = go.AddComponent<PocaQueCresce>();
        c.raioFinal = raioFinal;
        c.tempo = tempo;
    }

    /// Pinga de dentro de alguma coisa (a moita) pro chão, devagar, por um tempo.
    public static IEnumerator Pingar(Vector3 pos, float segundos, float intervalo = 0.35f)
    {
        float t = 0f;
        while (t < segundos)
        {
            Jato(pos + Random.insideUnitSphere * 0.12f, Vector3.down + Random.insideUnitSphere * 0.3f, 0.25f, 0.15f);
            float espera = intervalo * Random.Range(0.6f, 1.6f);
            t += espera;
            yield return new WaitForSeconds(espera);
        }
    }

    static Vector3 Chao(Vector3 p)
    {
        if (Physics.Raycast(p + Vector3.up * 1.5f, Vector3.down, out var hit, 6f, ~0, QueryTriggerInteraction.Ignore))
            return hit.point;
        var t = Terrain.activeTerrain;
        if (t != null) return new Vector3(p.x, t.SampleHeight(p) + t.transform.position.y, p.z);
        return p;
    }

    // ---------------------------------------------------------------- material

    static Material MatJato()
    {
        if (matJato != null) return matJato;
        matJato = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
        matJato.SetTexture("_BaseMap", TexGota());
        matJato.SetColor("_BaseColor", Color.white);
        matJato.SetFloat("_Surface", 1f);
        matJato.SetFloat("_Blend", 0f);
        matJato.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        matJato.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        matJato.SetFloat("_ZWrite", 0f);
        matJato.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        matJato.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        return matJato;
    }

    /// Unlit de proposito: a cena e noite fechada e um material iluminado
    /// deixava a poca preta — sangue que ninguem ve nao conta a historia.
    /// Aqui ele tem brilho proprio baixo, do jeito que poca molhada aparece.
    static Material MatPoca()
    {
        if (matPoca != null) return matPoca;
        matPoca = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        matPoca.SetTexture("_BaseMap", TexPoca());
        matPoca.SetColor("_BaseColor", new Color(1f, 1f, 1f, 1f));
        matPoca.SetFloat("_Surface", 1f);
        matPoca.SetFloat("_Blend", 0f);
        matPoca.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        matPoca.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        matPoca.SetFloat("_ZWrite", 0f);
        matPoca.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        matPoca.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent + 50;
        return matPoca;
    }

    static Texture2D TexGota()
    {
        if (texGota != null) return texGota;
        const int n = 32;
        texGota = new Texture2D(n, n, TextureFormat.RGBA32, false);
        var px = new Color[n * n];
        float c = (n - 1) * 0.5f;
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;
                float a = Mathf.Clamp01(1f - d * d);
                px[y * n + x] = new Color(1f, 1f, 1f, a);
            }
        texGota.SetPixels(px); texGota.Apply();
        return texGota;
    }

    /// Mancha irregular: disco deformado por ruído, com a borda rasgada e um
    /// miolo mais escuro. Nada de círculo perfeito.
    static Texture2D TexPoca()
    {
        if (texPoca != null) return texPoca;
        const int n = 256;
        texPoca = new Texture2D(n, n, TextureFormat.RGBA32, false);
        var px = new Color[n * n];
        float c = (n - 1) * 0.5f;
        float semente = Random.value * 50f;
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = (x - c) / c, dy = (y - c) / c;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float ang = Mathf.Atan2(dy, dx);
                // borda ondulada: o raio muda com o ângulo
                float borda = 0.62f + 0.16f * Mathf.PerlinNoise(semente + Mathf.Cos(ang) * 1.6f + 3f, Mathf.Sin(ang) * 1.6f)
                                    + 0.07f * Mathf.PerlinNoise(semente + Mathf.Cos(ang) * 5f, Mathf.Sin(ang) * 5f);
                float a = Mathf.Clamp01((borda - d) * 7f);
                float mancha = Mathf.PerlinNoise(x * 0.05f + semente, y * 0.05f);
                a *= 0.75f + 0.25f * mancha;
                // respingo solto em volta
                if (a < 0.02f && d < borda + 0.25f && Mathf.PerlinNoise(x * 0.35f, y * 0.35f) > 0.82f) a = 0.55f;
                // sangue na lama a noite e quase marrom: vermelho vivo vira tinta
                float escuro = 0.1f + 0.055f * mancha - 0.035f * Mathf.Clamp01(1f - d * 1.6f);
                float brilho = Mathf.Clamp01((d - borda * 0.75f) * 3f) * 0.1f;
                px[y * n + x] = new Color(escuro * 1.5f + brilho, escuro * 0.34f + brilho * 0.9f, escuro * 0.3f + brilho * 0.8f, a * 0.88f);
            }
        texPoca.SetPixels(px); texPoca.Apply();
        return texPoca;
    }
}

/// A poça cresce e escurece. Componente separado porque Sangue é estático.
public class PocaQueCresce : MonoBehaviour
{
    public float raioFinal = 0.75f;
    public float tempo = 6f;
    float t;
    Material meu;

    void Start()
    {
        var r = GetComponent<Renderer>();
        meu = r.material;                       // cópia: cada poça escurece sozinha
    }

    void Update()
    {
        t += Time.deltaTime;
        float k = Mathf.Clamp01(t / tempo);
        float raio = raioFinal * (1f - Mathf.Exp(-3.2f * k));   // rápido no começo, depois arrasta
        transform.localScale = Vector3.one * raio * 2f;
        if (meu != null)
        {
            var c = meu.GetColor("_BaseColor");
            c.a = Mathf.Lerp(1f, 0.82f, k);
            meu.SetColor("_BaseColor", c);
            // escurece enquanto seca (o Unlit nao tem brilho pra perder)
            float seca = Mathf.Lerp(1f, 0.72f, k);
            meu.SetColor("_BaseColor", new Color(seca, seca, seca, c.a));
        }
        if (k >= 1f) enabled = false;
    }
}
