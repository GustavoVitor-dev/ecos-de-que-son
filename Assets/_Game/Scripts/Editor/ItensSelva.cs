using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// Os itens do capítulo, montados de peça primitiva e malha extrudada.
///
/// Nenhum pacote do projeto tem faca de combate, caderno ou rádio de mochila
/// da época, e a câmera de cima mostra cada um com uns 30 pixels: o que
/// importa é a silhueta no chão e o detalhe na tela de inspeção, que é uma
/// cópia deste mesmo objeto.
///
/// Medidas em metros, de verdade (a HistoriaSelva escala pra ler no mapa).
public static class ItensSelva
{
    const string PastaMat = "Assets/_Game/Materiais/Itens/";
    const string PastaMalha = "Assets/_Game/Modelos/Itens/";

    // ---------------------------------------------------------------- materiais

    public static Material Mat(string nome, Color c, float liso, float metal = 0f, bool vidro = false)
    {
        string arq = PastaMat + nome + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(arq);
        if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, arq); }
        m.SetColor("_BaseColor", c);
        m.SetFloat("_Smoothness", liso);
        m.SetFloat("_Metallic", metal);
        if (vidro)
        {
            m.SetFloat("_Surface", 1f); m.SetFloat("_Blend", 0f);
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }
        EditorUtility.SetDirty(m);
        return m;
    }

    static Material Aco => Mat("I_Aco", new Color(0.46f, 0.47f, 0.47f), 0.62f, 0.85f);
    static Material AcoSujo => Mat("I_AcoSujo", new Color(0.3f, 0.29f, 0.27f), 0.35f, 0.7f);
    static Material Couro => Mat("I_CouroCabo", new Color(0.3f, 0.2f, 0.12f), 0.28f);
    static Material CouroEscuro => Mat("I_CouroEscuro", new Color(0.19f, 0.13f, 0.08f), 0.2f);
    static Material Latao => Mat("I_Latao", new Color(0.62f, 0.48f, 0.22f), 0.55f, 0.9f);
    static Material Oliva => Mat("I_Oliva", new Color(0.22f, 0.24f, 0.14f), 0.22f);
    static Material OlivaEscuro => Mat("I_OlivaEscuro", new Color(0.12f, 0.13f, 0.08f), 0.18f);
    static Material Borracha => Mat("I_Borracha", new Color(0.04f, 0.04f, 0.04f), 0.3f);
    static Material Papel => Mat("I_Papel", new Color(0.66f, 0.6f, 0.46f), 0.08f);
    static Material Capa => Mat("I_CapaCaderno", new Color(0.2f, 0.17f, 0.11f), 0.16f);
    static Material Sangue => Mat("I_Sangue", new Color(0.2f, 0.02f, 0.015f), 0.55f);
    static Material Lama => Mat("I_Lama", new Color(0.16f, 0.12f, 0.08f), 0.4f);
    static Material Vidro => Mat("I_Vidro", new Color(0.75f, 0.8f, 0.78f, 0.28f), 0.95f, 0f, true);
    static Material Cobre => Mat("I_Cobre", new Color(0.72f, 0.42f, 0.2f), 0.6f, 0.95f);
    static Material Corrosao => Mat("I_Corrosao", new Color(0.55f, 0.66f, 0.5f), 0.05f);
    static Material Crosta => Mat("I_Crosta", new Color(0.82f, 0.8f, 0.7f), 0.02f);
    static Material FioVermelho => Mat("I_FioVermelho", new Color(0.45f, 0.06f, 0.04f), 0.4f);
    static Material FioAmarelo => Mat("I_FioAmarelo", new Color(0.6f, 0.5f, 0.1f), 0.4f);
    static Material Visor => Mat("I_Visor", new Color(0.02f, 0.025f, 0.02f), 0.9f);

    // ---------------------------------------------------------------- peças

    static GameObject Peca(Transform pai, PrimitiveType tipo, Vector3 pos, Vector3 giro, Vector3 tam, Material m, string nome = null)
    {
        var go = GameObject.CreatePrimitive(tipo);
        if (nome != null) go.name = nome;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(pai, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = Quaternion.Euler(giro);
        go.transform.localScale = tam;
        go.GetComponent<Renderer>().sharedMaterial = m;
        return go;
    }

    /// Cilindro deitado ao longo de X (o primitivo é em pé, altura 2).
    static GameObject Cano(Transform pai, Vector3 pos, float raio, float comprimento, Material m, float giroY = 0f)
        => Peca(pai, PrimitiveType.Cylinder, pos, new Vector3(0f, giroY, 90f), new Vector3(raio * 2f, comprimento * 0.5f, raio * 2f), m);

    /// Fio: cilindros finos de ponto a ponto.
    static void Fio(Transform pai, IList<Vector3> pts, float raio, Material m)
    {
        for (int i = 0; i + 1 < pts.Count; i++)
        {
            var a = pts[i]; var b = pts[i + 1];
            var go = Peca(pai, PrimitiveType.Cylinder, (a + b) * 0.5f, Vector3.zero, new Vector3(raio * 2f, Vector3.Distance(a, b) * 0.5f + raio, raio * 2f), m);
            go.transform.localRotation = Quaternion.FromToRotation(Vector3.up, b - a);
        }
    }

    /// Placa extrudada de um contorno (em X,Z), espessura em Y. Contorno em
    /// estrela a partir do centro (leque do centroide).
    static Mesh Extrudar(string nome, Vector2[] c, float esp)
    {
        string arq = PastaMalha + nome + ".asset";
        var malha = AssetDatabase.LoadAssetAtPath<Mesh>(arq);
        bool nova = malha == null;
        if (nova) malha = new Mesh { name = nome };
        malha.Clear();
        var v = new List<Vector3>(); var t = new List<int>(); var n = new List<Vector3>();
        Vector2 cen = Vector2.zero; foreach (var p in c) cen += p; cen /= c.Length;
        float h = esp * 0.5f;
        foreach (float y in new[] { h, -h })
        {
            int b = v.Count;
            v.Add(new Vector3(cen.x, y, cen.y)); n.Add(y > 0 ? Vector3.up : Vector3.down);
            foreach (var p in c) { v.Add(new Vector3(p.x, y, p.y)); n.Add(y > 0 ? Vector3.up : Vector3.down); }
            for (int i = 0; i < c.Length; i++)
            {
                int a = b + 1 + i, d = b + 1 + (i + 1) % c.Length;
                if (y > 0) { t.Add(b); t.Add(a); t.Add(d); } else { t.Add(b); t.Add(d); t.Add(a); }
            }
        }
        for (int i = 0; i < c.Length; i++)
        {
            var p0 = c[i]; var p1 = c[(i + 1) % c.Length];
            var lado = new Vector3(-(p1.y - p0.y), 0f, p1.x - p0.x).normalized;
            int b = v.Count;
            v.Add(new Vector3(p0.x, h, p0.y)); v.Add(new Vector3(p1.x, h, p1.y)); v.Add(new Vector3(p1.x, -h, p1.y)); v.Add(new Vector3(p0.x, -h, p0.y));
            for (int k = 0; k < 4; k++) n.Add(lado);
            t.Add(b); t.Add(b + 2); t.Add(b + 1); t.Add(b); t.Add(b + 3); t.Add(b + 2);
        }
        malha.SetVertices(v); malha.SetNormals(n); malha.SetTriangles(t, 0);
        malha.RecalculateBounds();
        // contorno em sentido horário visto de cima (é o da faca)
        if (nova) AssetDatabase.CreateAsset(malha, arq); else EditorUtility.SetDirty(malha);
        return malha;
    }

    static GameObject ComMalha(Transform pai, Mesh m, Material mat, string nome)
    {
        var go = new GameObject(nome);
        go.transform.SetParent(pai, false);
        go.AddComponent<MeshFilter>().sharedMesh = m;
        go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        return go;
    }

    static Transform Raiz(string nome, Transform pai)
    {
        var go = new GameObject(nome);
        if (pai != null) go.transform.SetParent(pai, false);
        return go.transform;
    }

    // ---------------------------------------------------------------- itens

    /// Faca de combate (tipo Ka-Bar): lâmina de ponta recortada, guarda,
    /// cabo de anéis de couro, pomo de aço. Deitada, ao longo de X.
    public static GameObject Faca(Transform pai)
    {
        var r = Raiz("Faca", pai);
        // horário visto de cima: fio embaixo (z-), dorso em cima com o recorte da ponta
        var contorno = new[]
        {
            new Vector2(0f, 0.016f), new Vector2(0.118f, 0.016f), new Vector2(0.15f, 0.011f),
            new Vector2(0.178f, 0.003f), new Vector2(0.172f, -0.004f), new Vector2(0.155f, -0.012f),
            new Vector2(0.125f, -0.017f), new Vector2(0f, -0.017f),
        };
        var lamina = ComMalha(r, Extrudar("Faca_Lamina", contorno, 0.005f), Aco, "Lamina");
        lamina.transform.localPosition = new Vector3(0.012f, 0f, 0f);
        // sangue seco perto do fio e a canaleta escura no meio
        Peca(r, PrimitiveType.Cube, new Vector3(0.07f, 0.0028f, 0.004f), Vector3.zero, new Vector3(0.1f, 0.0008f, 0.005f), AcoSujo, "Canaleta");
        Peca(r, PrimitiveType.Cube, new Vector3(0.11f, 0.0029f, -0.011f), new Vector3(0f, 8f, 0f), new Vector3(0.05f, 0.0006f, 0.008f), Sangue, "Sangue");
        Peca(r, PrimitiveType.Cube, new Vector3(0.06f, -0.0029f, -0.012f), new Vector3(0f, -5f, 0f), new Vector3(0.03f, 0.0006f, 0.006f), Sangue, "Sangue2");
        // guarda
        Peca(r, PrimitiveType.Cube, new Vector3(0.006f, 0f, 0f), Vector3.zero, new Vector3(0.008f, 0.014f, 0.058f), AcoSujo, "Guarda");
        // cabo: anéis de couro alternando o tom, um pouco mais grosso no meio
        for (int i = 0; i < 12; i++)
        {
            float x = -0.004f - i * 0.0095f;
            float raio = 0.0125f + 0.0018f * Mathf.Sin(i / 11f * Mathf.PI);
            Cano(r, new Vector3(x, 0f, 0f), raio, 0.0092f, i % 2 == 0 ? Couro : CouroEscuro);
        }
        Cano(r, new Vector3(-0.123f, 0f, 0f), 0.014f, 0.012f, AcoSujo);
        Peca(r, PrimitiveType.Sphere, new Vector3(-0.131f, 0f, 0f), Vector3.zero, new Vector3(0.008f, 0.02f, 0.02f), AcoSujo, "Pomo");
        return r.gameObject;
    }

    /// Caderno de operador: capa de couro gasta, miolo inchado de água,
    /// elástico, a bússola presa num canto, sangue na capa e o toco da página
    /// arrancada saindo pela borda.
    public static GameObject CadernoDeCampo(Transform pai)
    {
        var r = Raiz("Caderno", pai);
        const float L = 0.14f, C = 0.2f;
        Peca(r, PrimitiveType.Cube, new Vector3(0f, 0.0015f, 0f), Vector3.zero, new Vector3(L, 0.003f, C), Capa, "CapaDeTras");
        // miolo: inchado, as bordas das folhas descasando
        for (int i = 0; i < 4; i++)
            Peca(r, PrimitiveType.Cube, new Vector3(0.003f + i * 0.0012f, 0.006f + i * 0.0035f, (i % 2 == 0 ? 0.001f : -0.0015f)),
                new Vector3(0f, (i - 1.5f) * 0.9f, 0f), new Vector3(L - 0.008f, 0.0036f, C - 0.008f), Papel, "Folhas" + i);
        Peca(r, PrimitiveType.Cube, new Vector3(0f, 0.0215f, 0f), new Vector3(0f, 0.6f, 0.8f), new Vector3(L, 0.003f, C), Capa, "Capa");
        // lombada
        Peca(r, PrimitiveType.Cube, new Vector3(-L * 0.5f, 0.0115f, 0f), Vector3.zero, new Vector3(0.006f, 0.023f, C), CouroEscuro, "Lombada");
        // elástico
        Peca(r, PrimitiveType.Cube, new Vector3(L * 0.28f, 0.012f, 0f), Vector3.zero, new Vector3(0.006f, 0.025f, C + 0.004f), Borracha, "Elastico");
        // sangue: mancha e um dedo arrastado na capa
        Peca(r, PrimitiveType.Cylinder, new Vector3(-0.02f, 0.0235f, 0.045f), Vector3.zero, new Vector3(0.045f, 0.0006f, 0.03f), Sangue, "Mancha");
        Peca(r, PrimitiveType.Cylinder, new Vector3(0.004f, 0.0235f, 0.03f), new Vector3(0f, 30f, 0f), new Vector3(0.012f, 0.0006f, 0.05f), Sangue, "Dedo");
        Peca(r, PrimitiveType.Cylinder, new Vector3(0.03f, 0.0235f, -0.06f), Vector3.zero, new Vector3(0.05f, 0.0005f, 0.035f), Lama, "Lama");
        // o toco da página arrancada saindo pela borda de baixo
        Peca(r, PrimitiveType.Cube, new Vector3(0.02f, 0.012f, -C * 0.5f - 0.004f), new Vector3(0f, 12f, 0f), new Vector3(0.05f, 0.0008f, 0.014f), Papel, "PaginaRasgada");
        Peca(r, PrimitiveType.Cube, new Vector3(0.028f, 0.0126f, -C * 0.5f - 0.006f), new Vector3(0f, 12f, 0f), new Vector3(0.02f, 0.0004f, 0.008f), Sangue, "SangueNaPagina");
        // a bússola presa no canto de cima, por uma tira
        var b = Raiz("Bussola", r);
        b.localPosition = new Vector3(L * 0.5f + 0.012f, 0.012f, C * 0.5f - 0.03f);
        Peca(b, PrimitiveType.Cube, new Vector3(-0.012f, 0f, 0f), Vector3.zero, new Vector3(0.03f, 0.004f, 0.012f), CouroEscuro, "Tira");
        Peca(b, PrimitiveType.Cylinder, new Vector3(0.012f, 0f, 0f), Vector3.zero, new Vector3(0.05f, 0.007f, 0.05f), Latao, "Caixa");
        Peca(b, PrimitiveType.Cylinder, new Vector3(0.012f, 0.0072f, 0f), Vector3.zero, new Vector3(0.042f, 0.0004f, 0.042f), Papel, "Mostrador");
        Peca(b, PrimitiveType.Cube, new Vector3(0.012f, 0.0082f, 0.006f), new Vector3(0f, 20f, 0f), new Vector3(0.004f, 0.0008f, 0.02f), Sangue, "AgulhaNorte");
        Peca(b, PrimitiveType.Cube, new Vector3(0.012f, 0.0082f, -0.006f), new Vector3(0f, 20f, 0f), new Vector3(0.004f, 0.0008f, 0.02f), Aco, "AgulhaSul");
        Peca(b, PrimitiveType.Cylinder, new Vector3(0.012f, 0.009f, 0f), Vector3.zero, new Vector3(0.044f, 0.0006f, 0.044f), Vidro, "Vidro");
        return r.gameObject;
    }

    /// AN/PRC-25 caído de costas, painel pra cima: o painel afundou no canto
    /// do receptor, fios pra fora, a antena dobrada, o monofone pela corda.
    public static GameObject RadioQuebrado(Transform pai)
    {
        var r = Raiz("Radio", pai);
        const float L = 0.28f, A = 0.1f, P = 0.25f;
        Peca(r, PrimitiveType.Cube, new Vector3(0f, A * 0.5f, 0f), Vector3.zero, new Vector3(L, A, P), Oliva, "Caixa");
        // quinas da caixa (reforço) e a alça
        foreach (float sx in new[] { -1f, 1f })
            Peca(r, PrimitiveType.Cube, new Vector3(sx * (L * 0.5f + 0.004f), A * 0.5f, 0f), Vector3.zero, new Vector3(0.008f, A * 0.9f, P * 0.96f), OlivaEscuro);
        Peca(r, PrimitiveType.Cube, new Vector3(0f, A * 0.35f, -P * 0.5f - 0.02f), new Vector3(0f, 4f, 0f), new Vector3(0.2f, 0.006f, 0.035f), OlivaEscuro, "Alca");
        // painel
        var painel = Raiz("Painel", r); painel.localPosition = new Vector3(0f, A + 0.002f, 0f);
        Peca(painel, PrimitiveType.Cube, Vector3.zero, Vector3.zero, new Vector3(L - 0.02f, 0.004f, P - 0.02f), OlivaEscuro, "Placa");
        // dois botões grandes de sintonia, um pequeno de volume
        foreach (var (x, z, raio) in new[] { (-0.08f, 0.04f, 0.024f), (0.0f, 0.04f, 0.024f), (-0.09f, -0.06f, 0.014f) })
        {
            Peca(painel, PrimitiveType.Cylinder, new Vector3(x, 0.012f, z), Vector3.zero, new Vector3(raio * 2f, 0.01f, raio * 2f), Borracha);
            Peca(painel, PrimitiveType.Cube, new Vector3(x, 0.0225f, z + raio * 0.5f), Vector3.zero, new Vector3(0.003f, 0.001f, raio), Crosta);
        }
        // janela de frequência
        Peca(painel, PrimitiveType.Cube, new Vector3(-0.04f, 0.004f, -0.02f), Vector3.zero, new Vector3(0.07f, 0.002f, 0.022f), Visor, "Frequencia");
        // conectores do monofone e da antena
        Peca(painel, PrimitiveType.Cylinder, new Vector3(-0.1f, 0.012f, 0.09f), Vector3.zero, new Vector3(0.018f, 0.012f, 0.018f), AcoSujo, "ConectorMonofone");
        Peca(painel, PrimitiveType.Cylinder, new Vector3(0.1f, 0.016f, 0.09f), Vector3.zero, new Vector3(0.022f, 0.016f, 0.022f), AcoSujo, "BaseAntena");
        // o canto do receptor, estraçalhado: buraco escuro, placa entortada, cacos
        Peca(painel, PrimitiveType.Cube, new Vector3(0.075f, -0.004f, -0.045f), Vector3.zero, new Vector3(0.09f, 0.006f, 0.1f), Visor, "Buraco");
        Peca(painel, PrimitiveType.Cube, new Vector3(0.06f, 0.012f, -0.075f), new Vector3(-24f, 18f, 9f), new Vector3(0.07f, 0.003f, 0.045f), OlivaEscuro, "PlacaDobrada");
        Peca(painel, PrimitiveType.Cube, new Vector3(0.105f, 0.006f, -0.02f), new Vector3(15f, -30f, -20f), new Vector3(0.03f, 0.003f, 0.02f), OlivaEscuro, "Caco");
        Peca(painel, PrimitiveType.Cube, new Vector3(0.05f, 0.004f, -0.01f), new Vector3(8f, 50f, 12f), new Vector3(0.018f, 0.002f, 0.012f), Vidro, "CacoVidro");
        // válvula partida lá dentro
        Peca(painel, PrimitiveType.Cylinder, new Vector3(0.085f, 0.004f, -0.04f), new Vector3(80f, 20f, 0f), new Vector3(0.018f, 0.018f, 0.018f), Vidro, "ValvulaPartida");
        // fios pra fora do buraco
        Fio(painel, new[] { new Vector3(0.07f, -0.002f, -0.05f), new Vector3(0.08f, 0.02f, -0.07f), new Vector3(0.11f, 0.018f, -0.1f), new Vector3(0.13f, 0.006f, -0.12f) }, 0.0016f, FioVermelho);
        Fio(painel, new[] { new Vector3(0.09f, -0.002f, -0.03f), new Vector3(0.11f, 0.024f, -0.035f), new Vector3(0.14f, 0.02f, -0.02f), new Vector3(0.16f, 0.004f, -0.03f) }, 0.0016f, FioAmarelo);
        Fio(painel, new[] { new Vector3(0.06f, -0.002f, -0.03f), new Vector3(0.05f, 0.016f, -0.06f), new Vector3(0.035f, 0.01f, -0.09f) }, 0.0014f, Cobre);
        // antena: dobrada no meio, caída no chão
        var baseAnt = new Vector3(0.1f, A + 0.03f, 0.09f);
        Fio(r, new[] { baseAnt, baseAnt + new Vector3(0.05f, 0.04f, 0.12f), new Vector3(0.23f, 0.01f, 0.32f), new Vector3(0.48f, 0.005f, 0.42f) }, 0.0035f, Borracha);
        // monofone pela corda
        var mono = Raiz("Monofone", r); mono.localPosition = new Vector3(-0.28f, 0.02f, 0.1f); mono.localRotation = Quaternion.Euler(0f, -35f, 0f);
        Peca(mono, PrimitiveType.Cube, Vector3.zero, Vector3.zero, new Vector3(0.16f, 0.028f, 0.035f), Borracha, "Corpo");
        Peca(mono, PrimitiveType.Cylinder, new Vector3(0.075f, 0.005f, 0f), Vector3.zero, new Vector3(0.05f, 0.012f, 0.05f), Borracha, "Fone");
        Peca(mono, PrimitiveType.Cylinder, new Vector3(-0.075f, 0.005f, 0f), Vector3.zero, new Vector3(0.045f, 0.012f, 0.045f), Borracha, "Microfone");
        var corda = new List<Vector3>();
        for (int i = 0; i <= 10; i++)
        {
            float t = i / 10f;
            var a = new Vector3(-0.1f, A + 0.012f, 0.09f); var b = new Vector3(-0.25f, 0.03f, 0.1f);
            corda.Add(Vector3.Lerp(a, b, t) + new Vector3(0f, Mathf.Sin(t * Mathf.PI) * 0.02f, Mathf.Sin(t * Mathf.PI * 3f) * 0.03f));
        }
        Fio(r, corda, 0.004f, Borracha);
        // lama por cima
        Peca(r, PrimitiveType.Cylinder, new Vector3(-0.06f, A + 0.005f, -0.08f), Vector3.zero, new Vector3(0.1f, 0.001f, 0.06f), Lama, "Lama");
        return r.gameObject;
    }

    public static GameObject Valvula(Transform pai)
    {
        var r = Raiz("Valvula", pai);
        Peca(r, PrimitiveType.Cylinder, new Vector3(0f, 0.008f, 0f), Vector3.zero, new Vector3(0.028f, 0.008f, 0.028f), Borracha, "Base");
        Peca(r, PrimitiveType.Cylinder, new Vector3(0f, 0.038f, 0f), Vector3.zero, new Vector3(0.024f, 0.022f, 0.024f), Vidro, "Vidro");
        Peca(r, PrimitiveType.Sphere, new Vector3(0f, 0.06f, 0f), Vector3.zero, new Vector3(0.024f, 0.014f, 0.024f), Vidro, "Topo");
        Peca(r, PrimitiveType.Cube, new Vector3(0f, 0.036f, 0f), Vector3.zero, new Vector3(0.012f, 0.03f, 0.004f), Visor, "Placas");
        Peca(r, PrimitiveType.Cylinder, new Vector3(0f, 0.052f, 0f), Vector3.zero, new Vector3(0.014f, 0.0015f, 0.014f), Aco, "Espelho");
        for (int i = 0; i < 7; i++)
        {
            float a = i * Mathf.PI * 2f / 7f;
            Peca(r, PrimitiveType.Cylinder, new Vector3(Mathf.Cos(a) * 0.008f, -0.004f, Mathf.Sin(a) * 0.008f), Vector3.zero, new Vector3(0.0015f, 0.005f, 0.0015f), Aco);
        }
        return r.gameObject;
    }

    public static GameObject FioDeCobre(Transform pai)
    {
        var r = Raiz("FioDeCobre", pai);
        // rolo frouxo: três voltas tortas
        var pts = new List<Vector3>();
        for (int i = 0; i <= 72; i++)
        {
            float a = i / 24f * Mathf.PI * 2f;
            float raio = 0.035f + 0.004f * Mathf.Sin(a * 3.3f);
            pts.Add(new Vector3(Mathf.Cos(a) * raio, 0.003f + i * 0.00012f, Mathf.Sin(a) * raio));
        }
        pts.Add(pts[pts.Count - 1] + new Vector3(0.05f, -0.003f, 0.02f));
        Fio(r, pts, 0.0018f, Cobre);
        return r.gameObject;
    }

    public static GameObject Fita(Transform pai)
    {
        var r = Raiz("FitaIsolante", pai);
        var pref = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Survival Tools/Prefabs/tape.prefab");
        if (pref != null)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(pref, r);
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            var b = FlorestaSelva.Limites(go);
            float maior = Mathf.Max(b.size.x, b.size.y, b.size.z, 0.001f);
            go.transform.localScale *= 0.07f / maior;
            // o rolo do pacote é branco de farmácia: fita de pano verde-oliva, suja de lama
            foreach (var rr in go.GetComponentsInChildren<Renderer>(true))
            {
                var mats = rr.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null) continue;
                    string arq = PastaMat + "I_Fita_" + i + ".mat";
                    var m = AssetDatabase.LoadAssetAtPath<Material>(arq);
                    if (m == null) { m = new Material(mats[i]); AssetDatabase.CreateAsset(m, arq); }
                    m.CopyPropertiesFromMaterial(mats[i]);
                    m.SetColor("_BaseColor", new Color(0.24f, 0.25f, 0.18f));
                    m.SetFloat("_Smoothness", 0.25f);
                    EditorUtility.SetDirty(m);
                    mats[i] = m;
                }
                rr.sharedMaterials = mats;
            }
        }
        else
        {
            Peca(r, PrimitiveType.Cylinder, new Vector3(0f, 0.01f, 0f), Vector3.zero, new Vector3(0.07f, 0.01f, 0.07f), Borracha, "Rolo");
        }
        return r.gameObject;
    }

    /// A bateria do PRC-25 (BA-386), aberta no fim: inchada, rachada, crosta
    /// branca e verde de corrosão, ácido seco escorrido.
    public static GameObject BateriaPodre(Transform pai)
    {
        var r = Raiz("BateriaPodre", pai);
        var caixa = Peca(r, PrimitiveType.Cube, new Vector3(0f, 0.04f, 0f), new Vector3(0f, 0f, 2f), new Vector3(0.1f, 0.08f, 0.2f), Oliva, "Caixa");
        // inchaço: a face de cima estufada
        Peca(r, PrimitiveType.Sphere, new Vector3(0.005f, 0.078f, 0.01f), Vector3.zero, new Vector3(0.085f, 0.018f, 0.17f), Oliva, "Estufada");
        Peca(r, PrimitiveType.Cube, new Vector3(0f, 0.05f, -0.101f), Vector3.zero, new Vector3(0.07f, 0.045f, 0.002f), OlivaEscuro, "Etiqueta");
        // rachaduras
        Peca(r, PrimitiveType.Cube, new Vector3(0.02f, 0.088f, 0.03f), new Vector3(0f, 35f, 0f), new Vector3(0.002f, 0.004f, 0.09f), Visor, "Racha");
        Peca(r, PrimitiveType.Cube, new Vector3(-0.051f, 0.05f, 0.02f), new Vector3(20f, 0f, 0f), new Vector3(0.003f, 0.05f, 0.002f), Visor, "Racha2");
        // conector e terminais comidos
        Peca(r, PrimitiveType.Cylinder, new Vector3(0f, 0.09f, 0.075f), Vector3.zero, new Vector3(0.03f, 0.008f, 0.03f), AcoSujo, "Conector");
        var rnd = new System.Random(386);
        for (int i = 0; i < 26; i++)
        {
            float x = (float)(rnd.NextDouble() - 0.5) * 0.06f, z = 0.06f + (float)(rnd.NextDouble() - 0.5) * 0.05f;
            float s = 0.006f + (float)rnd.NextDouble() * 0.012f;
            Peca(r, PrimitiveType.Sphere, new Vector3(x, 0.09f + s * 0.2f, z), Vector3.zero, new Vector3(s, s * 0.6f, s), i % 3 == 0 ? Corrosao : Crosta);
        }
        // ácido escorrido pela lateral
        for (int i = 0; i < 3; i++)
            Peca(r, PrimitiveType.Cube, new Vector3(0.0505f, 0.055f - i * 0.004f, 0.02f + i * 0.03f), new Vector3(0f, 0f, 0f), new Vector3(0.002f, 0.05f - i * 0.01f, 0.008f), Corrosao, "Escorrido" + i);
        return r.gameObject;
    }
}
