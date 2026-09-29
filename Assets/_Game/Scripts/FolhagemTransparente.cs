using System.Collections.Generic;
using UnityEngine;

/// Folha que fica entre a câmera e o jogador desbota, pra ele aparecer no meio
/// do mato. Quando ele sai dali, a folha volta ao normal.
///
/// Toda a folhagem do projeto é URP/Lit com recorte de alfa (fila 2450). Pra
/// desbotar, a planta troca o material por uma cópia transparente do mesmo
/// material (mesma textura, recorte mantido) e o alfa desce por
/// MaterialPropertyBlock. De volta a 1, o material original volta: fora do
/// raio nada fica transparente, então não há custo nem briga de ordenação.
///
/// A copa (camada Copa) só entra onde a câmera de cima ainda a desenha.
[DisallowMultipleComponent]
public class FolhagemTransparente : MonoBehaviour
{
    [Tooltip("Raio em volta do jogador (e da linha até a câmera) em que a folha desbota.")]
    public float raio = 2.3f;
    [Tooltip("Alfa da folha desbotada.")]
    [Range(0f, 1f)] public float alfaMinimo = 0.22f;
    [Tooltip("Quanto do alfa muda por segundo.")]
    public float velocidade = 5f;
    [Tooltip("Planta mais baixa que isso (acima do pé) não cobre o jogador e não desbota.")]
    public float alturaMinima = 0.45f;
    [Tooltip("Copa de árvore (camada Copa) abre num raio maior e some quase toda: " +
             "ela fica acima do jogador e cobre muito mais tela que um arbusto.")]
    public float raioCopa = 5.5f;
    [Range(0f, 1f)] public float alfaCopa = 0.08f;
    [Tooltip("Alfa mínimo de quem TEM COLISOR. Planta que some e continua barrando é o " +
             "pior tipo de parede invisível: você bate no que não está vendo. Quem " +
             "bloqueia desbota só até aqui e segue legível na tela.")]
    [Range(0f, 1f)] public float alfaDeQuemBloqueia = 0.55f;

    const float Celula = 4f;
    static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

    class Planta
    {
        public Renderer r;
        public Material[] originais;
        public Material[] desbotados;
        public Color[] cores;
        public float alfa = 1f;
        public bool trocada;
        public bool copa;
        public bool bloqueia;
    }

    Transform jogador;
    CharacterController cc;
    Camera cam;
    readonly Dictionary<long, List<Planta>> grade = new Dictionary<long, List<Planta>>();
    readonly List<Planta> ativas = new List<Planta>();
    readonly HashSet<Planta> alvo = new HashSet<Planta>();
    // Por instância, não estático: com o domínio sem recarregar entre um Play e
    // outro, um cache estático guardava cópias já destruídas e a folha trocava
    // por material nulo — ficava rosa.
    readonly Dictionary<Material, Material> copias = new Dictionary<Material, Material>();
    MaterialPropertyBlock bloco;

    void Start()
    {
        var sol = FindFirstObjectByType<SoldierController>();
        if (sol == null) { enabled = false; return; }
        jogador = sol.transform;
        cc = sol.GetComponent<CharacterController>();
        var rig = FindFirstObjectByType<CameraRig>();
        cam = rig != null ? rig.topDownCamera : Camera.main;
        bloco = new MaterialPropertyBlock();

        // a copa só entra se a câmera de cima desenha ela (no Acampamento desenha)
        int copa = LayerMask.NameToLayer("Copa");
        bool copaVisivel = copa >= 0 && cam != null && (cam.cullingMask & (1 << copa)) != 0;
        foreach (var r in FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
        {
            if ((r.gameObject.layer == copa && !copaVisivel) || r.transform.IsChildOf(jogador)) continue;
            // moita NÃO desbota: ela é a camuflagem. Se o mato onde o jogador
            // está fica transparente, o esconderijo deixa de esconder na tela.
            if (r.GetComponentInParent<Moita>() != null) continue;
            var mats = r.sharedMaterials;
            // arbusto do Poly Haven mistura folha (recorte, 2450) e galho (opaco)
            // na mesma malha: basta ter uma folha, e o galho desbota junto
            bool folha = false, valido = mats.Length > 0;
            foreach (var m in mats)
            {
                if (m == null || !m.HasProperty(BaseColor) || m.renderQueue > 2500) { valido = false; break; }
                if (m.renderQueue == 2450) folha = true;
            }
            if (!folha || !valido) continue;

            // tem colisor que barra, nele ou em algum pai? (tronco de árvore,
            // caixote, tora) — 419 árvores desbotavam até sumir com a cápsula
            // de colisão intacta, e o jogador ficava preso no nada
            bool barra = false;
            for (var t = r.transform; t != null && !barra; t = t.parent)
                foreach (var col in t.GetComponents<Collider>())
                    if (!col.isTrigger) { barra = true; break; }

            var p = new Planta { r = r, copa = r.gameObject.layer == copa, bloqueia = barra, originais = mats, desbotados = new Material[mats.Length], cores = new Color[mats.Length] };
            for (int i = 0; i < mats.Length; i++)
            {
                p.desbotados[i] = Copia(mats[i]);
                p.cores[i] = mats[i].GetColor(BaseColor);
            }

            var b = r.bounds;
            int x0 = Mathf.FloorToInt(b.min.x / Celula), x1 = Mathf.FloorToInt(b.max.x / Celula);
            int z0 = Mathf.FloorToInt(b.min.z / Celula), z1 = Mathf.FloorToInt(b.max.z / Celula);
            for (int x = x0; x <= x1; x++)
                for (int z = z0; z <= z1; z++)
                {
                    long k = Chave(x, z);
                    if (!grade.TryGetValue(k, out var lista)) grade[k] = lista = new List<Planta>();
                    lista.Add(p);
                }
        }
    }

    static long Chave(int x, int z) => ((long)x << 32) ^ (uint)z;

    Material Copia(Material m)
    {
        if (copias.TryGetValue(m, out var c) && c != null) return c;
        c = new Material(m) { name = m.name + " (desbotada)" };
        c.SetFloat("_Surface", 1f);
        c.SetFloat("_Blend", 0f);
        c.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        c.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        c.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
        c.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        c.SetFloat("_ZWrite", 0f);
        c.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        c.SetOverrideTag("RenderType", "Transparent");
        c.renderQueue = 3000;
        copias[m] = c;
        return c;
    }

    void LateUpdate()
    {
        if (jogador == null) return;

        float pe = cc != null ? jogador.position.y + cc.center.y - cc.height * 0.5f : jogador.position.y;
        // numa cutscene quem tem que aparecer é o que a CÂMERA enquadra: a
        // folha abria em volta do jogador, fora do quadro, e o plano ficava
        // atrás de uma parede de mato
        Vector3 a = CameraRig.Cena ? new Vector3(CameraRig.Foco.x, jogador.position.y, CameraRig.Foco.z) : jogador.position;

        // a folha que cobre o jogador na tela está entre ele e a câmera
        Vector3 paraCam = cam != null ? -cam.transform.forward : Vector3.up;
        Vector3 recuo = new Vector3(paraCam.x, 0f, paraCam.z);
        float inclinacao = Mathf.Max(0.2f, paraCam.y);
        // até uns 8 m de altura: a copa fica bem acima da cabeça
        Vector3 b = a + recuo / inclinacao * 8f;

        alvo.Clear();
        float r0 = Mathf.Max(raio, raioCopa);
        int x0 = Mathf.FloorToInt((Mathf.Min(a.x, b.x) - r0) / Celula), x1 = Mathf.FloorToInt((Mathf.Max(a.x, b.x) + r0) / Celula);
        int z0 = Mathf.FloorToInt((Mathf.Min(a.z, b.z) - r0) / Celula), z1 = Mathf.FloorToInt((Mathf.Max(a.z, b.z) + r0) / Celula);
        for (int x = x0; x <= x1; x++)
            for (int z = z0; z <= z1; z++)
            {
                if (!grade.TryGetValue(Chave(x, z), out var lista)) continue;
                foreach (var p in lista)
                {
                    if (p.r == null || alvo.Contains(p)) continue;
                    var bb = p.r.bounds;
                    if (bb.max.y < pe + alturaMinima) continue;
                    if (DistanciaAoSegmento(bb, a, b) <= (p.copa ? raioCopa : raio)) alvo.Add(p);
                }
            }

        foreach (var p in alvo) if (!p.trocada && p.alfa >= 1f) Trocar(p, true);

        float passo = velocidade * Time.deltaTime;
        for (int i = ativas.Count - 1; i >= 0; i--)
        {
            var p = ativas[i];
            if (p.r == null) { ativas.RemoveAt(i); continue; }
            float quer = alvo.Contains(p) ? (p.bloqueia ? alfaDeQuemBloqueia : p.copa ? alfaCopa : alfaMinimo) : 1f;
            p.alfa = Mathf.MoveTowards(p.alfa, quer, passo);
            if (p.alfa >= 1f && quer >= 1f) { Trocar(p, false); ativas.RemoveAt(i); continue; }
            Pintar(p);
        }
    }

    static float DistanciaAoSegmento(Bounds bb, Vector3 a, Vector3 b)
    {
        // distância no plano entre o retângulo da planta e o segmento jogador→câmera
        Vector2 min = new Vector2(bb.min.x, bb.min.z), max = new Vector2(bb.max.x, bb.max.z);
        Vector2 p0 = new Vector2(a.x, a.z), p1 = new Vector2(b.x, b.z);
        float melhor = float.MaxValue;
        for (int k = 0; k <= 4; k++)
        {
            Vector2 q = Vector2.Lerp(p0, p1, k / 4f);
            Vector2 c = Vector2.Max(min, Vector2.Min(max, q));
            melhor = Mathf.Min(melhor, Vector2.Distance(q, c));
        }
        return melhor;
    }

    void Trocar(Planta p, bool desbotar)
    {
        p.trocada = desbotar;
        if (desbotar)
        {
            p.r.sharedMaterials = p.desbotados;
            ativas.Add(p);
            Pintar(p);
        }
        else
        {
            p.alfa = 1f;
            p.r.sharedMaterials = p.originais;
            p.r.SetPropertyBlock(null);
            for (int i = 0; i < p.originais.Length; i++) p.r.SetPropertyBlock(null, i);
        }
    }

    void Pintar(Planta p)
    {
        for (int i = 0; i < p.cores.Length; i++)
        {
            p.r.GetPropertyBlock(bloco, i);
            var c = p.cores[i];
            c.a *= p.alfa;
            bloco.SetColor(BaseColor, c);
            p.r.SetPropertyBlock(bloco, i);
        }
    }

    void OnDisable()
    {
        foreach (var p in ativas) if (p.r != null) Trocar(p, false);
        ativas.Clear();
    }

    void OnDestroy()
    {
        foreach (var c in copias.Values) if (c != null) Destroy(c);
        copias.Clear();
    }
}
