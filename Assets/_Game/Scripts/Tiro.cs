using System.Collections;
using UnityEngine;

/// O disparo: clarão na boca do cano, traçante curto, estouro e a poeira de
/// onde a bala bate.
///
/// Num jogo de cima, à noite, o tiro precisa dizer três coisas em meio segundo:
/// DE ONDE veio, PRA ONDE foi e se PEGOU em você. Por isso o clarão fica no
/// guarda (denuncia a posição dele), o traçante liga os dois pontos e o
/// impacto levanta pó no lugar errado quando ele erra — assim errar também
/// informa.
public static class Tiro
{
    static Material matTracante;
    static AudioClip estouro, zunido;

    /// Um tiro de `de` pra `para`. `acertou` muda só o efeito de chegada.
    public static void Disparar(Vector3 de, Vector3 para, bool acertou, Transform dono = null)
    {
        var go = new GameObject("Tiro");
        go.transform.position = de;
        var comp = go.AddComponent<TiroVisual>();
        comp.de = de; comp.para = para; comp.acertou = acertou;
        comp.material = Material();
        Som(de, dono);
        Object.Destroy(go, 1.2f);
    }

    /// O barulho chama todo mundo: quem atira entrega a si mesmo e a você.
    public static void Barulho(Vector3 onde, float raio = 26f)
        => Noise.Emit(onde, raio, NoiseSource.Outro);

    static Material Material()
    {
        if (matTracante != null) return matTracante;
        matTracante = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        matTracante.SetColor("_BaseColor", new Color(1f, 0.85f, 0.5f, 1f));
        matTracante.SetFloat("_Surface", 1f);
        matTracante.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        matTracante.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);   // aditivo: brilha
        matTracante.SetFloat("_ZWrite", 0f);
        matTracante.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        matTracante.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        return matTracante;
    }

    static void Som(Vector3 onde, Transform dono)
    {
        if (estouro == null) estouro = Estouro();
        var go = new GameObject("Som_Tiro");
        go.transform.position = onde;
        var a = go.AddComponent<AudioSource>();
        a.clip = estouro;
        a.spatialBlend = 1f;
        a.volume = 0.7f;
        a.minDistance = 4f;
        a.maxDistance = 70f;
        a.rolloffMode = AudioRolloffMode.Linear;
        a.pitch = Random.Range(0.92f, 1.08f);
        a.Play();
        Object.Destroy(go, 2.5f);
    }

    /// Estouro de fuzil sintetizado: estalo seco, corpo grave e a cauda que a
    /// mata devolve.
    static AudioClip Estouro()
    {
        const int taxa = 22050;
        int n = (int)(taxa * 0.9f);
        var d = new float[n];
        var rnd = new System.Random(77);
        float lp = 0f, lp2 = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)taxa;
            float ruido = (float)(rnd.NextDouble() * 2 - 1);
            // estalo: ataque instantâneo e queda muito rápida
            float estalo = ruido * Mathf.Exp(-t * 90f);
            // corpo: grave curto
            lp = Mathf.Lerp(lp, ruido, 0.08f);
            float corpo = lp * Mathf.Exp(-t * 26f) * 1.4f;
            // cauda: eco espalhado da mata
            lp2 = Mathf.Lerp(lp2, ruido, 0.015f);
            float cauda = lp2 * Mathf.Exp(-t * 3.2f) * 0.5f * (0.6f + 0.4f * Mathf.Sin(t * 14f));
            d[i] = Mathf.Clamp(estalo * 0.9f + corpo + cauda, -1f, 1f) * 0.9f;
        }
        var c = AudioClip.Create("tiro", n, 1, taxa, false);
        c.SetData(d, 0);
        return c;
    }
}

/// Desenha um tiro por um quadro e meio: traçante, clarão e impacto.
public class TiroVisual : MonoBehaviour
{
    public Vector3 de, para;
    public bool acertou;
    public Material material;

    IEnumerator Start()
    {
        // clarão na boca do cano
        var luz = new GameObject("Clarao").AddComponent<Light>();
        luz.transform.position = de;
        luz.type = LightType.Point;
        luz.color = new Color(1f, 0.82f, 0.5f);
        luz.range = 9f;
        luz.intensity = 14f;
        luz.shadows = LightShadows.None;

        // traçante: um fio fino entre os dois pontos
        var linha = gameObject.AddComponent<LineRenderer>();
        linha.material = material;
        linha.positionCount = 2;
        linha.SetPosition(0, de);
        linha.SetPosition(1, para);
        linha.startWidth = 0.035f;
        linha.endWidth = 0.012f;
        linha.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        linha.useWorldSpace = true;

        Tiro.Barulho(de);

        // impacto: pó onde bateu (quando erra, o pó é a informação de que errou)
        if (!acertou) Poeira(para);

        float t = 0f;
        while (t < 0.09f)
        {
            t += Time.deltaTime;
            float k = 1f - t / 0.09f;
            luz.intensity = 14f * k;
            linha.startWidth = 0.035f * k;
            linha.endWidth = 0.012f * k;
            yield return null;
        }
        Destroy(luz.gameObject);
        Destroy(linha);
    }

    void Poeira(Vector3 onde)
    {
        var go = new GameObject("Poeira");
        go.transform.position = onde;
        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.duration = 0.4f;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.6f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 2.4f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.13f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.32f, 0.28f, 0.22f, 0.75f), new Color(0.18f, 0.16f, 0.13f, 0.5f));
        main.gravityModifier = 0.7f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        var em = ps.emission;
        em.rateOverTime = 0f;
        em.SetBursts(new[] { new ParticleSystem.Burst(0f, 8, 16) });
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Hemisphere;
        shape.radius = 0.06f;
        var r = ps.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = material;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        ps.Play();
        Destroy(go, 1.5f);
    }
}
