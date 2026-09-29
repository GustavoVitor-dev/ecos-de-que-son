using System.Collections.Generic;
using UnityEngine;

/// O Miller some quando entra na moita.
///
/// Não é só o guarda deixar de ver (isso é o PlayerConcealment): é a tela
/// mostrar que ele entrou. O corpo apaga até virar um vulto atrás da folha, e
/// volta a aparecer quando ele sai ou quando se mexe demais lá dentro — quem
/// corre dentro do mato balança tudo e é visto.
///
/// O apagar é feito com uma CÓPIA transparente do material de cada malha (o
/// original é opaco e não tem alfa), trocada só enquanto ele está escondido.
[RequireComponent(typeof(PlayerConcealment))]
public class JogadorNaMoita : MonoBehaviour
{
    [Tooltip("Alfa mínimo: 0 some de vez, 0.15 deixa um vulto.")]
    [Range(0f, 1f)] public float vulto = 0.14f;
    [Tooltip("Velocidade do apagar/aparecer.")]
    public float velocidade = 3.5f;

    PlayerConcealment oculto;
    readonly List<Renderer> malhas = new List<Renderer>();
    readonly Dictionary<Renderer, Material[]> originais = new Dictionary<Renderer, Material[]>();
    readonly Dictionary<Renderer, Material[]> copias = new Dictionary<Renderer, Material[]>();
    MaterialPropertyBlock bloco;
    float alfa = 1f;
    bool trocado;

    void Start()
    {
        oculto = GetComponent<PlayerConcealment>();
        bloco = new MaterialPropertyBlock();
        foreach (var r in GetComponentsInChildren<Renderer>(true))
        {
            if (r is ParticleSystemRenderer) continue;
            if (r.sharedMaterials.Length == 0 || r.sharedMaterials[0] == null) continue;
            malhas.Add(r);
            originais[r] = r.sharedMaterials;
            var c = new Material[r.sharedMaterials.Length];
            for (int i = 0; i < c.Length; i++) c[i] = Transparente(r.sharedMaterials[i]);
            copias[r] = c;
        }
    }

    void LateUpdate()
    {
        // escondido de verdade = dentro da moita E se comportando (o
        // PlayerConcealment já devolve 1 quando corre)
        float alvo = oculto != null && oculto.InConcealment
            ? Mathf.Lerp(vulto, 1f, Mathf.InverseLerp(0.35f, 1f, oculto.VisibilityMultiplier))
            : 1f;
        alfa = Mathf.MoveTowards(alfa, alvo, Time.deltaTime * velocidade);

        bool queroTrocar = alfa < 0.99f;
        if (queroTrocar != trocado)
        {
            foreach (var r in malhas)
                if (r != null) r.sharedMaterials = queroTrocar ? copias[r] : originais[r];
            trocado = queroTrocar;
        }
        if (!trocado) return;

        foreach (var r in malhas)
        {
            if (r == null) continue;
            r.GetPropertyBlock(bloco);
            var c = r.sharedMaterials[0] != null && r.sharedMaterials[0].HasProperty("_BaseColor")
                ? r.sharedMaterials[0].GetColor("_BaseColor") : Color.white;
            c.a = alfa;
            bloco.SetColor("_BaseColor", c);
            r.SetPropertyBlock(bloco);
        }
    }

    static Material Transparente(Material m)
    {
        var c = new Material(m);
        c.SetFloat("_Surface", 1f);
        c.SetFloat("_Blend", 0f);
        c.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        c.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        c.SetFloat("_ZWrite", 0f);
        c.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        c.DisableKeyword("_ALPHATEST_ON");
        c.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        return c;
    }
}
