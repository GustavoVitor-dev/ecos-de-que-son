using System.Collections.Generic;
using System.IO;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// Constrói a floresta da Selva em volta do layout de jogo que já existe.
///
/// O bloco era um cubo de 40 × 90 m com uma textura só e árvores em cima. O
/// que muda aqui:
///   - terreno de verdade: micro-relevo no chão, o mirante vira morro, um córrego
///     corta o bloco de lado a lado, crateras de bomba, e fora da área jogável
///     morros e um vale por onde o córrego sai;
///   - chão pintado em camadas: serapilheira, trilha de terra batida, raízes,
///     lama no córrego e nas crateras, rocha com musgo nos barrancos;
///   - o que tem numa mata: troncos caídos, tocos, pedras, raízes, galhos,
///     cogumelos, samambaias, palmeiras, e o lixo de guerra;
///   - os blocos cinza de protótipo (rochedo, ruínas, caixotes) ganham forma de
///     pedra, cabana e tambores. O colisor antigo fica: o jogo não muda.
///
/// Tudo que já estava na cena é assentado no chão novo. A altura original de
/// cada objeto fica gravada na primeira vez, então dá pra rodar de novo à
/// vontade sem o assentamento acumular.
public static class FlorestaSelva
{
    const string Cena = "Assets/_Game/Scenes/Selva.unity";
    const string Pasta = "Assets/_Game/Terreno";
    const string Original = "Assets/_Game/Terreno/Selva_alturas_originais.json";

    // terreno: 180 × 200 m, do -90 ao 90 em x e do -80 ao 120 em z
    const float X0 = -90f, Z0 = -80f, Largura = 180f, Comprimento = 200f;
    const float Base = -6f, Altura = 32f;
    const int ResAltura = 513, ResPintura = 512;

    // área jogável. Era um corredor de 36 m (±18) num terreno de 180 m: andar
    // reto numa trilha. Agora 80 m (±40), com córrego como barreira e três
    // travessias, aldeia no extremo leste e lugares pra achar dos dois lados.
    // As paredes Limite_* acompanham (Construir as reposiciona).
    public const float JX = 40f, JZmin = -41f, JZmax = 46f;

    // Rede de trilhas. A principal leva do começo à saída; as outras bifurcam,
    // algumas voltam pra principal (laço) e outras morrem num lugar — cratera,
    // corpo, cova, cabana, barranco. Trilha sem saída tem que terminar em
    // alguma coisa, senão é só um erro de mapa.
    internal static readonly Vector2[][] Trilhas =
    {
        // 0: principal
        new[] {
            new Vector2(0, -60), new Vector2(0, -36), new Vector2(0, -32), new Vector2(-2, -31), new Vector2(-3, -29),
            new Vector2(1, -27), new Vector2(-1, -24), new Vector2(2, -21), new Vector2(0, -17), new Vector2(0, -13),
            new Vector2(0, -9), new Vector2(0, -4), new Vector2(0.6f, 0), new Vector2(-1, 4), new Vector2(-4, 8),
            new Vector2(-4, 14), new Vector2(0, 19), new Vector2(-1, 24), new Vector2(2, 27), new Vector2(1.5f, 32),
            new Vector2(1, 38), new Vector2(1, 43), new Vector2(3, 60), new Vector2(12, 90) },
        // desvios pelos dois matos da escolha (beats 4a e 4b)
        new[] { new Vector2(0, -17), new Vector2(-5, -19), new Vector2(-8, -23) },
        new[] { new Vector2(0, -17), new Vector2(8, -19), new Vector2(9, -24) },
        // o rádio
        new[] { new Vector2(0, 19), new Vector2(-5, 19.5f), new Vector2(-10, 19) },
        // laço: a volta da patrulha pelo leste do rochedo
        new[] { new Vector2(-1, 4), new Vector2(5, 7), new Vector2(6, 12), new Vector2(4, 17), new Vector2(0, 19) },
        // laço leste do começo: sai da principal e volta pelo mato 4b
        new[] { new Vector2(1, -27), new Vector2(6, -28.5f), new Vector2(9, -24) },
        // sem saída: cratera oeste do começo, com o corpo
        new[] { new Vector2(-3, -29), new Vector2(-7, -31), new Vector2(-10.5f, -32.5f) },
        // sem saída: segue o mato 4a até a cratera do oeste
        new[] { new Vector2(-8, -23), new Vector2(-11, -19.5f), new Vector2(-12.8f, -15.5f) },
        // rota leste: beira do córrego, tronco-ponte (x≈12) e a aldeia
        new[] { new Vector2(0, -4), new Vector2(5.5f, -4.5f), new Vector2(10.5f, -2.5f), new Vector2(13.5f, 2f),
                new Vector2(18f, 4.5f), new Vector2(24f, 5.5f), new Vector2(29.5f, 6.5f) },
        // sem saída: córrego pro oeste, até a cabana e as covas
        new[] { new Vector2(0.6f, 0), new Vector2(-4.5f, 1.2f), new Vector2(-7.8f, 2.6f) },
        // do posto pro norte, até o barranco — e dali até a cratera noroeste
        new[] { new Vector2(-10, 19), new Vector2(-12, 24.5f), new Vector2(-13.8f, 29f),
                new Vector2(-19f, 31.5f), new Vector2(-24.5f, 31f) },
        // do norte pro leste, até a aldeia (a volta com a chave)
        new[] { new Vector2(2, 27), new Vector2(7, 26.2f), new Vector2(11.5f, 25.2f), new Vector2(14.2f, 23.5f),
                new Vector2(19f, 19f), new Vector2(25f, 13f), new Vector2(29.5f, 8f) },
        // rota oeste: sai do começo, atravessa o córrego no vau (x≈-26) e sobe até o posto
        new[] { new Vector2(-7, -31), new Vector2(-15f, -27f), new Vector2(-22f, -17f), new Vector2(-26f, -7f),
                new Vector2(-26.5f, 0f), new Vector2(-24f, 8f), new Vector2(-19f, 14f), new Vector2(-13.5f, 19f) },
        // exploração: cratera grande do oeste
        new[] { new Vector2(-22f, -17f), new Vector2(-27f, -18.5f), new Vector2(-31f, -19.5f) },
        // exploração: além da aldeia, até a cratera leste
        new[] { new Vector2(29.5f, 6.5f), new Vector2(30.5f, 10.5f), new Vector2(33f, 13f), new Vector2(34.5f, 15f) },
        // sem saída: antes do portão, pro oeste, até as covas
        new[] { new Vector2(1.5f, 32), new Vector2(-3.5f, 34), new Vector2(-6, 38) },
    };

    // pontas das trilhas sem saída que ganham um lampião largado no chão:
    // uma luz fraca no fim do caminho é o que faz alguém querer ir até lá
    static readonly Vector2[] Iscas = { new Vector2(-31f, -19.5f), new Vector2(-24.5f, 31f), new Vector2(34.5f, 15f) };

    // Trilha de verdade não é reta: cada trecho é subdividido e ondulado de lado,
    // menos nas pontas (senão a bifurcação desencontra da trilha-mãe).
    static List<Vector2[]> suaves;
    internal static List<Vector2[]> Suaves
    {
        get
        {
            if (suaves != null) return suaves;
            suaves = new List<Vector2[]>();
            for (int k = 0; k < Trilhas.Length; k++)
            {
                var t = Trilhas[k];
                var pts = new List<Vector2>();
                float andado = 0f, total = 0f;
                for (int i = 0; i + 1 < t.Length; i++) total += Vector2.Distance(t[i], t[i + 1]);
                for (int i = 0; i + 1 < t.Length; i++)
                {
                    Vector2 a = t[i], b = t[i + 1];
                    float comp = Vector2.Distance(a, b);
                    int n = Mathf.Max(1, Mathf.CeilToInt(comp / 0.8f));
                    var lado = new Vector2(-(b - a).y, (b - a).x).normalized;
                    for (int j = 0; j < n; j++)
                    {
                        float u = j / (float)n;
                        float s0 = andado + comp * u;
                        float ponta = Mathf.Min(S(0f, 2.5f, s0), S(0f, 2.5f, total - s0));
                        float onda = (Mathf.PerlinNoise(s0 * 0.11f + k * 13.7f, 7.3f) - 0.5f) * 1.6f * ponta;
                        pts.Add(Vector2.Lerp(a, b, u) + lado * onda);
                    }
                    andado += comp;
                }
                pts.Add(t[t.Length - 1]);
                suaves.Add(pts.ToArray());
            }
            return suaves;
        }
    }

    // crateras de bomba: x, z, raio
    internal static readonly Vector3[] Crateras =
    {
        new Vector3(-12, -33, 2.6f), new Vector3(-14, -13, 2.5f), new Vector3(14, -30, 2.4f),
        new Vector3(-15, 40, 2.2f), new Vector3(15, 23, 2.2f),
        new Vector3(-34, -20, 4f), new Vector3(30, -48, 3.5f), new Vector3(-28, 30, 3.2f), new Vector3(36, 20, 4.5f),
        new Vector3(-40, 60, 3.8f), new Vector3(25, 70, 3.2f), new Vector3(-22, -58, 3f), new Vector3(45, -10, 3.6f),
    };

    // ---------------------------------------------------------------- relevo

    static float S(float a, float b, float x) { float t = Mathf.Clamp01((x - a) / (b - a)); return t * t * (3f - 2f * t); }

    static float Fbm(float x, float z, int oitavas)
    {
        float v = 0f, a = 0.5f, f = 1f, soma = 0f;
        for (int i = 0; i < oitavas; i++) { v += a * Mathf.PerlinNoise(x * f + i * 17.3f, z * f - i * 9.1f); soma += a; a *= 0.5f; f *= 2.03f; }
        return v / soma;
    }

    static float DistFora(float x, float z)
    {
        float dx = Mathf.Max(Mathf.Abs(x) - JX, 0f);
        float dz = Mathf.Max(z - JZmax, Mathf.Max(JZmin - z, 0f));
        return Mathf.Sqrt(dx * dx + dz * dz);
    }

    internal static float DistTrilha(float x, float z)
    {
        var p = new Vector2(x, z);
        float m = float.MaxValue;
        foreach (var t in Suaves)
            for (int i = 0; i + 1 < t.Length; i++)
            {
                Vector2 a = t[i], b = t[i + 1], ab = b - a;
                if (ab.sqrMagnitude < 1e-6f) continue;
                // atalho: trecho longe demais nem entra na conta
                if (Mathf.Min((p - a).sqrMagnitude, (p - b).sqrMagnitude) > (m + 1.5f) * (m + 1.5f) && m < 50f) continue;
                float k = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
                m = Mathf.Min(m, Vector2.Distance(p, a + ab * k));
            }
        return m;
    }

    public static float ZCorrego(float x) => -1.5f + 1.3f * Mathf.Sin(x * 0.16f + 0.7f) + 0.6f * Mathf.Sin(x * 0.41f);
    internal static float LarguraCorrego(float x) => 1.25f + 0.35f * Mathf.PerlinNoise(x * 0.2f, 3.3f);

    static float Mirante(float x, float z)
    {
        // o antigo caixote de 17 × 6 m vira um morro com rampas ao sul e ao norte
        float qx = Mathf.Max(Mathf.Abs(x) - 7.5f, 0f) / 3.2f;
        float qz = Mathf.Max(Mathf.Abs(z + 9f) - 2.5f, 0f) / 4.5f;
        float q = Mathf.Sqrt(qx * qx + qz * qz);
        float h = 2f * (1f - S(0f, 1f, q));
        // barranco com pedra nas laterais, não rampa lisa
        return h + (h > 0.05f && h < 1.95f ? (Mathf.PerlinNoise(x * 0.7f, z * 0.7f) - 0.5f) * 0.3f : 0f);
    }

    static float Monte(float x, float z, float cx, float cz, float r, float h)
    {
        float d = Vector2.Distance(new Vector2(x, z), new Vector2(cx, cz)) / r;
        return d >= 1f ? 0f : h * (1f - S(0f, 1f, d));
    }

    static float Cratera(float x, float z, Vector3 c)
    {
        float t = Vector2.Distance(new Vector2(x, z), new Vector2(c.x, c.y)) / c.z;
        if (t > 1.8f) return 0f;
        float fundo = t < 1f ? -0.85f * (1f - t * t) : 0f;
        float borda = 0.35f * Mathf.Exp(-Mathf.Pow((t - 1f) / 0.28f, 2f));
        return fundo + borda;
    }

    public static float H(float x, float z)
    {
        float fora = DistFora(x, z);
        float dt = DistTrilha(x, z);

        // chão da mata: nunca plano, mas sem degrau que atrapalhe andar
        float micro = (Fbm(x * 0.11f + 31f, z * 0.11f + 17f, 3) - 0.5f) * 0.55f
                    + (Mathf.PerlinNoise(x * 0.45f + 5f, z * 0.45f + 9f) - 0.5f) * 0.12f;
        micro *= S(0.6f, 1.8f, dt);
        // trilha gasta: o leito afunda e a terra empurrada por quem passa sobe
        // um pouco nas beiradas — é relevo, não cor, que diz "aqui se anda"
        float h = micro - 0.12f * (1f - S(0.35f, 1.05f, dt))
                + 0.05f * S(0.85f, 1.2f, dt) * (1f - S(1.4f, 2.1f, dt));

        h += Monte(x, z, 1.5f, 10.5f, 4.2f, 0.7f);   // o rochedo senta num monte
        h += Monte(x, z, -14.5f, 30f, 4.5f, 1.1f);    // barranco a oeste do portão
        h += Monte(x, z, 14f, 3f, 4f, 0.8f);          // lombada leste do córrego
        h += Monte(x, z, -13f, -24f, 4f, 0.9f);

        h = Mathf.Max(h, Mirante(x, z));
        foreach (var c in Crateras) h += Cratera(x, z, c);

        if (fora > 0f)
        {
            float s = S(0f, 26f, fora);
            float relevo = 8f * s + (Fbm(x * 0.028f + 3f, z * 0.028f + 11f, 4) - 0.35f) * 14f * S(2f, 30f, fora);
            if (x < 0f) relevo += 4f * s;              // serra a oeste
            if (z > JZmax) relevo -= 5f * S(0f, 40f, z - JZmax); // o norte desce pro rio
            h += relevo;
        }

        // o córrego corta tudo por último; lá fora ele vira vale
        float dc = Mathf.Abs(z - ZCorrego(x));
        float w = LarguraCorrego(x);
        float barranco = 1.6f + fora * 0.8f;
        float leito = -0.55f - S(0f, 30f, fora) * 1.5f;
        h = Mathf.Lerp(h, leito, 1f - S(w, w + barranco, dc));

        return Mathf.Clamp(h, Base + 0.2f, Base + Altura - 0.2f);
    }

    // ---------------------------------------------------------------- menu

    [MenuItem("Ecos/Selva/Construir floresta")]
    public static void Construir()
    {
        if (EditorApplication.isPlaying) { Debug.LogError("Sair do Play antes."); return; }
        EditorSceneManager.SaveOpenScenes();
        var cena = EditorSceneManager.OpenScene(Cena, OpenSceneMode.Single);
        if (!AssetDatabase.IsValidFolder(Pasta)) AssetDatabase.CreateFolder("Assets/_Game", "Terreno");

        var nivel = GameObject.Find("Nivel").transform;
        var registro = CarregarOuGravarOriginais(nivel);

        var antigo = nivel.Find("Floresta");
        if (antigo != null) Object.DestroyImmediate(antigo.gameObject);
        var floresta = new GameObject("Floresta").transform;
        floresta.SetParent(nivel, false);

        // paredes-limite acompanham a área jogável
        foreach (var (n, x) in new[] { ("Limite_Oeste", -JX - 1f), ("Limite_Leste", JX + 1f) })
        {
            var t = nivel.Find(n); if (t == null) continue;
            Undo.RecordObject(t, "limite"); t.position = new Vector3(x, t.position.y, t.position.z); EditorUtility.SetDirty(t);
        }
        foreach (var n in new[] { "Limite_Norte", "Limite_Sul" })
        {
            var t = nivel.Find(n); if (t == null) continue;
            Undo.RecordObject(t, "limite"); t.localScale = new Vector3(JX * 2f + 4f, t.localScale.y, t.localScale.z); EditorUtility.SetDirty(t);
        }

        var terreno = CriarTerreno(floresta);
        Pintar(terreno.terrainData);

        // o cubo de chão e o mirante de caixote saem: o terreno é o chão agora
        nivel.Find("Chao").gameObject.SetActive(false);
        nivel.Find("Mirante").gameObject.SetActive(false);

        TrocarEspecies(nivel);
        Reorganizar(nivel);
        Assentar(registro);
        LimparChao(nivel);
        Vestir(nivel);
        CriarAgua(floresta);
        Espalhar(floresta, nivel);
        Esconderijos(floresta, nivel);
        Decalques(floresta);
        // os galhos 3D pendurados saíram: o material deles é opaco, não some
        // em cima do jogador, e a paralaxe faz o mesmo trabalho melhor
        Paralaxe(floresta, nivel);
        Canopia();

        // a corrente do capítulo (portão, aldeia, poço) por cima da mata nova
        HistoriaSelva.Montar(nivel);

        Physics.SyncTransforms();
        AssarNavegacao();

        EditorSceneManager.MarkSceneDirty(cena);
        EditorSceneManager.SaveScene(cena);
        Debug.Log("[FlorestaSelva] pronto");
    }

    // ---------------------------------------------------------------- altura original

    [System.Serializable] class Item { public string caminho; public float y; public float chao; }
    [System.Serializable] class Registro { public List<Item> itens = new List<Item>(); }

    static string Caminho(Transform t)
    {
        string s = t.name + "#" + t.GetSiblingIndex();
        for (var p = t.parent; p != null; p = p.parent) s = p.name + "#" + p.GetSiblingIndex() + "/" + s;
        return s;
    }

    static Transform Achar(string caminho)
    {
        var partes = caminho.Split('/');
        Transform atual = null;
        foreach (var parte in partes)
        {
            int h = parte.LastIndexOf('#');
            string nome = parte.Substring(0, h);
            int idx = int.Parse(parte.Substring(h + 1));
            if (atual == null)
            {
                foreach (var r in EditorSceneManager.GetActiveScene().GetRootGameObjects())
                    if (r.name == nome && r.transform.GetSiblingIndex() == idx) { atual = r.transform; break; }
            }
            else atual = idx < atual.childCount && atual.GetChild(idx).name == nome ? atual.GetChild(idx) : atual.Find(nome);
            if (atual == null) return null;
        }
        return atual;
    }

    static List<Transform> Assentaveis(Transform nivel)
    {
        var l = new List<Transform>();
        var fora = new HashSet<string> { "Chao", "Limite_Oeste", "Limite_Leste", "Limite_Sul", "Limite_Norte", "Luar", "Mirante", "Floresta" };
        foreach (Transform g in nivel)
        {
            if (fora.Contains(g.name)) continue;
            if (g.childCount > 0 && g.GetComponent<Renderer>() == null && g.GetComponent<Collider>() == null)
                foreach (Transform c in g) l.Add(c);
            else l.Add(g);
        }
        foreach (var raiz in new[] { "Atores", "Beats", "Portao" })
        {
            var r = GameObject.Find(raiz);
            if (r == null) continue;
            foreach (Transform c in r.transform)
            {
                if (c.name.StartsWith("Rota_")) foreach (Transform w in c) l.Add(w);
                else l.Add(c);
            }
        }
        foreach (var n in new[] { "_Setup/Player", "_Setup/Entidade", "_Setup/SpawnPoint", "Ancora_Aparicao_1" })
        {
            var go = GameObject.Find(n);
            if (go != null) l.Add(go.transform);
        }
        return l;
    }

    static Registro CarregarOuGravarOriginais(Transform nivel)
    {
        string arq = Path.GetFullPath(Original);
        if (File.Exists(arq)) return JsonUtility.FromJson<Registro>(File.ReadAllText(arq));

        // primeira vez: o chão antigo (cubo + mirante de caixote) ainda está lá
        var reg = new Registro();
        var chao = nivel.Find("Chao");
        var mirante = nivel.Find("Mirante");
        foreach (var t in Assentaveis(nivel))
        {
            var p = t.position;
            float antigo = 0f;
            foreach (var h in Physics.RaycastAll(new Vector3(p.x, 30f, p.z), Vector3.down, 60f, ~0, QueryTriggerInteraction.Ignore))
                if (h.transform == chao || h.transform.IsChildOf(mirante)) antigo = Mathf.Max(antigo, h.point.y);
            reg.itens.Add(new Item { caminho = Caminho(t), y = p.y, chao = antigo });
        }
        File.WriteAllText(arq, JsonUtility.ToJson(reg, true));
        AssetDatabase.ImportAsset(Original);
        Debug.Log("[FlorestaSelva] alturas originais gravadas: " + reg.itens.Count);
        return reg;
    }

    static void Assentar(Registro reg)
    {
        int n = 0, perdidos = 0;
        foreach (var it in reg.itens)
        {
            var t = Achar(it.caminho);
            if (t == null) { perdidos++; continue; }
            var p = t.position;
            Undo.RecordObject(t, "assentar");
            t.position = new Vector3(p.x, it.y + H(p.x, p.z) - it.chao, p.z);
            EditorUtility.SetDirty(t);
            n++;
        }
        Debug.Log("[FlorestaSelva] assentados " + n + ", não achados " + perdidos);
    }

    /// Tira árvore do meio do caminho e empilha na borda.
    ///
    /// A mata estava sorteada por toda a área: volume alto no meio, nada de
    /// parede nas bordas, e por isso nem caminho nem ponto de interesse se lia
    /// na tela. Aqui a mesma árvore muda de lugar (não some: o custo é o mesmo)
    /// e vai formar três coisas — parede fechada nas laterais, que é o que
    /// aparece nos cantos da tela; grupos apertando o caminho em pontos
    /// escolhidos, que viram porta; e clareira em volta do que importa.
    static void Reorganizar(Transform nivel)
    {
        var arv = nivel.Find("Arvores");
        if (arv == null) return;
        var ps = PontosDeJogo(nivel);
        var rnd = new System.Random(7);
        int copaLayer = LayerMask.NameToLayer("Copa");
        var ocupadas = new List<Vector2>();
        foreach (Transform t in arv)
        {
            var q = t.position;
            if (Mathf.Abs(q.x) > 9f && Mathf.Abs(q.x) <= JX) ocupadas.Add(new Vector2(q.x, q.z));
        }

        // portas: pares de grupos apertando a rota, com vão de uns 5 m
        var portas = new List<Vector2>();
        foreach (float z in new[] { -27f, -22f, -14f, -6f, 3f, 12f, 22f, 30f, 36f })
        {
            Vector2 a = PontoDaTrilha(z), dir = DirecaoDaTrilha(z);
            var lado = new Vector2(-dir.y, dir.x);
            portas.Add(a + lado * 3.4f);
            portas.Add(a - lado * 3.4f);
        }

        int movidas = 0, mantidas = 0, presas = 0;
        foreach (Transform t in arv)
        {
            if (!t.gameObject.activeSelf) t.gameObject.SetActive(true);
            var p = t.position;
            if (Mathf.Abs(p.x) > JX || p.z < JZmin || p.z > JZmax) { mantidas++; continue; }

            bool temCopa = false;
            foreach (var r in t.GetComponentsInChildren<Renderer>(true))
                if (r.gameObject.layer == copaLayer) { temCopa = true; break; }

            float dt = DistTrilha(p.x, p.z);
            // só sai de cima da trilha: a mata vem até a beira. Abrir 3,6 m de
            // cada lado fazia a trilha parecer um corredor vazio.
            float chance = dt < 1.8f ? 1f : 0f;
            // clareira em volta de rádio, portão, rochedo, extração e postos
            if (!Livre(p.x, p.z, ps, 1.5f) && dt > 1.8f) chance = Mathf.Max(chance, 0.7f);
            if (rnd.NextDouble() > chance) { mantidas++; continue; }

            Vector2 destino = Vector2.zero;
            bool achou = false;
            // 80 tentativas: nas 40 últimas o espaçamento é dispensado — árvore
            // em cima da trilha é pior que árvore perto de outra
            for (int tent = 0; tent < 80 && !achou; tent++)
            {
                if (rnd.NextDouble() < 0.3 && portas.Count > 0)
                {
                    var c = portas[rnd.Next(portas.Count)];
                    destino = c + new Vector2(Rnd(rnd, -1.5f, 1.5f), Rnd(rnd, -1.6f, 1.6f));
                }
                else
                {
                    float sinal = rnd.NextDouble() < 0.5 ? -1f : 1f;
                    // árvore de copa fica no anel de dentro: é a copa dela que
                    // entra pelo canto da tela e fecha o quadro por cima
                    float a = temCopa ? 9.4f : 12.5f, b2 = temCopa ? 12.6f : 17.4f;
                    destino = new Vector2(sinal * Rnd(rnd, a, b2), Rnd(rnd, JZmin + 0.5f, JZmax - 0.5f));
                }
                if (DistTrilha(destino.x, destino.y) < 2.2f) continue;
                if (Mathf.Abs(destino.y - ZCorrego(destino.x)) < LarguraCorrego(destino.x) + 1.2f) continue;
                if (!Livre(destino.x, destino.y, ps, 0.6f)) continue;
                // espaçamento: mata de verdade não é pilha de tronco encostado
                bool perto = false;
                if (tent < 40) foreach (var o in ocupadas) if ((o - destino).sqrMagnitude < 2.1f * 2.1f) { perto = true; break; }
                if (perto) continue;
                achou = true;
            }
            if (!achou) { mantidas++; presas++; continue; }

            ocupadas.Add(destino);
            t.position = new Vector3(destino.x, p.y, destino.y);
            t.rotation = Quaternion.Euler(0, Rnd(rnd, 0, 360), 0);
            EditorUtility.SetDirty(t);
            movidas++;
        }
        Debug.Log("[FlorestaSelva] mata: " + movidas + " árvores tiradas do caminho, " + mantidas + " no lugar, " + presas + " sem lugar pra ir");

        // O contrário também: puxa árvore da parede de fora (onde sobra) pra beira
        // das trilhas. Rodadas antigas abriram 3,6 m de cada lado e o construtor só
        // sabia afastar — o corredor vazio ficava. Só preenche o que falta, então
        // rodar de novo não embaralha tudo.
        var sobra = new List<Transform>();
        var naBeira = new List<Vector2>();
        foreach (Transform t in arv)
        {
            var q = new Vector2(t.position.x, t.position.z);
            if (Mathf.Abs(q.x) > JX || q.y < JZmin || q.y > JZmax) continue;
            float d = DistTrilha(q.x, q.y);
            if (d >= 1.8f && d <= 5f) naBeira.Add(q);
            else if (d > 7f && Mathf.Abs(q.x) > 11f && Livre(q.x, q.y, ps, 0f)) sobra.Add(t);
        }
        for (int i = sobra.Count - 1; i > 0; i--) { int j = rnd.Next(i + 1); var x = sobra[i]; sobra[i] = sobra[j]; sobra[j] = x; }
        int puxadas = 0, proxima = 0;
        foreach (var tr in Suaves)
            for (int i = 0; i < tr.Length && proxima < sobra.Count; i += 3)
            {
                var c = tr[i];
                if (Mathf.Abs(c.x) > JX - 1f || c.y < JZmin + 1f || c.y > JZmax - 1f) continue;
                var dir = i + 1 < tr.Length ? (tr[i + 1] - c).normalized : Vector2.up;
                foreach (float lado in new[] { -1f, 1f })
                {
                    if (rnd.NextDouble() < 0.45 || proxima >= sobra.Count) continue;
                    var alvo = c + new Vector2(-dir.y, dir.x) * lado * Rnd(rnd, 2.3f, 4.6f);
                    if (DistTrilha(alvo.x, alvo.y) < 2.2f) continue;
                    if (Mathf.Abs(alvo.x) > JX - 0.5f) continue;
                    if (Mathf.Abs(alvo.y - ZCorrego(alvo.x)) < LarguraCorrego(alvo.x) + 1.2f) continue;
                    if (!Livre(alvo.x, alvo.y, ps, 0.6f)) continue;
                    bool perto = false;
                    foreach (var o in naBeira) if ((o - alvo).sqrMagnitude < 2.4f * 2.4f) { perto = true; break; }
                    if (perto) continue;
                    var t = sobra[proxima++];
                    t.position = new Vector3(alvo.x, t.position.y, alvo.y);
                    t.rotation = Quaternion.Euler(0, Rnd(rnd, 0, 360), 0);
                    EditorUtility.SetDirty(t);
                    naBeira.Add(alvo);
                    puxadas++;
                }
            }
        Debug.Log("[FlorestaSelva] mata na beira da trilha: " + puxadas + " árvores puxadas da parede de fora");
    }

    static Vector2 PontoDaTrilha(float z)
    {
        var t = Trilhas[0];
        for (int i = 0; i + 1 < t.Length; i++)
            if ((z >= t[i].y && z <= t[i + 1].y) || (z <= t[i].y && z >= t[i + 1].y))
            {
                float k = Mathf.InverseLerp(t[i].y, t[i + 1].y, z);
                return Vector2.Lerp(t[i], t[i + 1], k);
            }
        return new Vector2(0, z);
    }

    static Vector2 DirecaoDaTrilha(float z)
    {
        var a = PontoDaTrilha(z - 1.5f); var b = PontoDaTrilha(z + 1.5f);
        var d = b - a;
        return d.sqrMagnitude < 0.0001f ? Vector2.up : d.normalized;
    }

    /// Some com a planta rasteira que caiu dentro do córrego, no fundo de
    /// cratera ou em cima da trilha — e devolve as outras. Numa passada só,
    /// pra rodar de novo com outro traçado religar o que voltou a valer.
    static void LimparChao(Transform nivel)
    {
        int agua = 0, trilha = 0;
        foreach (var g in new[] { "Folhagem", "Rasteira", "Vegetacao" })
        {
            var grupo = nivel.Find(g); if (grupo == null) continue;
            foreach (Transform t in grupo)
            {
                var p = t.position;
                bool naAgua = Mathf.Abs(p.z - ZCorrego(p.x)) < LarguraCorrego(p.x) + 0.5f;
                foreach (var c in Crateras) if (Vector2.Distance(new Vector2(p.x, p.z), new Vector2(c.x, c.y)) < c.z * 0.6f) naAgua = true;
                // o esconderijo de mato pode encostar na trilha, mas não ficar em cima
                float limite = g == "Vegetacao" ? 1.1f : 0.95f;
                bool naTrilha = DistTrilha(p.x, p.z) < limite;
                if (t.gameObject.activeSelf == (naAgua || naTrilha))
                {
                    t.gameObject.SetActive(!(naAgua || naTrilha));
                    EditorUtility.SetDirty(t.gameObject);
                }
                if (naAgua) agua++; else if (naTrilha) trilha++;
            }
        }
        // o mato rasteiro que já estava na cena e fica colado na trilha também
        // vai pra sombra: era ele que clareava a margem perto do jogador
        var escuros = new Dictionary<Material, Material>();
        int sombra = 0;
        foreach (var g in new[] { "Folhagem", "Rasteira" })
        {
            var grupo = nivel.Find(g); if (grupo == null) continue;
            foreach (Transform t in grupo)
            {
                if (!t.gameObject.activeSelf) continue;
                float d = DistTrilha(t.position.x, t.position.z);
                if (d < 0.95f || d > 2.8f) continue;
                Escurecer(t.gameObject, escuros);
                sombra++;
            }
        }
        Debug.Log("[FlorestaSelva] fora da água: " + agua + ", fora da trilha: " + trilha + ", na sombra da margem: " + sombra);
    }

    /// Troca a árvore de desenho (folha em quadrado verde chapado) pela mata
    /// de selva, escolhendo a espécie por zona — como numa mata de verdade,
    /// onde a diversidade também tem regra: pau d'água na beira do córrego,
    /// copa alta na encosta, árvore morta na cratera. A espécie vem de um
    /// ruído de baixa frequência, então ela aparece em manchas, não sorteada
    /// uma a uma.
    ///
    /// O tronco (cápsula com colisor) não muda: cobertura e navegação ficam iguais.
    static void TrocarEspecies(Transform nivel)
    {
        var arv = nivel.Find("Arvores");
        if (arv == null) return;

        var beiraDagua = new[] { P("Assets/Coconut Palm Tree Pack/Prefabs/Coconut_Palm_Tree03.prefab"),
                                 P("Assets/Coconut Palm Tree Pack/Prefabs/Coconut_Palm_Tree07.prefab"),
                                 P("Assets/ForestVision/Optimized/Valley Fern Tree.prefab") };
        var copaAlta = new[] { P("Assets/ForestVision/Optimized/Opt_TallJungle.prefab"),
                               P("Assets/ForestVision/Optimized/Opt_Kapok.prefab"),
                               P("Assets/ForestVision/Optimized/Opt_Oaky.prefab") };
        var medias = new[] { P("Assets/ForestVision/Optimized/Opt_SmallBranching.prefab"),
                             P("Assets/ForestVision/Optimized/Opt_Tall Forest 1.prefab"),
                             P("Assets/ForestVision/Optimized/Valley Fern Tree.prefab") };
        // cratera: toco partido, não árvore seca. A galhada retorcida da DeadTree
        // vista de cima virava um emaranhado cinza-esverdeado por todo lado.
        var secas = new[] { P(PH + "tree_stump_01/tree_stump_01.fbx"),
                            P(PH + "tree_stump_02/tree_stump_02.fbx") };

        int trocadas = 0, mantidas = 0, copas = 0;
        int copaLayer = LayerMask.NameToLayer("Copa");
        var rnd = new System.Random(21);
        foreach (Transform t in arv)
        {
            // o "Visual" É o modelo (instância de prefab), não um pai dele
            var vis = t.Find("Visual");
            if (vis == null) { mantidas++; continue; }
            var atual = PrefabUtility.GetCorrespondingObjectFromSource(vis.gameObject);
            if (atual == null) { mantidas++; continue; }

            float crat = 99f;
            foreach (var c in Crateras) crat = Mathf.Min(crat, Vector2.Distance(new Vector2(t.position.x, t.position.z), new Vector2(c.x, c.y)) - c.z);
            // CommonTree (folha quadrada), DeadTree (galhada retorcida) e pachira
            // (4 plantas de vaso enfileiradas no mesmo arquivo, tronco trançado)
            // saem todas. O resto fica.
            bool feia = atual.name.StartsWith("CommonTree") || atual.name.StartsWith("DeadTree") || atual.name.StartsWith("pachira");
            if (!feia)
            {
                // folha de pachira tem que estar na camada Copa, senão ela não
                // abre quando o jogador passa embaixo
                foreach (var r in vis.GetComponentsInChildren<Renderer>(true))
                    if (r.name.Contains("leaves") && copaLayer >= 0 && r.gameObject.layer != copaLayer)
                    { r.gameObject.layer = copaLayer; EditorUtility.SetDirty(r.gameObject); copas++; }
                mantidas++;
                continue;
            }

            var p = t.position;
            float dc = Mathf.Abs(p.z - ZCorrego(p.x)) - LarguraCorrego(p.x);
            float naCratera = 99f;
            foreach (var c in Crateras) naCratera = Mathf.Min(naCratera, Vector2.Distance(new Vector2(p.x, p.z), new Vector2(c.x, c.y)) - c.z);
            float mancha = Mathf.PerlinNoise(p.x * 0.045f + 100f, p.z * 0.045f + 50f);

            GameObject[] grupo;
            if (naCratera < 2.5f) grupo = secas;
            else if (dc < 4.5f) grupo = beiraDagua;
            else if (mancha < 0.42f) grupo = copaAlta;
            else if (mancha < 0.72f) grupo = medias;
            else grupo = copaAlta;

            var pref = grupo[(trocadas + (int)(mancha * 7)) % grupo.Length];
            if (pref == null) { mantidas++; continue; }

            bool ehToco = grupo == secas;
            float altura = ehToco ? Rnd(rnd, 0.7f, 1.2f) : Mathf.Max(3f, t.lossyScale.y) * Rnd(rnd, 2.1f, 3.1f);
            var pai = vis.parent;
            Object.DestroyImmediate(vis.gameObject);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(pref, pai);
            go.name = "Visual";
            SemColisor(go);
            go.transform.localRotation = Quaternion.Euler(0, Rnd(rnd, 0, 360), 0);
            // o pai é a cápsula esticada do tronco: escala uniforme no filho
            // entortaria a árvore, então desfaz a escala do pai primeiro
            var ls = t.lossyScale;
            var inv = new Vector3(1f / Mathf.Max(0.0001f, ls.x), 1f / Mathf.Max(0.0001f, ls.y), 1f / Mathf.Max(0.0001f, ls.z));
            go.transform.localScale = inv;
            go.transform.position = t.position;
            var b = Limites(go);
            float e = b.size.y > 0.01f ? altura / b.size.y : 1f;
            go.transform.localScale = inv * e;
            b = Limites(go);
            go.transform.position += new Vector3(t.position.x - b.center.x, 0f, t.position.z - b.center.z);
            go.transform.position = new Vector3(go.transform.position.x, t.position.y - t.lossyScale.y * 0.5f - 0.15f, go.transform.position.z);
            // folha da árvore nova também entra na camada Copa
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                bool folha = false;
                foreach (var m in r.sharedMaterials) if (m != null && m.renderQueue == 2450) folha = true;
                if (folha && copaLayer >= 0) r.gameObject.layer = copaLayer;
            }
            trocadas++;
        }
        Debug.Log("[FlorestaSelva] espécies: " + trocadas + " árvores de desenho trocadas por mata de selva, " + mantidas + " mantidas, " + copas + " folhas postas na camada Copa");
    }

    /// Manchas 2D no chão: folhiço, musgo, terra batida e raiz, em quadrados
    /// deitados. É o que quebra o chão liso sem custar geometria — a mesma
    /// ideia das manchas pintadas de Darkwood.
    static void Decalques(Transform pai)
    {
        var mats = new[] { MaterialDeChao("Folhas", "gnd_Leaves.png", 0.55f), MaterialDeChao("Musgo", "gnd_MossyRock_Base_Color.png", 0.5f),
                           MaterialDeChao("Terra", "gnd_ForestDirt_basecolor.png", 0.6f), MaterialDeChao("Raiz", "gnd_ForestRooty_color.png", 0.5f) };
        var malha = MalhaDeQuadrado();
        var g = new GameObject("Manchas").transform;
        g.SetParent(pai, false);
        var rnd = new System.Random(55);
        int n = 0;
        for (int tent = 0; tent < 1400 && n < 340; tent++)
        {
            float x = Rnd(rnd, -JX + 0.5f, JX - 0.5f), z = Rnd(rnd, JZmin + 0.5f, JZmax - 0.5f);
            if (Mathf.Abs(z - ZCorrego(x)) < LarguraCorrego(x) - 0.2f) continue;
            float dtm = DistTrilha(x, z);
            if (dtm > 1.1f && dtm < 1.9f) continue; // mancha clara na margem apagava a borda
            var go = new GameObject("Mancha", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(g, false);
            float y = H(x, z);
            go.transform.position = new Vector3(x, y + 0.03f, z);
            float ex = 0.4f;
            var nrm = Vector3.Cross(new Vector3(0, H(x, z + ex) - H(x, z - ex), 2 * ex), new Vector3(2 * ex, H(x + ex, z) - H(x - ex, z), 0)).normalized;
            if (nrm.y < 0) nrm = -nrm;
            go.transform.rotation = Quaternion.FromToRotation(Vector3.up, nrm) * Quaternion.Euler(0, Rnd(rnd, 0, 360), 0);
            float tam = Rnd(rnd, 1.3f, 3.4f);
            go.transform.localScale = new Vector3(tam, 1f, tam * Rnd(rnd, 0.7f, 1.3f));
            go.GetComponent<MeshFilter>().sharedMesh = malha;
            var r = go.GetComponent<MeshRenderer>();
            // trilha ganha mancha de terra; o resto varia por ruído
            bool naTrilha = DistTrilha(x, z) < 1.1f;
            // mancha de terra só na trilha; fora dela folhiço, musgo e raiz
            int qual = (int)(Mathf.PerlinNoise(x * 0.09f, z * 0.09f) * 2.99f);
            r.sharedMaterial = naTrilha ? mats[2] : mats[qual == 2 ? 3 : qual];
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
            n++;
        }
        Debug.Log("[FlorestaSelva] manchas de chão: " + n);
    }

    static UnityEngine.Mesh MalhaDeQuadrado()
    {
        const string arq = Pasta + "/Quadrado.asset";
        var m = AssetDatabase.LoadAssetAtPath<UnityEngine.Mesh>(arq);
        if (m != null) return m;
        m = new UnityEngine.Mesh { name = "Quadrado" };
        m.SetVertices(new List<Vector3> { new Vector3(-0.5f, 0, -0.5f), new Vector3(-0.5f, 0, 0.5f), new Vector3(0.5f, 0, 0.5f), new Vector3(0.5f, 0, -0.5f) });
        m.SetUVs(0, new List<Vector2> { new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0) });
        m.SetTriangles(new[] { 0, 1, 2, 0, 2, 3 }, 0);
        m.RecalculateNormals(); m.RecalculateBounds();
        AssetDatabase.CreateAsset(m, arq);
        return m;
    }

    /// Recorta uma mancha da textura de chão: a cor vem da textura, o alfa é
    /// um borrão de ruído. Sem isso a mancha seria um quadrado com cara de
    /// adesivo.
    static Material MaterialDeChao(string nome, string origem, float dureza)
    {
        string tex = Pasta + "/T_Mancha_" + nome + ".png";
        if (AssetDatabase.LoadAssetAtPath<Texture2D>(tex) == null)
        {
            const string d = "Assets/ForestVision/Materials/Grounds/Textures/";
            var ti = AssetImporter.GetAtPath(d + origem) as TextureImporter;
            bool liaAntes = ti != null && ti.isReadable;
            if (ti != null && !liaAntes) { ti.isReadable = true; ti.SaveAndReimport(); }
            var fonte = AssetDatabase.LoadAssetAtPath<Texture2D>(d + origem);
            const int N = 256;
            var t = new Texture2D(N, N, TextureFormat.RGBA32, false);
            float ox = Random.Range(0f, 10f), oz = Random.Range(0f, 10f);
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    var c = fonte != null ? fonte.GetPixelBilinear((x + ox * 13f) / N * 0.6f, (y + oz * 13f) / N * 0.6f) : Color.gray;
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(N / 2f, N / 2f)) / (N / 2f);
                    float borda = 1f - Mathf.SmoothStep(dureza, 1f, dist);
                    float ruido = Mathf.PerlinNoise(x * 0.035f + ox, y * 0.035f + oz);
                    c.a = Mathf.Clamp01(borda * (ruido * 1.5f - 0.25f) * 1.6f);
                    t.SetPixel(x, y, c);
                }
            t.Apply();
            File.WriteAllBytes(Path.GetFullPath(tex), t.EncodeToPNG());
            AssetDatabase.ImportAsset(tex);
            var ti2 = (TextureImporter)AssetImporter.GetAtPath(tex);
            ti2.alphaIsTransparency = true;
            ti2.SaveAndReimport();
            if (ti != null && !liaAntes) { ti.isReadable = false; ti.SaveAndReimport(); }
        }
        string arq = Pasta + "/M_Mancha_" + nome + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(arq);
        if (m == null)
        {
            m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(m, arq);
        }
        m.SetFloat("_Surface", 1f);
        m.SetFloat("_Blend", 0f);
        m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_ZWrite", 0f);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.SetOverrideTag("RenderType", "Transparent");
        m.renderQueue = 2800;
        m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(tex));
        m.SetColor("_BaseColor", Color.white);
        m.SetFloat("_Smoothness", 0.05f);
        EditorUtility.SetDirty(m);
        return m;
    }

    /// Galho pendurado na altura da câmera, nas laterais do corredor: é o que
    /// fecha o canto da tela e dá teto de mata fechada. Vai na camada Copa,
    /// então abre quando o jogador passa por baixo.
    static void Galhos(Transform pai, Transform nivel)
    {
        var pref = P("Assets/ForestVision/Prefabs/BranchesGroup.prefab");
        if (pref == null) return;
        var g = new GameObject("GalhosDeCima").transform;
        g.SetParent(pai, false);
        var rnd = new System.Random(88);
        int copa = LayerMask.NameToLayer("Copa");
        var ps = PontosDeJogo(nivel);
        var mat = MaterialDeGalho();
        int n = 0;
        for (float z = JZmin + 4f; z < JZmax - 3f; z += Rnd(rnd, 5f, 9f))
        {
            for (int lado = -1; lado <= 1; lado += 2)
            {
                if (rnd.NextDouble() < 0.22) continue;
                var centro = PontoDaTrilha(z);
                float x = centro.x + lado * Rnd(rnd, 3.6f, 7.5f);
                if (Mathf.Abs(x) > JX - 1.5f) continue;
                if (!Livre(x, z, ps, 0.5f)) continue;
                var go = (GameObject)PrefabUtility.InstantiatePrefab(pref, g);
                SemColisor(go);
                go.transform.position = new Vector3(x, H(x, z) + Rnd(rnd, 3.4f, 5.2f), z);
                // inclina pra dentro do corredor, como galho que avança sobre a trilha
                go.transform.rotation = Quaternion.Euler(Rnd(rnd, -18f, 18f), lado > 0 ? Rnd(rnd, 180f, 260f) : Rnd(rnd, 0f, 80f), Rnd(rnd, -25f, -8f) * lado);
                go.transform.localScale = Vector3.one * Rnd(rnd, 0.5f, 0.9f);
                foreach (var r in go.GetComponentsInChildren<Renderer>())
                {
                    r.gameObject.layer = copa;
                    var ms = r.sharedMaterials;
                    for (int k = 0; k < ms.Length; k++) ms[k] = mat;
                    r.sharedMaterials = ms;
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
                n++;
            }
        }
        Debug.Log("[FlorestaSelva] galhos por cima: " + n);
    }

    static Material MaterialDeGalho()
    {
        const string arq = Pasta + "/M_Galho.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(arq);
        if (m == null)
        {
            var orig = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ForestVision/Prefabs/BranchesGroup.prefab");
            var r = orig != null ? orig.GetComponentInChildren<Renderer>() : null;
            m = r != null && r.sharedMaterial != null ? new Material(r.sharedMaterial) : new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(m, arq);
        }
        // o galho vem outonal, amarelo: aqui é selva de noite
        m.SetColor("_BaseColor", new Color(0.3f, 0.36f, 0.26f));
        EditorUtility.SetDirty(m);
        return m;
    }

    /// Silhueta de copa vista de baixo pra cima, como em Darkwood: fotografa a
    /// árvore do projeto de cima, recorta pelo fundo e pinta de verde quase
    /// preto. Fica com o formato real da folhagem, não um borrão.
    static Texture2D Silhueta(string nome, string prefab, float giro) => Silhueta(nome, prefab, giro, false);

    public static Texture2D Silhueta(string nome, string prefab, float giro, bool refazer)
    {
        string arq = Pasta + "/T_Copa_" + nome + ".png";
        var pronta = AssetDatabase.LoadAssetAtPath<Texture2D>(arq);
        if (pronta != null && !refazer) return pronta;
        var pref = AssetDatabase.LoadAssetAtPath<GameObject>(prefab);
        if (pref == null) { Debug.LogWarning("[FlorestaSelva] sem modelo pra silhueta: " + prefab); return null; }

        // Fotografa em 1536 e reduz pra 512 (3x3 por pixel).
        //
        // A primeira versão fotografava direto em 512 e recortava com corte
        // seco: saía um ADESIVO PRETO, miolo 100% opaco e borda serrilhada —
        // de perto, papel recortado. A folhagem de verdade, encostada na
        // lente, é borrada, rala nas pontas e deixa passar um fio de luz.
        const int SUPER = 1536;
        const int N = 512;
        const int passo = SUPER / N;
        const int camadaFoto = 31;
        var go = (GameObject)PrefabUtility.InstantiatePrefab(pref);
        go.transform.position = new Vector3(5000f, 0f, 5000f);
        go.transform.rotation = Quaternion.Euler(0f, giro, 0f);
        foreach (var tr in go.GetComponentsInChildren<Transform>(true)) tr.gameObject.layer = camadaFoto;
        var b = Limites(go);

        var camGo = new GameObject("fotoCopa");
        var cam = camGo.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = Mathf.Max(b.size.x, b.size.z) * 0.52f;
        cam.transform.position = new Vector3(b.center.x, b.max.y + 10f, b.center.z);
        cam.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        cam.nearClipPlane = 0.1f; cam.farClipPlane = b.size.y + 30f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        // fundo magenta: nada da mata tem essa cor, então dá pra recortar por ela
        cam.backgroundColor = new Color(1f, 0f, 1f, 1f);
        cam.cullingMask = 1 << camadaFoto;
        var sol = new GameObject("solFoto").AddComponent<Light>();
        sol.type = LightType.Directional; sol.intensity = 1.2f; sol.cullingMask = 1 << camadaFoto;
        sol.transform.rotation = Quaternion.Euler(60f, 30f, 0f);

        var rt = new RenderTexture(SUPER, SUPER, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
        cam.targetTexture = rt;
        bool neb = RenderSettings.fog; RenderSettings.fog = false;
        cam.Render();
        RenderSettings.fog = neb;
        cam.targetTexture = null;
        var prev = RenderTexture.active; RenderTexture.active = rt;
        var grande = new Texture2D(SUPER, SUPER, TextureFormat.RGBA32, false);
        grande.ReadPixels(new Rect(0, 0, SUPER, SUPER), 0, 0);
        grande.Apply();
        RenderTexture.active = prev;
        Object.DestroyImmediate(go); Object.DestroyImmediate(camGo); Object.DestroyImmediate(sol.gameObject);
        rt.Release();

        // 1. cobertura e luz da folha, ainda em alta, reduzidas por média:
        //    é a média que dá borda com meio-tom em vez de escada
        var alto = grande.GetPixels32();
        Object.DestroyImmediate(grande);
        var cobertura = new float[N * N];
        var luzes = new float[N * N];
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float soma = 0f, luz = 0f;
                for (int sy = 0; sy < passo; sy++)
                    for (int sx = 0; sx < passo; sx++)
                    {
                        var c = alto[(y * passo + sy) * SUPER + (x * passo + sx)];
                        float vr = c.r / 255f, vg = c.g / 255f, va = c.b / 255f;
                        float dist = Mathf.Sqrt((vr - 1f) * (vr - 1f) + vg * vg + (va - 1f) * (va - 1f));
                        float cob = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.16f, 0.52f, dist));
                        soma += cob;
                        luz += vg * cob;
                    }
                int i = y * N + x;
                cobertura[i] = soma / (passo * passo);
                luzes[i] = soma > 0.001f ? Mathf.Clamp01(luz / soma * 1.6f) : 0f;
            }

        // 2. dois borrões: um curto (a folha encostada na lente está fora de
        //    foco) e um largo, que mede a DENSIDADE local
        var suave = Borrar(cobertura, N, 2);
        var largo = Borrar(cobertura, N, 9);
        var luzSuave = Borrar(luzes, N, 3);

        var px = new Color[N * N];
        for (int i = 0; i < px.Length; i++)
        {
            float a = Mathf.Lerp(cobertura[i], suave[i], 0.7f);
            // a franja fina (folhinha solta na ponta do galho) rareia em vez
            // de terminar numa borda de tesoura
            a *= 0.28f + 0.72f * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.08f, 0.5f, largo[i]));
            // onde a folha pegou luz, o miolo abre e passa um fio de céu:
            // é o que tira o aspecto de mancha chapada
            a *= 1f - 0.42f * luzSuave[i];
            a = Mathf.Clamp01(a);

            // verde quase preto, com um resto de vida onde bateu luz: preto
            // puro vira buraco recortado na tela
            float v = 0.45f + 1.55f * luzes[i];
            px[i] = new Color(0.009f * v, 0.016f * v, 0.011f * v, a);
        }

        var tx = new Texture2D(N, N, TextureFormat.RGBA32, false);
        tx.SetPixels(px);
        tx.Apply();
        File.WriteAllBytes(Path.GetFullPath(arq), tx.EncodeToPNG());
        AssetDatabase.ImportAsset(arq);
        var ti = (TextureImporter)AssetImporter.GetAtPath(arq);
        ti.alphaIsTransparency = true;
        ti.wrapMode = TextureWrapMode.Clamp;
        ti.mipmapEnabled = true;                 // a copa é vista em tamanhos muito diferentes
        ti.filterMode = FilterMode.Trilinear;
        ti.anisoLevel = 4;
        ti.SaveAndReimport();
        Object.DestroyImmediate(tx);
        return AssetDatabase.LoadAssetAtPath<Texture2D>(arq);
    }

    /// Borrão separável (duas passadas de média). Serve pra tirar a borda de
    /// tesoura e pra medir densidade local da folhagem.
    static float[] Borrar(float[] fonte, int n, int raio)
    {
        var meio = new float[fonte.Length];
        var fim = new float[fonte.Length];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float s = 0f; int c = 0;
                for (int k = -raio; k <= raio; k++)
                {
                    int xx = x + k;
                    if (xx < 0 || xx >= n) continue;
                    s += fonte[y * n + xx]; c++;
                }
                meio[y * n + x] = s / c;
            }
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float s = 0f; int c = 0;
                for (int k = -raio; k <= raio; k++)
                {
                    int yy = y + k;
                    if (yy < 0 || yy >= n) continue;
                    s += meio[yy * n + x]; c++;
                }
                fim[y * n + x] = s / c;
            }
        return fim;
    }

    /// Refaz só as silhuetas de copa, sem remontar a floresta inteira: os
    /// materiais apontam pro mesmo arquivo, então trocar o PNG basta.
    public static void RefazerSilhuetas()
    {
        var lista = new (string, string, float)[] {
            ("Kapok", "Assets/ForestVision/Optimized/Opt_Kapok.prefab", 30f),
            ("Oaky", "Assets/ForestVision/Optimized/Opt_Oaky.prefab", 0f),
            ("Palmeira", "Assets/Coconut Palm Tree Pack/Prefabs/Coconut_Palm_Tree03.prefab", 0f),
            ("Samambaia", "Assets/ForestVision/Optimized/Valley Fern Tree.prefab", 0f),
            ("Galhos", "Assets/ForestVision/Prefabs/BranchesGroup.prefab", 0f),
        };
        int n = 0;
        foreach (var (nome, pr, g) in lista)
            if (Silhueta(nome, pr, g, true) != null) n++;
        AssetDatabase.SaveAssets();
        Debug.Log("[FlorestaSelva] " + n + " silhuetas de copa refeitas");
    }

    static Material MaterialDeCopa(Texture2D tex)
    {
        string arq = Pasta + "/M_Copa_" + tex.name + ".mat";
        var sh = Shader.Find("Ecos/Silhueta");
        var m = AssetDatabase.LoadAssetAtPath<Material>(arq);
        if (m == null) { m = new Material(sh); AssetDatabase.CreateAsset(m, arq); }
        // shader próprio: com o Unlit do URP a fila voltava pra 3000 e a
        // escuridão do cone (3100) pintava por cima da copa
        m.shader = sh;
        m.renderQueue = -1;
        m.SetTexture("_BaseMap", tex);
        m.SetColor("_BaseColor", Color.white);
        EditorUtility.SetDirty(m);
        return m;
    }

    /// Duas camadas de primeiro plano sobre o mapa:
    ///   copa (fator 0,35) — em cima das árvores de verdade da borda, então a
    ///   sombra escura casa com onde a mata está;
    ///   galho (fator 0,8) — mais perto da lente, maior, só nas laterais: é o
    ///   que atravessa o canto da tela e passa rápido quando o jogador anda.
    static void Paralaxe(Transform pai, Transform nivel)
    {
        var copas = new List<Texture2D>();
        foreach (var (n, pr, g) in new (string, string, float)[] {
            ("Kapok", "Assets/ForestVision/Optimized/Opt_Kapok.prefab", 30f),
            ("Oaky", "Assets/ForestVision/Optimized/Opt_Oaky.prefab", 0f),
            ("Palmeira", "Assets/Coconut Palm Tree Pack/Prefabs/Coconut_Palm_Tree03.prefab", 0f),
            ("Samambaia", "Assets/ForestVision/Optimized/Valley Fern Tree.prefab", 0f) })
        {
            var t = Silhueta(n, pr, g);
            if (t != null) copas.Add(t);
        }
        var galho = Silhueta("Galhos", "Assets/ForestVision/Prefabs/BranchesGroup.prefab", 0f);
        if (copas.Count == 0) return;

        var raiz = new GameObject("CopaParalaxe");
        raiz.transform.SetParent(pai, false);
        var cp = raiz.AddComponent<CopaParalaxe>();
        var malha = MalhaDeQuadrado();
        var rnd = new System.Random(303);
        var ocupados = new List<Vector2>();

        GameObject Quad(Texture2D tex, string nome)
        {
            var go = new GameObject(nome, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(raiz.transform, false);
            go.GetComponent<MeshFilter>().sharedMesh = malha;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = MaterialDeCopa(tex);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return go;
        }

        void Add(Vector2 a, Texture2D tex, float fator, float altura, float tam, float alfa, string nome)
        {
            var go = Quad(tex, nome);
            go.transform.localScale = new Vector3(tam, 1f, tam);
            go.transform.position = new Vector3(a.x, H(a.x, a.y) + altura, a.y);
            cp.folhas.Add(new CopaParalaxe.Folha
            {
                t = go.transform, ancora = new Vector3(a.x, 0f, a.y), fator = fator, altura = altura,
                alfa = alfa, giro = Rnd(rnd, 0f, 360f), fase = Rnd(rnd, 0f, 6.28f)
            });
        }

        bool Longe(Vector2 a, float d)
        {
            foreach (var o in ocupados) if ((o - a).sqrMagnitude < d * d) return false;
            return true;
        }

        // A paralaxe EMPURRA a silhueta pra fora: desenhada a (1 + fator) vezes a
        // distância real. Uma copa a 12 m do caminho aparecia a 16 m, fora da
        // tela (meia largura de 14 m). A âncora vai mais perto, já contando isso:
        // a copa (fator ~0,35) a 6–11 m do caminho cai a 8–15 m na tela; o galho
        // (fator ~0,8) a 5–8 m cai a 9–14 m — as duas na moldura.
        int nCopa = 0;
        for (float z = JZmin - 3f; z < JZmax + 3f; z += 3.2f)
            foreach (float lado in new[] { -1f, 1f })
            {
                if (rnd.NextDouble() < 0.15) continue;
                var c = PontoDaTrilha(Mathf.Clamp(z, JZmin, JZmax));
                var p = new Vector2(c.x + lado * Rnd(rnd, 6f, 11f), z + Rnd(rnd, -1.4f, 1.4f));
                if (!Longe(p, 3.6f)) continue;
                ocupados.Add(p);
                Add(p, copas[nCopa % copas.Count], Rnd(rnd, 0.28f, 0.42f), Rnd(rnd, 9f, 11f), Rnd(rnd, 7f, 11f), Rnd(rnd, 0.85f, 0.95f), "Copa");
                nCopa++;
            }

        // copa POR CIMA das trilhas: sem ela a trilha parece um corredor aberto
        // no meio da mata. Some em volta do jogador, então não tapa quem anda.
        int nSobre = 0;
        foreach (var t in Suaves)
            for (int i = 0; i < t.Length; i += 6)
            {
                var c = t[i];
                if (Mathf.Abs(c.x) > JX + 2f || c.y < JZmin - 2f || c.y > JZmax + 2f) continue;
                if (rnd.NextDouble() < 0.25) continue;
                var dir = i + 1 < t.Length ? (t[i + 1] - c).normalized : Vector2.up;
                var p = c + new Vector2(-dir.y, dir.x) * Rnd(rnd, -2.2f, 2.2f);
                if (!Longe(p, 3.2f)) continue;
                ocupados.Add(p);
                Add(p, copas[(nCopa + nSobre) % copas.Count], Rnd(rnd, 0.3f, 0.42f), Rnd(rnd, 9f, 11f), Rnd(rnd, 6f, 9f), Rnd(rnd, 0.82f, 0.92f), "CopaSobreTrilha");
                nSobre++;
            }
        nCopa += nSobre;

        int nGalho = 0;
        if (galho != null)
            for (float z = JZmin; z < JZmax; z += Rnd(rnd, 4f, 6.5f))
            {
                float lado = nGalho % 2 == 0 ? -1f : 1f;
                var c = PontoDaTrilha(z);
                var p = new Vector2(c.x + lado * Rnd(rnd, 5f, 8f), z);
                Add(p, galho, Rnd(rnd, 0.7f, 0.9f), Rnd(rnd, 15f, 17f), Rnd(rnd, 10f, 14f), 0.97f, "Galho");
                nGalho++;
            }

        EditorUtility.SetDirty(cp);
        Debug.Log("[FlorestaSelva] paralaxe: " + nCopa + " copas e " + nGalho + " galhos por cima da tela");
    }

    /// Pegada de bota: sola com salto, desenhada à mão numa textura pequena.
    /// Terra escura e úmida onde o peso afundou; borda suave.
    static Material MaterialDePegada()
    {
        const string tex = Pasta + "/T_Pegada.png";
        if (AssetDatabase.LoadAssetAtPath<Texture2D>(tex) == null)
        {
            const int Wd = 64, Ht = 160;
            var t = new Texture2D(Wd, Ht, TextureFormat.RGBA32, false);
            for (int y = 0; y < Ht; y++)
                for (int x = 0; x < Wd; x++)
                {
                    float u = (x + 0.5f) / Wd * 2f - 1f, v = (y + 0.5f) / Ht;
                    // sola (frente, mais larga) e salto (atrás), com vão entre eles
                    float sola = 1f - Mathf.Clamp01((Mathf.Pow(u / 0.9f, 2f) + Mathf.Pow((v - 0.68f) / 0.3f, 2f) - 0.75f) / 0.35f);
                    float salto = 1f - Mathf.Clamp01((Mathf.Pow(u / 0.78f, 2f) + Mathf.Pow((v - 0.17f) / 0.14f, 2f) - 0.75f) / 0.35f);
                    // cravos do solado: listras finas
                    float cravo = 0.75f + 0.25f * (Mathf.Repeat(v * 22f, 1f) > 0.5f ? 1f : 0f);
                    float a = Mathf.Max(sola, salto) * cravo * (0.8f + 0.2f * Mathf.PerlinNoise(x * 0.3f, y * 0.3f));
                    t.SetPixel(x, y, new Color(0.16f, 0.12f, 0.09f, Mathf.Clamp01(a) * 0.7f));
                }
            t.Apply();
            File.WriteAllBytes(Path.GetFullPath(tex), t.EncodeToPNG());
            AssetDatabase.ImportAsset(tex);
            var ti = (TextureImporter)AssetImporter.GetAtPath(tex);
            ti.alphaIsTransparency = true;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.SaveAndReimport();
        }
        const string arq = Pasta + "/M_Pegada.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(arq);
        if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, arq); }
        m.SetFloat("_Surface", 1f);
        m.SetFloat("_Blend", 0f);
        m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_ZWrite", 0f);
        m.SetFloat("_Cull", 0f);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.SetOverrideTag("RenderType", "Transparent");
        m.renderQueue = 2810;
        m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(tex));
        m.SetColor("_BaseColor", Color.white);
        // úmida: pega um brilho da lanterna, como o fundo de uma pegada molhada
        m.SetFloat("_Smoothness", 0.55f);
        EditorUtility.SetDirty(m);
        return m;
    }

    /// Troca os materiais por uma cópia em tom escuro (mesma textura). O mato
    /// colado na trilha fica na sombra: é o escuro dos lados que faz o chão
    /// pisado se destacar, não uma faixa clara pintada.
    static void Escurecer(GameObject go, Dictionary<Material, Material> cache)
    {
        foreach (var r in go.GetComponentsInChildren<Renderer>())
        {
            var ms = r.sharedMaterials;
            for (int k = 0; k < ms.Length; k++)
            {
                var m = ms[k];
                if (m == null || !m.HasProperty("_BaseColor") || m.name.StartsWith("M_Sombra_")) continue;
                if (!cache.TryGetValue(m, out var c))
                {
                    string arq = Pasta + "/M_Sombra_" + m.name.Replace("/", "_") + ".mat";
                    c = AssetDatabase.LoadAssetAtPath<Material>(arq);
                    if (c == null) { c = new Material(m); AssetDatabase.CreateAsset(c, arq); }
                    var b = m.GetColor("_BaseColor");
                    c.SetColor("_BaseColor", new Color(b.r * 0.38f, b.g * 0.42f, b.b * 0.36f, b.a));
                    if (c.HasProperty("_Smoothness")) c.SetFloat("_Smoothness", 0.05f);
                    EditorUtility.SetDirty(c);
                    cache[m] = c;
                }
                ms[k] = c;
            }
            r.sharedMaterials = ms;
        }
    }

    // ---------------------------------------------------------------- terreno

    static Terrain CriarTerreno(Transform pai)
    {
        const string arq = Pasta + "/Selva_TerrainData.asset";
        var td = AssetDatabase.LoadAssetAtPath<TerrainData>(arq);
        if (td == null) { td = new TerrainData(); AssetDatabase.CreateAsset(td, arq); }
        td.heightmapResolution = ResAltura;
        td.alphamapResolution = ResPintura;
        td.baseMapResolution = 1024;
        td.size = new Vector3(Largura, Altura, Comprimento);

        var alt = new float[ResAltura, ResAltura];
        for (int iz = 0; iz < ResAltura; iz++)
            for (int ix = 0; ix < ResAltura; ix++)
            {
                float x = X0 + Largura * ix / (ResAltura - 1f);
                float z = Z0 + Comprimento * iz / (ResAltura - 1f);
                alt[iz, ix] = (H(x, z) - Base) / Altura;
            }
        td.SetHeights(0, 0, alt);
        td.terrainLayers = Camadas();
        EditorUtility.SetDirty(td);

        var go = Terrain.CreateTerrainGameObject(td);
        go.name = "Terreno";
        go.transform.SetParent(pai, false);
        go.transform.position = new Vector3(X0, Base, Z0);
        var t = go.GetComponent<Terrain>();
        var mat = AssetDatabase.LoadAssetAtPath<Material>(Pasta + "/M_Terreno.mat");
        if (mat == null)
        {
            mat = new Material(Shader.Find("Universal Render Pipeline/Terrain/Lit"));
            AssetDatabase.CreateAsset(mat, Pasta + "/M_Terreno.mat");
        }
        t.materialTemplate = mat;
        t.heightmapPixelError = 3f;
        t.basemapDistance = 150f;
        t.drawInstanced = true;
        t.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.NavigationStatic | StaticEditorFlags.BatchingStatic);
        return t;
    }

    static TerrainLayer Camada(string nome, string cor, string normal, float tam, Color tinta, float liso = 0.26f)
    {
        string arq = Pasta + "/TL_" + nome + ".terrainlayer";
        var l = AssetDatabase.LoadAssetAtPath<TerrainLayer>(arq);
        if (l == null) { l = new TerrainLayer(); AssetDatabase.CreateAsset(l, arq); }
        const string d = "Assets/ForestVision/Materials/Grounds/Textures/";
        l.diffuseTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(d + cor);
        if (normal != null)
        {
            var ti = AssetImporter.GetAtPath(d + normal) as TextureImporter;
            if (ti != null && ti.textureType != TextureImporterType.NormalMap) { ti.textureType = TextureImporterType.NormalMap; ti.SaveAndReimport(); }
            l.normalMapTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(d + normal);
            l.normalScale = 1f;
        }
        l.tileSize = new Vector2(tam, tam);
        l.diffuseRemapMax = new Vector4(tinta.r, tinta.g, tinta.b, 1f);
        l.smoothness = liso;
        EditorUtility.SetDirty(l);
        return l;
    }

    // ordem importa: é o índice usado na pintura
    const int Serapilheira = 0, Cobertura = 1, TerraBatida = 2, Raizes = 3, Lama = 4, Rocha = 5, Musgo = 6, Terra = 7, Sombra = 8;

    static TerrainLayer[] Camadas() => new[]
    {
        Camada("Serapilheira", "gnd_Leaves.png", "gnd_Leaves_Normal.png", 4.5f, new Color(0.48f, 0.43f, 0.36f)),
        Camada("Cobertura", "gnd_CoveredWithLeaves_Base_Color.png", "gnd_CoveredWithLeaves_Normal.png", 3.5f, new Color(0.42f, 0.52f, 0.38f)),
        // compactada e úmida: reflete a lanterna, e o folhiço em volta não
        // terra fina e clara (a de raiz era marrom-média e sumia no folhiço):
        // medido, a trilha tinha só 1,08× o brilho do chão ao lado
        Camada("TerraBatida", "gnd_ForestDirt_basecolor.png", null, 5.5f, new Color(1.5f, 1.26f, 0.92f), 0.58f),
        Camada("Raizes", "gnd_ForestRooty_color.png", "gnd_ForestRooty_normal.png", 4f, new Color(0.64f, 0.54f, 0.43f)),
        Camada("Lama", "gnd_MuddyRock_Base_Color.png", "gnd_MuddyRock_Normal.png", 3f, new Color(0.55f, 0.53f, 0.47f)),
        Camada("Rocha", "gnd_ForestMossyRocks_basecolor.png", null, 5f, new Color(0.72f, 0.78f, 0.68f)),
        Camada("Musgo", "gnd_MossyRock_Base_Color.png", "gnd_MossyRock_Normal.png", 4f, new Color(0.42f, 0.52f, 0.28f)),
        Camada("Terra", "gnd_ForestDirt_basecolor.png", null, 4f, new Color(0.62f, 0.56f, 0.48f)),
        // folhagem baixa na sombra, colada na trilha: a mesma cobertura verde em
        // tom bem escuro. Medido: com a cobertura normal a margem brilhava MAIS
        // que a trilha sob a lanterna (0,89×).
        Camada("Sombra", "gnd_CoveredWithLeaves_Base_Color.png", "gnd_CoveredWithLeaves_Normal.png", 3.5f, new Color(0.2f, 0.25f, 0.18f)),
    };

    static void Pintar(TerrainData td)
    {
        int n = ResPintura, k = td.terrainLayers.Length;
        var a = new float[n, n, k];
        var w = new float[k];
        for (int iz = 0; iz < n; iz++)
            for (int ix = 0; ix < n; ix++)
            {
                float u = (ix + 0.5f) / n, v = (iz + 0.5f) / n;
                float x = X0 + Largura * u, z = Z0 + Comprimento * v;
                float incl = td.GetSteepness(u, v);
                float dtReal = DistTrilha(x, z);
                // borda recortada: ruído largo desloca a margem, ruído fino esfarela
                // ela em manchas — trilha de verdade não tem linha reta
                float dt = dtReal + (Mathf.PerlinNoise(x * 0.6f, z * 0.6f) - 0.5f) * 0.4f
                                  + (Mathf.PerlinNoise(x * 2.4f + 50f, z * 2.4f + 50f) - 0.5f) * 0.5f;
                float dc = Mathf.Abs(z - ZCorrego(x)) - LarguraCorrego(x);
                float fora = DistFora(x, z);

                System.Array.Clear(w, 0, k);
                w[Serapilheira] = 1f;
                w[Cobertura] = S(0.4f, 0.58f, Mathf.PerlinNoise(x * 0.07f + 40f, z * 0.07f + 3f)) * 2.2f + (fora > 0f ? 1.2f : 0f);
                w[Raizes] = S(0.55f, 0.7f, Mathf.PerlinNoise(x * 0.13f + 80f, z * 0.13f + 21f)) * 1.2f;
                w[Musgo] = S(0.66f, 0.78f, Mathf.PerlinNoise(x * 0.09f + 7f, z * 0.09f + 61f)) * 0.9f;
                // terra solta fica rara fora da trilha: senão a trilha não contrasta
                w[Terra] = S(0.7f, 0.8f, Mathf.PerlinNoise(x * 0.05f + 12f, z * 0.05f + 90f)) * 0.25f;

                // trilha estreita e de borda nítida: terra batida no meio, faixa
                // de raiz escura na margem, mato logo depois
                float trilha = 1f - S(0.5f, 1.0f, dt);
                // folha acumulada na beirada (quem passa empurra pro lado) e raiz
                // exposta logo depois: a transição é de chão, não de cor
                // margem ESCURA: folhagem verde e raiz dos dois lados, pouco folhiço
                // claro. O contraste vem do escuro do lado, não de pintar a trilha.
                float beirada = S(0.8f, 1.15f, dt) * (1f - S(2.2f, 3.4f, dt));
                w[Serapilheira] *= 0.25f + 0.75f * S(1.2f, 3.2f, dt);
                w[Sombra] += beirada * 3.6f;
                w[Raizes] += beirada * 1.8f;
                if (fora > 3f) trilha *= 1f - S(3f, 8f, fora) * 0.5f;
                // miolo compactado: o pisoteio tira folha, musgo e raiz do leito.
                // Sem isso o folhiço dividia o peso e a trilha virava 50% terra.
                float limpo = 1f - 0.94f * trilha;
                w[Serapilheira] *= limpo; w[Cobertura] *= limpo; w[Raizes] *= limpo;
                w[Musgo] *= limpo; w[Terra] *= limpo; w[Sombra] *= limpo;
                w[TerraBatida] = trilha * 4f;

                float barranco = 1f - S(0f, 1.4f, dc);
                w[Lama] = (1f - S(-0.6f, 0.4f, dc)) * 5f;
                w[Terra] += barranco * 2.5f;

                foreach (var c in Crateras)
                {
                    float t = Vector2.Distance(new Vector2(x, z), new Vector2(c.x, c.y)) / c.z;
                    w[Lama] += (1f - S(0.3f, 0.75f, t)) * 5f;
                    w[Terra] += (1f - S(0.6f, 1.4f, t)) * 3f;
                }

                w[Rocha] = S(24f, 38f, incl) * 6f + (fora > 0f ? S(0.6f, 0.72f, Fbm(x * 0.05f, z * 0.05f, 2)) * 2f : 0f);

                float soma = 0f;
                for (int i = 0; i < k; i++) { if (w[i] < 0f) w[i] = 0f; soma += w[i]; }
                for (int i = 0; i < k; i++) a[iz, ix, i] = w[i] / soma;
            }
        td.SetAlphamaps(0, 0, a);
        EditorUtility.SetDirty(td);
    }

    // ---------------------------------------------------------------- água

    static void CriarAgua(Transform pai)
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(Pasta + "/M_Agua.mat");
        if (mat == null)
        {
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);
            mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.renderQueue = 3000;
            AssetDatabase.CreateAsset(mat, Pasta + "/M_Agua.mat");
        }
        mat.SetColor("_BaseColor", new Color(0.05f, 0.075f, 0.065f, 0.82f));
        mat.SetFloat("_Smoothness", 0.93f);
        mat.SetFloat("_Metallic", 0f);
        EditorUtility.SetDirty(mat);

        var agua = new GameObject("Agua").transform;
        agua.SetParent(pai, false);

        // córrego: faixa seguindo a linha do leito, na altura do leito + 25 cm
        var vs = new List<Vector3>(); var tri = new List<int>(); var uv = new List<Vector2>();
        for (float x = -85f; x <= 85f; x += 1f)
        {
            float z = ZCorrego(x), w = LarguraCorrego(x) + 0.6f + DistFora(x, z) * 0.15f;
            float y = H(x, z) + 0.25f;
            vs.Add(new Vector3(x, y, z - w)); vs.Add(new Vector3(x, y, z + w));
            uv.Add(new Vector2(x * 0.2f, 0f)); uv.Add(new Vector2(x * 0.2f, 1f));
            int i = vs.Count - 4;
            if (i >= 0) { tri.Add(i); tri.Add(i + 1); tri.Add(i + 2); tri.Add(i + 1); tri.Add(i + 3); tri.Add(i + 2); }
        }
        Malha(agua, "Corrego", vs, tri, uv, mat);

        // poças no fundo das crateras
        foreach (var c in Crateras)
        {
            float y = H(c.x, c.y) + 0.22f;
            float r = c.z * 0.5f;
            var pv = new List<Vector3> { new Vector3(c.x, y, c.y) }; var pt = new List<int>(); var pu = new List<Vector2> { new Vector2(0.5f, 0.5f) };
            const int lados = 20;
            for (int i = 0; i < lados; i++)
            {
                float ang = i * Mathf.PI * 2f / lados;
                float rr = r * (0.8f + 0.35f * Mathf.PerlinNoise(c.x + i * 0.4f, c.y));
                pv.Add(new Vector3(c.x + Mathf.Cos(ang) * rr, y, c.y + Mathf.Sin(ang) * rr));
                pu.Add(new Vector2(0.5f + Mathf.Cos(ang) * 0.5f, 0.5f + Mathf.Sin(ang) * 0.5f));
                pt.Add(0); pt.Add(1 + (i + 1) % lados); pt.Add(1 + i);
            }
            Malha(agua, "Poca", pv, pt, pu, mat);
        }
    }

    static void Malha(Transform pai, string nome, List<Vector3> v, List<int> t, List<Vector2> uv, Material mat)
    {
        var m = new Mesh { name = nome };
        m.SetVertices(v); m.SetTriangles(t, 0); m.SetUVs(0, uv);
        m.RecalculateNormals(); m.RecalculateBounds();
        var go = new GameObject(nome, typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetParent(pai, false);
        go.GetComponent<MeshFilter>().sharedMesh = m;
        var r = go.GetComponent<MeshRenderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        // malha gerada fica dentro da cena
    }

    // ---------------------------------------------------------------- blocos de protótipo

    internal static GameObject P(string caminho)
    {
        var go = AssetDatabase.LoadAssetAtPath<GameObject>(caminho);
        if (go == null) Debug.LogWarning("[FlorestaSelva] faltou " + caminho);
        return go;
    }

    const string PH = "Assets/_Game/Modelos/PolyHaven/";
    const string JP = "Assets/JP Environmental Asset Pack/Prefabs/";

    internal static Bounds Limites(GameObject go)
    {
        var b = new Bounds(go.transform.position, Vector3.zero); bool primeiro = true;
        foreach (var r in go.GetComponentsInChildren<Renderer>())
        {
            if (primeiro) { b = r.bounds; primeiro = false; } else b.Encapsulate(r.bounds);
        }
        return b;
    }

    /// Esconde o cubo e põe um modelo do mesmo tamanho no lugar. O colisor do
    /// cubo continua valendo — cobertura e navegação ficam idênticas.
    static void Vestir(Transform nivel)
    {
        foreach (Transform g in new[] { nivel.Find("Rochedo_Central"), nivel.Find("Posto_Caixotes"), nivel.Find("Obstaculos") })
            foreach (Transform t in g)
                foreach (Transform v in t) if (v.name == "Visual") Object.DestroyImmediate(v.gameObject);

        var pedras = new[] { P("Assets/RockFREE/LODGroups/rock1_LOD0.prefab"), P("Assets/RockFREE/LODGroups/rock2_LOD0.prefab"), P("Assets/RockFREE/LODGroups/rock4_LOD0.prefab") };
        int i = 0;
        foreach (Transform t in nivel.Find("Rochedo_Central")) Encaixar(t, pedras[i++ % pedras.Length], 1.15f, i * 67f);

        var tambores = new[] { P(PH + "Barrel_01/Barrel_01.fbx") };
        i = 0;
        foreach (Transform t in nivel.Find("Posto_Caixotes"))
        {
            var r = t.GetComponent<MeshRenderer>(); if (r) r.enabled = false;
            var vis = new GameObject("Visual").transform; vis.SetParent(t, false);
            vis.localScale = new Vector3(1f / t.lossyScale.x, 1f / t.lossyScale.y, 1f / t.lossyScale.z);
            vis.localPosition = new Vector3(0, -0.5f, 0);
            // dois ou três tambores de pé e um caído, cara de depósito abandonado
            for (int k = 0; k < 3; k++)
            {
                var b = (GameObject)PrefabUtility.InstantiatePrefab(tambores[0], vis);
                b.transform.localPosition = new Vector3((k % 2) * 0.62f - 0.3f, 0f, (k / 2) * 0.62f - 0.3f);
                b.transform.localRotation = Quaternion.Euler(0, (i * 53 + k * 91) % 360, 0);
                SemColisor(b);
            }
            var caixa = (GameObject)PrefabUtility.InstantiatePrefab(P(PH + "ammo_box/ammo_box.fbx"), vis);
            caixa.transform.localPosition = new Vector3(0.35f, 0.02f, 0.4f);
            caixa.transform.localScale = Vector3.one * 2.2f;
            caixa.transform.localRotation = Quaternion.Euler(0, i * 40, 0);
            SemColisor(caixa);
            i++;
        }

        // tambor verde-oliva de exército, não vermelho de posto de gasolina
        var oliva = new Dictionary<Material, Material>();
        foreach (Transform t in nivel.Find("Posto_Caixotes"))
            foreach (var r in t.GetComponentsInChildren<MeshRenderer>())
            {
                if (!r.name.ToLower().Contains("barrel")) continue;
                var ms = r.sharedMaterials;
                for (int k = 0; k < ms.Length; k++) ms[k] = Oliva(ms[k], oliva);
                r.sharedMaterials = ms;
            }

        // marcas de trilha e paredes de limite: referência e colisor, não cenário
        foreach (Transform t in nivel.Find("Trilha"))
        {
            if (t.name.StartsWith("Marca")) { var r = t.GetComponent<Renderer>(); if (r) r.enabled = false; }
            else if (t.name.StartsWith("Poste")) Encaixar(t, P("Assets/NikolayFedorov/OldVillage/VilagePropsPack/Prefab/Trunks/Trunk01.prefab"), 1f, 0f);
        }
        foreach (Transform t in nivel) if (t.name.StartsWith("Limite_")) { var r = t.GetComponent<Renderer>(); if (r) r.enabled = false; }

        // colunas cinza dos bosques viram árvore; o cilindro segue como tronco invisível
        var comuns = new[] { P("Assets/ForestVision/Optimized/Opt_Kapok.prefab"), P("Assets/ForestVision/Optimized/Opt_Oaky.prefab"), P("Assets/ForestVision/Optimized/Opt_TallJungle.prefab") };
        i = 0;
        foreach (Transform t in nivel.Find("Obstaculos"))
        {
            if (!t.name.StartsWith("Bosque")) continue;
            var r = t.GetComponent<MeshRenderer>(); if (r == null) continue;
            for (int k = t.childCount - 1; k >= 0; k--) if (t.GetChild(k).name == "Visual") Object.DestroyImmediate(t.GetChild(k).gameObject);
            var alvo = Caixa(t); r.enabled = false;
            var vis = new GameObject("Visual").transform; vis.SetParent(t, false);
            vis.localScale = new Vector3(1f / t.lossyScale.x, 1f / t.lossyScale.y, 1f / t.lossyScale.z);
            vis.rotation = Quaternion.identity;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(comuns[i % comuns.Length], vis);
            SemColisor(go);
            go.transform.position = new Vector3(alvo.center.x, alvo.min.y - 0.1f, alvo.center.z);
            go.transform.rotation = Quaternion.Euler(0, i * 47f, 0);
            go.transform.localScale = Vector3.one;
            var m = Limites(go);
            go.transform.localScale = Vector3.one * (alvo.size.y / m.size.y) * 1.1f;
            i++;
        }

        var radio = GameObject.Find("Atores/Radio");
        if (radio != null) Encaixar(radio.transform, P("Assets/Survival Tools/Prefabs/walkie.prefab") ?? AcharPrefab("walkie"), 1f, 25f, uniforme: true);
        var corpo = GameObject.Find("Atores/Corpo_Unidade");
        if (corpo != null) Morto(corpo.transform);

        var oeste = nivel.Find("Obstaculos/Ruina_Oeste");
        if (oeste != null) Encaixar(oeste, P("Assets/Blackant Master Studio/The Shed/The Shed Optimized/Prefab/The Shed.prefab"), 1f, 200f, uniforme: true);
        var leste = nivel.Find("Obstaculos/Ruina_Leste");
        if (leste != null) Encaixar(leste, P("Assets/RockFREE/LODGroups/rock3_LOD0.prefab"), 1.1f, 30f);
    }

    /// Caixa do primitivo pela malha, não pelo renderer: o renderer já pode
    /// estar desligado de uma rodada anterior e aí os limites dele somem.
    static Bounds Caixa(Transform t)
    {
        var mf = t.GetComponent<MeshFilter>();
        if (mf == null || mf.sharedMesh == null) return new Bounds(t.position, t.lossyScale);
        var b = mf.sharedMesh.bounds;
        var s = t.lossyScale;
        var tam = t.rotation * Vector3.Scale(b.size, s);
        return new Bounds(t.TransformPoint(b.center), new Vector3(Mathf.Abs(tam.x), Mathf.Abs(tam.y), Mathf.Abs(tam.z)));
    }

    static GameObject AcharPrefab(string nome)
    {
        foreach (var g in AssetDatabase.FindAssets(nome + " t:Prefab")) return AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(g));
        return null;
    }

    static Material Oliva(Material m, Dictionary<Material, Material> cache)
    {
        if (m == null || m.name.StartsWith("M_Oliva")) return m;
        if (cache.TryGetValue(m, out var c)) return c;
        string arq = Pasta + "/M_Oliva_" + m.name + ".mat";
        c = AssetDatabase.LoadAssetAtPath<Material>(arq);
        if (c == null) { c = new Material(m); AssetDatabase.CreateAsset(c, arq); }
        c.SetColor("_BaseColor", new Color(0.36f, 0.4f, 0.26f));
        EditorUtility.SetDirty(c);
        cache[m] = c;
        return c;
    }

    /// O corpo da unidade era uma cápsula deitada. Vira um soldado caído: o
    /// mesmo modelo dos guardas, com a animação de morte parada no último quadro.
    /// O pacote de soldados é a versão demo e não traz morte nem cadáver (só
    /// guarda parado, combate parado, correr e atirar), então a morte vem da
    /// biblioteca de animação que já está no projeto.
    static void Morto(Transform capsula)
    {
        var r = capsula.GetComponent<MeshRenderer>(); if (r) r.enabled = false;
        for (int k = capsula.childCount - 1; k >= 0; k--) if (capsula.GetChild(k).name == "Visual") Object.DestroyImmediate(capsula.GetChild(k).gameObject);
        var vis = new GameObject("Visual").transform;
        vis.SetParent(capsula, false);
        vis.localScale = new Vector3(1f / capsula.lossyScale.x, 1f / capsula.lossyScale.y, 1f / capsula.lossyScale.z);
        vis.rotation = Quaternion.identity;
        PorCorpo(vis, capsula.position, 140f);
    }

    public static UnityEditor.Animations.AnimatorController ControladorDeMorte()
    {
        const string cp = "Assets/_Game/Modelos/Q_Animacoes/AC_Morto.controller";
        var ctrl = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(cp);
        if (ctrl != null) return ctrl;
        AnimationClip morte = null;
        foreach (var o in AssetDatabase.LoadAllAssetsAtPath("Assets/_Game/Modelos/Q_Animacoes/UAL1_Standard.fbx"))
            if (o is AnimationClip c && !c.name.StartsWith("__preview__") && c.name.EndsWith("Death01")) morte = c;
        if (morte == null) return null;
        ctrl = UnityEditor.Animations.AnimatorController.CreateAnimatorControllerAtPath(cp);
        var st = ctrl.layers[0].stateMachine.AddState("Caido");
        st.motion = morte;
        ctrl.layers[0].stateMachine.defaultState = st;
        AssetDatabase.SaveAssets();
        return ctrl;
    }

    internal static GameObject PorCorpo(Transform pai, Vector3 pos, float giro)
    {
        var modelo = P("Assets/ToonSoldiers_WW2_demo/model/ToonSoldier_WW2_demo.FBX");
        var ctrl = ControladorDeMorte();
        if (modelo == null || ctrl == null) return null;
        var go = (GameObject)PrefabUtility.InstantiatePrefab(modelo, pai);
        go.name = "Soldado_Caido";
        go.transform.position = new Vector3(pos.x, H(pos.x, pos.z), pos.z);
        go.transform.rotation = Quaternion.Euler(0, giro, 0);
        go.transform.localScale = Vector3.one * 0.62f;
        SemColisor(go);
        var an = go.GetComponent<Animator>();
        an.runtimeAnimatorController = ctrl;
        an.applyRootMotion = false;
        an.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        var mp = go.AddComponent<MortoParado>();
        mp.estado = "Caido";
        mp.comecarNoFim = true;
        return go;
    }

    static Material Material_FolhaSeca()
    {
        const string arq = Pasta + "/M_FolhaSeca.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(arq);
        if (m == null)
        {
            var b = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Modelos/PolyHaven/Materiais/PH_fern_02.mat");
            m = b != null ? new Material(b) : new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(m, arq);
        }
        // folha caída é marrom e fosca, não verde
        m.SetColor("_BaseColor", new Color(0.42f, 0.3f, 0.19f));
        m.SetFloat("_Smoothness", 0.05f);
        EditorUtility.SetDirty(m);
        return m;
    }

    static Material Material_Bruma()
    {
        const string tex = Pasta + "/T_Bruma.png";
        if (AssetDatabase.LoadAssetAtPath<Texture2D>(tex) == null)
        {
            const int N = 128;
            var t = new Texture2D(N, N, TextureFormat.RGBA32, false);
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), new Vector2(N / 2f, N / 2f)) / (N / 2f);
                    float a = Mathf.Clamp01(1f - d);
                    a = a * a * (0.6f + 0.4f * Mathf.PerlinNoise(x * 0.06f, y * 0.06f));
                    t.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            t.Apply();
            File.WriteAllBytes(Path.GetFullPath(tex), t.EncodeToPNG());
            AssetDatabase.ImportAsset(tex);
            var ti = (TextureImporter)AssetImporter.GetAtPath(tex);
            ti.alphaIsTransparency = true;
            ti.textureCompression = TextureImporterCompression.CompressedHQ;
            ti.SaveAndReimport();
        }
        const string arq = Pasta + "/M_Bruma.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(arq);
        if (m == null)
        {
            var sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (sh == null) sh = Shader.Find("Universal Render Pipeline/Unlit");
            m = new Material(sh);
            AssetDatabase.CreateAsset(m, arq);
        }
        m.SetFloat("_Surface", 1f);
        m.SetFloat("_Blend", 0f);
        m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_ZWrite", 0f);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.SetOverrideTag("RenderType", "Transparent");
        m.renderQueue = 3050;
        m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(tex));
        m.SetColor("_BaseColor", new Color(0.62f, 0.66f, 0.64f, 1f));
        EditorUtility.SetDirty(m);
        return m;
    }

    /// Bruma rente ao chão: pouca partícula, enorme e quase transparente. É o
    /// que dá profundidade no escuro sem custar quase nada.
    static void Bruma(Transform pai, float x, float z, Material mat)
    {
        var go = new GameObject("Bruma");
        go.transform.SetParent(pai, false);
        go.transform.position = new Vector3(x, H(x, z) + 0.35f, z);
        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.loop = true;
        main.prewarm = true;
        main.startLifetime = 18f;
        main.startSpeed = 0.09f;
        main.startSize = new ParticleSystem.MinMaxCurve(5f, 11f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 1f, 1f, 0.05f), new Color(1f, 1f, 1f, 0.11f));
        main.maxParticles = 26;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = -0.002f;
        var em = ps.emission; em.rateOverTime = 1.1f;
        var sh = ps.shape;
        sh.enabled = true;
        sh.shapeType = ParticleSystemShapeType.Box;
        sh.scale = new Vector3(13f, 0.4f, 13f);
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(new[] { new GradientColorKey(Color.white, 0f) },
                     new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
        col.color = new ParticleSystem.MinMaxGradient(grad);
        var rend = go.GetComponent<ParticleSystemRenderer>();
        rend.material = mat;
        rend.renderMode = ParticleSystemRenderMode.HorizontalBillboard;
        rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        rend.receiveShadows = false;
        rend.sortingFudge = 5f;
    }

    static void Encaixar(Transform cubo, GameObject modelo, float folga, float giro, bool uniforme = false)
    {
        if (modelo == null) return;
        for (int k = cubo.childCount - 1; k >= 0; k--) if (cubo.GetChild(k).name == "Visual") Object.DestroyImmediate(cubo.GetChild(k).gameObject);
        var r = cubo.GetComponent<MeshRenderer>();
        var alvo = Caixa(cubo);
        if (r != null) r.enabled = false;

        var vis = new GameObject("Visual").transform;
        vis.SetParent(cubo, false);
        // Visual com escala de mundo 1: o modelo não herda a deformação do cubo
        vis.localScale = new Vector3(1f / cubo.lossyScale.x, 1f / cubo.lossyScale.y, 1f / cubo.lossyScale.z);
        vis.rotation = Quaternion.identity;
        var go = (GameObject)PrefabUtility.InstantiatePrefab(modelo, vis);
        SemColisor(go);
        go.transform.rotation = Quaternion.Euler(0, giro, 0);
        go.transform.position = alvo.center;
        go.transform.localScale = Vector3.one;

        var m = Limites(go);
        var e = new Vector3(alvo.size.x / m.size.x, alvo.size.y / m.size.y, alvo.size.z / m.size.z) * folga;
        if (uniforme) e = Vector3.one * Mathf.Min(e.x, e.z);
        go.transform.localScale = e;
        m = Limites(go);
        // base do modelo no pé do cubo, afundado um pouco pra não flutuar em chão torto
        go.transform.position += new Vector3(alvo.center.x - m.center.x, alvo.min.y - m.min.y - 0.15f, alvo.center.z - m.center.z);
    }

    internal static void SemColisor(GameObject go)
    {
        foreach (var c in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
    }

    // ---------------------------------------------------------------- mata

    class Proibido { public Vector2 p; public float r; }

    static List<Proibido> PontosDeJogo(Transform nivel)
    {
        var l = new List<Proibido>();
        void Add(Transform t, float r) { if (t != null) l.Add(new Proibido { p = new Vector2(t.position.x, t.position.z), r = r }); }
        foreach (var raiz in new[] { "Atores", "Beats", "Portao" })
        {
            var g = GameObject.Find(raiz); if (g == null) continue;
            foreach (Transform c in g.transform)
            {
                if (c.name.StartsWith("Rota_")) foreach (Transform w in c) Add(w, 2.2f);
                else Add(c, 2.5f);
            }
        }
        foreach (var n in new[] { "Rochedo_Central", "Posto_Caixotes", "Obstaculos", "Vegetacao" })
            foreach (Transform t in nivel.Find(n)) Add(t, n == "Obstaculos" ? 3.5f : n == "Vegetacao" ? 1.8f : 2.4f);
        Add(nivel.Find("Zona_Extracao"), 6f);
        Add(GameObject.Find("_Setup/SpawnPoint").transform, 4f);
        return l;
    }

    static bool Livre(float x, float z, List<Proibido> ps, float folga)
    {
        foreach (var p in ps) if ((new Vector2(x, z) - p.p).sqrMagnitude < (p.r + folga) * (p.r + folga)) return false;
        return true;
    }

    static float Rnd(System.Random r, float a, float b) => a + (float)r.NextDouble() * (b - a);

    static GameObject Por(Transform pai, GameObject pref, float x, float z, float escala, System.Random rnd, bool colisor, float afundar = 0.05f, bool inclinar = true)
    {
        if (pref == null) return null;
        var go = (GameObject)PrefabUtility.InstantiatePrefab(pref, pai);
        go.transform.position = new Vector3(x, H(x, z) - afundar, z);
        var giro = Quaternion.Euler(0, Rnd(rnd, 0, 360), 0);
        if (inclinar)
        {
            // acompanha a inclinação do chão
            float e = 0.4f;
            var n = Vector3.Cross(new Vector3(0, H(x, z + e) - H(x, z - e), 2 * e), new Vector3(2 * e, H(x + e, z) - H(x - e, z), 0)).normalized;
            if (n.y < 0) n = -n;
            giro = Quaternion.FromToRotation(Vector3.up, n) * giro;
        }
        go.transform.rotation = giro;
        go.transform.localScale = go.transform.localScale * escala;
        if (!colisor) SemColisor(go);
        return go;
    }

    static void Espalhar(Transform floresta, Transform nivel)
    {
        var rnd = new System.Random(1968);
        var ps = PontosDeJogo(nivel);
        Transform G(string n) { var t = new GameObject(n).transform; t.SetParent(floresta, false); return t; }

        var troncos = G("TroncosCaidos"); var pedras = G("Pedras"); var tocos = G("Tocos"); var miudos = G("Miudezas");
        var palmas = G("Palmeiras"); var guerra = G("Guerra"); var longe = G("MataDistante"); var samambaias = G("Samambaias");
        var arbustos = G("Arbustos"); var folhas = G("PilhasDeFolha"); var beira = G("BeiraDaTrilha"); var mortos = G("Mortos"); var bruma = G("Bruma");

        // Dentro: longe da trilha, dos pontos de jogo e do leito do córrego
        bool Dentro(out float x, out float z, float trilha, float folga, bool corregoOk = false)
        {
            for (int tent = 0; tent < 60; tent++)
            {
                x = Rnd(rnd, -JX + 1f, JX - 1f); z = Rnd(rnd, JZmin + 1f, JZmax - 1f);
                if (DistTrilha(x, z) < trilha) continue;
                if (!corregoOk && Mathf.Abs(z - ZCorrego(x)) < LarguraCorrego(x) + 0.8f) continue;
                if (Mirante(x, z) > 1.9f) continue; // topo do morro fica limpo, é mirante
                if (!Livre(x, z, ps, folga)) continue;
                return true;
            }
            x = z = 0; return false;
        }

        var tora = P("Assets/NikolayFedorov/OldVillage/VilagePropsPack/Prefab/Logs/log01.prefab");
        var tronco = P(PH + "dead_tree_trunk/dead_tree_trunk.fbx");
        for (int i = 0; i < 55; i++)
            if (Dentro(out float x, out float z, 2.6f, 1.5f))
            {
                var go = Por(troncos, i % 4 == 0 && tronco != null ? tronco : tora, x, z, Rnd(rnd, 0.9f, 1.4f), rnd, true, 0.12f);
                if (go != null && go.GetComponentInChildren<Collider>() == null) go.AddComponent<BoxCollider>();
            }

        var grandes = new[] { P(JP + "Large Rock 1.prefab"), P(JP + "Large Rock 2.prefab"), P("Assets/RockFREE/LODGroups/rock6_LOD0.prefab") };
        for (int i = 0; i < 30; i++)
            if (Dentro(out float x, out float z, 2.4f, 1.6f))
            {
                var pref = grandes[i % grandes.Length];
                float e = pref != null && pref.name.StartsWith("rock") ? Rnd(rnd, 0.18f, 0.28f) : Rnd(rnd, 0.5f, 1.05f);
                Por(pedras, pref, x, z, e, rnd, true, 0.3f);
            }
        var pequenas = new[] { P(JP + "Small Rock 1.prefab"), P(JP + "Small Rock 2.prefab"), P(JP + "Small Rock 3.prefab"), P(JP + "Small Rock 4 Moss.prefab"), P(JP + "Rock Group.prefab") };
        for (int i = 0; i < 140; i++)
            if (Dentro(out float x, out float z, 1.2f, 0.3f, corregoOk: i % 3 == 0))
                Por(pedras, pequenas[i % pequenas.Length], x, z, Rnd(rnd, 0.6f, 1.6f), rnd, false, 0.08f);
        // barranco de pedra nas laterais do morro do mirante
        var pedraMirante = P("Assets/RockFREE/LODGroups/rock5_LOD0.prefab");
        for (int i = 0; i < 6; i++)
        {
            float x = (i % 2 == 0 ? -1 : 1) * Rnd(rnd, 8.6f, 9.8f), z = Rnd(rnd, -12f, -6f);
            if (Livre(x, z, ps, 0f)) Por(pedras, pedraMirante, x, z, Rnd(rnd, 0.08f, 0.12f), rnd, true, 0.4f);
        }

        var toco = P("Assets/NikolayFedorov/OldVillage/VilagePropsPack/Prefab/Trunks/Trunk01.prefab");
        var tocoPH = new[] { P(PH + "tree_stump_01/tree_stump_01.fbx"), P(PH + "tree_stump_02/tree_stump_02.fbx") };
        for (int i = 0; i < 38; i++)
            if (Dentro(out float x, out float z, 2f, 0.8f))
                Por(tocos, i % 3 == 0 ? tocoPH[i % 2] : toco, x, z, Rnd(rnd, 0.9f, 1.6f), rnd, true, 0.05f);
        var raiz = P(PH + "root_cluster_01/root_cluster_01.fbx");
        for (int i = 0; i < 5; i++)
            if (Dentro(out float x, out float z, 2.2f, 1f))
                Por(tocos, raiz, x, z, Rnd(rnd, 0.7f, 1f), rnd, false, 0.2f);
        // árvore seca (DeadTree) saiu de vez: de cima a galhada retorcida era
        // só poluição na tela.

        var galhos = P(PH + "dry_branches_medium_01/dry_branches_medium_01.fbx");
        var casca = P(PH + "bark_debris_01/bark_debris_01.fbx");
        var cogumelo = P("Assets/_Game/Modelos/Q_Natureza/Mushroom_Common.fbx");
        // galho seco só ao pé do que caiu: tronco e toco. Espalhado ao acaso
        // eram 137 feixes sem motivo pelo mapa todo.
        int ng = 0;
        foreach (Transform fonte in new[] { troncos, tocos })
            foreach (Transform oG in fonte)
            {
                if (rnd.NextDouble() > 0.45) continue;
                var qG = oG.position + new Vector3(Rnd(rnd, -1.4f, 1.4f), 0f, Rnd(rnd, -1.4f, 1.4f));
                if (DistTrilha(qG.x, qG.z) < 1f) continue;
                Por(miudos, galhos, qG.x, qG.z, Rnd(rnd, 0.7f, 1.1f), rnd, false, 0.02f);
                ng++;
            }
        Debug.Log("[FlorestaSelva] galho seco ao pé de tronco/toco: " + ng);
        for (int i = 0; i < 5; i++) if (Dentro(out float x, out float z, 1.2f, 0f)) Por(miudos, casca, x, z, Rnd(rnd, 0.9f, 1.3f), rnd, false, 0.02f);
        for (int i = 0; i < 80; i++) if (Dentro(out float x, out float z, 1.5f, 0f)) Por(miudos, cogumelo, x, z, Rnd(rnd, 0.35f, 0.8f), rnd, false, 0.02f);

        var fetoArvore = P("Assets/ForestVision/Optimized/Valley Fern Tree.prefab");
        for (int i = 0; i < 30; i++)
            if (Dentro(out float x, out float z, 3f, 1.8f))
                Por(samambaias, fetoArvore, x, z, Rnd(rnd, 0.35f, 0.55f), rnd, false, 0.1f, inclinar: false);

        // palmeiras: nas bordas do corredor e na beira do córrego
        var palmeira = new List<GameObject>();
        for (int i = 1; i <= 12; i++) { var p = P("Assets/Coconut Palm Tree Pack/Prefabs/Coconut_Palm_Tree" + i.ToString("00") + ".prefab"); if (p) palmeira.Add(p); }
        int copa = LayerMask.NameToLayer("Copa");
        for (int i = 0; i < 12; i++)
        {
            float x = (i % 2 == 0 ? -1 : 1) * Rnd(rnd, 11f, 16.5f), z = Rnd(rnd, JZmin + 3f, JZmax - 3f);
            if (i < 4) { x = Rnd(rnd, -15f, 15f); z = ZCorrego(x) + (i % 2 == 0 ? -1 : 1) * (LarguraCorrego(x) + 1.3f); }
            if (!Livre(x, z, ps, 1f) || DistTrilha(x, z) < 3f) continue;
            var go = Por(palmas, palmeira[i % palmeira.Count], x, z, Rnd(rnd, 0.38f, 0.52f), rnd, true, 0.1f, inclinar: false);
            if (go != null) Folhas(go, copa);
        }

        // guerra: o rádio e o corpo da unidade ficam em volta do posto
        var lata = P(PH + "can_rusted/can_rusted.fbx"); var pneu = P(PH + "old_tyre/old_tyre.fbx");
        var municao = P(PH + "ammo_box/ammo_box.fbx"); var tambor = P(PH + "Barrel_01/Barrel_01.fbx");
        var arame = P("Assets/Assets_FenceChained/prefabs/chainlink_barbwire-1.prefab");
        var posto = nivel.Find("Posto_Caixotes");
        var centroPosto = posto != null && posto.childCount > 0 ? posto.GetChild(0).position : new Vector3(-11, 0, 19);
        for (int i = 0; i < 14; i++)
        {
            float x = centroPosto.x + Rnd(rnd, -4f, 4f), z = centroPosto.z + Rnd(rnd, -4f, 4f);
            var pref = i % 4 == 0 ? pneu : i % 4 == 1 ? municao : lata;
            Por(guerra, pref, x, z, pref == municao ? 2.2f : 1f, rnd, false, 0.02f);
        }
        for (int i = 0; i < 10; i++)
            if (Dentro(out float x, out float z, 1.5f, 0.5f))
                Por(guerra, i % 3 == 0 ? tambor : i % 3 == 1 ? municao : lata, x, z, i % 3 == 1 ? 2.2f : 1f, rnd, false, 0.02f);
        // arame farpado largado na frente do portão, dos dois lados do cipoal
        if (arame != null)
            foreach (var px in new[] { -9f, 10.5f })
            {
                var go = Por(guerra, arame, px, 36.5f, 1f, rnd, true, 0.05f, inclinar: false);
                if (go != null) go.transform.rotation = Quaternion.Euler(0, px < 0 ? 8f : -6f, 0);
            }

        // arbusto: em MOITAS. Salpicado um a um pelo mapa ficava aleatório —
        // lugar sem nada do lado de lugar com um pé solto. Moita é centro + 4–7
        // plantas apertadas, e só onde o ruído de mancha diz que a mata fecha.
        var moita = new[] { P("Assets/ForestVision/Optimized/ForestBush.prefab"), P(JP + "Foliage 1.prefab"), P(JP + "Foliage 2.prefab"), P(JP + "Foliage 3.prefab") }; // Bush_Common saiu: é vermelho de fábrica e tem folha quadrada
        var massaM = P("Assets/ForestVision/Optimized/Opt_SmallBranching.prefab");
        int nm = 0, nMoitas = 0;
        for (int tent = 0; tent < 3000 && nMoitas < 60; tent++)
        {
            float x = Rnd(rnd, -JX + 1.5f, JX - 1.5f), z = Rnd(rnd, JZmin + 1.5f, JZmax - 1.5f);
            if (Mathf.PerlinNoise(x * 0.11f + 500f, z * 0.11f + 20f) < 0.5f) continue;
            if (DistTrilha(x, z) < 3.2f) continue;
            if (Mathf.Abs(z - ZCorrego(x)) < LarguraCorrego(x) + 1.5f) continue;
            if (!Livre(x, z, ps, 1.2f)) continue;
            int nPl = 5 + rnd.Next(4);
            for (int kM = 0; kM < nPl; kM++)
            {
                float angM = kM * 2.4f + Rnd(rnd, -0.4f, 0.4f), rM = kM == 0 ? 0f : Rnd(rnd, 0.5f, 1.3f);
                float pxM = x + Mathf.Cos(angM) * rM, pzM = z + Mathf.Sin(angM) * rM;
                if (DistTrilha(pxM, pzM) < 2f) continue;
                if (kM < 2) { Por(arbustos, massaM, pxM, pzM, Rnd(rnd, 0.28f, 0.37f), rnd, false, 0.08f, inclinar: false); nm++; continue; }
                var prefM = kM % 2 == 0 ? moita[3] : moita[2];
                Por(arbustos, prefM, pxM, pzM, Rnd(rnd, 0.9f, 1.2f), rnd, false, 0.08f);
                nm++;
            }
            nMoitas++;
        }
        Debug.Log("[FlorestaSelva] arbustos: " + nMoitas + " moitas, " + nm + " plantas");

        MataNova(G("MataNova"), ps, rnd);
        Corrego(G("Corrego_Travessias"));

        // folhiço em 3D: galho e casca, não arbusto pintado de marrom
        // (a primeira versão achatava um arbusto e virava um monte vermelho)
        int nf = 0;
        for (int tent = 0; tent < 800 && nf < 90; tent++)
        {
            float x = Rnd(rnd, -JX + 1f, JX - 1f), z = Rnd(rnd, JZmin + 1f, JZmax - 1f);
            if (DistTrilha(x, z) < 1.3f) continue;
            if (Mathf.Abs(z - ZCorrego(x)) < LarguraCorrego(x) + 0.5f) continue;
            // pilha de casca; galho só de vez em quando
            for (int k = 0; k < 3; k++)
                Por(folhas, k == 0 && nf % 4 == 0 ? galhos : casca, x + Rnd(rnd, -0.8f, 0.8f), z + Rnd(rnd, -0.8f, 0.8f), Rnd(rnd, 0.7f, 1.3f), rnd, false, 0.03f);
            nf++;
        }

        // beira da trilha: pedra e tora de um lado e de outro, alternando.
        // É isso que faz um caminho existir no meio do mato sem placa nenhuma.
        int nb = 0;
        foreach (var t in Suaves)
            for (int i = 0; i + 1 < t.Length; i++)
            {
                Vector2 a = t[i], b = t[i + 1];
                float comp = Vector2.Distance(a, b);
                // com a trilha subdividida os trechos têm 0,8 m: sorteia por trecho
                if (rnd.NextDouble() > 0.16) continue;
                for (float d = comp * 0.5f; d < comp; d += 10f)
                {
                    Vector2 q = Vector2.Lerp(a, b, d / comp);
                    if (Mathf.Abs(q.x) > JX - 1.5f || q.y < JZmin + 1f || q.y > JZmax - 1f) continue;
                    var dir = (b - a).normalized;
                    var lado = new Vector2(-dir.y, dir.x) * (nb % 2 == 0 ? 1f : -1f) * Rnd(rnd, 1.25f, 1.9f);
                    Vector2 w = q + lado;
                    if (!Livre(w.x, w.y, ps, 0.2f)) continue;
                    if (Mathf.Abs(w.y - ZCorrego(w.x)) < LarguraCorrego(w.x) + 0.6f) continue;
                    var pref = nb % 3 == 0 ? tora : nb % 3 == 1 ? pequenas[nb % pequenas.Length] : toco;
                    Por(beira, pref, w.x, w.y, pref == tora ? Rnd(rnd, 0.8f, 1.1f) : Rnd(rnd, 0.9f, 1.5f), rnd, false, 0.12f);
                    nb++;
                }
            }

        // margem de mato baixo dos dois lados da trilha, com falhas: é o que
        // desenha o caminho de verdade. Sem colisor — dá pra atravessar, só
        // não é o caminho.
        var margem = G("MargemDaTrilha");
        var baixos = new[] { P(JP + "Foliage 3.prefab"), P(JP + "Foliage 3.prefab"), P("Assets/ForestVision/Optimized/Opt_Grasses_01.prefab"),
                             P(JP + "Foliage 2.prefab"), P("Assets/ForestVision/Optimized/ForestBush.prefab") };
        int nmg = 0;
        var escuros = new Dictionary<Material, Material>();
        foreach (var t in Suaves)
            for (int i = 0; i + 1 < t.Length; i++)
            {
                Vector2 a = t[i], b = t[i + 1];
                if (Mathf.Abs(a.x) > JX - 1f || a.y < JZmin + 0.5f || a.y > JZmax - 0.5f) continue;
                var dir = (b - a).normalized;
                var nrm = new Vector2(-dir.y, dir.x);
                // duas fileiras: uma colada na trilha (a parede baixa que a desenha)
                // e outra atrás. Sem evitar ponto de jogo: é visual, sem colisor, e
                // as trilhas passam justamente pelos pontos — a margem sumia ali.
                foreach (float lado in new[] { -1f, 1f, -1f, 1f })
                {
                    bool colada = nmg % 4 < 2;
                    if (rnd.NextDouble() < (colada ? 0.18 : 0.45)) continue;
                    var w = a + nrm * lado * (colada ? Rnd(rnd, 1.1f, 1.8f) : Rnd(rnd, 1.9f, 3.4f)) + dir * Rnd(rnd, 0f, 0.8f);
                    if (DistTrilha(w.x, w.y) < 1.0f) continue;
                    if (Mathf.Abs(w.y - ZCorrego(w.x)) < LarguraCorrego(w.x) + 0.4f) continue;
                    var pref = baixos[nmg % baixos.Length];
                    float e = pref == baixos[4] ? Rnd(rnd, 0.25f, 0.4f) : pref == baixos[2] ? Rnd(rnd, 0.5f, 0.8f) : Rnd(rnd, 0.8f, 1.4f);
                    var gm = Por(margem, pref, w.x, w.y, e, rnd, false, 0.05f);
                    if (gm != null) Escurecer(gm, escuros);
                    nmg++;
                }
            }
        Debug.Log("[FlorestaSelva] margem de mato nas trilhas: " + nmg);

        // lampião largado no fim de trilha sem saída, com uma luz fraca
        var lampiao = P(PH + "Lantern_01/Lantern_01.fbx");
        foreach (var isca in Iscas)
        {
            var go = Por(guerra, lampiao, isca.x, isca.y, 1.2f, rnd, false, 0.02f, inclinar: false);
            if (go == null) continue;
            go.transform.rotation = Quaternion.Euler(0f, Rnd(rnd, 0, 360), Rnd(rnd, 70f, 95f)); // tombado
            var luzGo = new GameObject("LuzDoLampiao");
            luzGo.transform.SetParent(guerra, false);
            luzGo.transform.position = new Vector3(isca.x, H(isca.x, isca.y) + 0.35f, isca.y);
            var luz = luzGo.AddComponent<Light>();
            luz.type = LightType.Point;
            luz.color = new Color(1f, 0.62f, 0.3f);
            luz.range = 3.4f;
            luz.intensity = 0.9f;
            luz.shadows = LightShadows.None;
        }

        // marcas de uso no leito: pedrinha solta e graveto quebrado, esparsos
        var leito = G("LeitoDaTrilha");
        var pedrinha = new[] { P(JP + "Small Rock 1.prefab"), P(JP + "Small Rock 2.prefab"), P(JP + "Small Rock 3.prefab") };
        int nl = 0;
        foreach (var t in Suaves)
            for (int i = 0; i < t.Length; i++)
            {
                if (rnd.NextDouble() > 0.22) continue;
                var c = t[i];
                if (Mathf.Abs(c.x) > JX - 0.5f || c.y < JZmin + 0.5f || c.y > JZmax - 0.5f) continue;
                if (Mathf.Abs(c.y - ZCorrego(c.x)) < LarguraCorrego(c.x) + 0.3f) continue;
                var w = c + new Vector2(Rnd(rnd, -0.55f, 0.55f), Rnd(rnd, -0.55f, 0.55f));
                if (!Livre(w.x, w.y, ps, 0f)) continue;
                bool graveto = rnd.NextDouble() < 0.4;
                Por(leito, graveto ? galhos : pedrinha[nl % pedrinha.Length], w.x, w.y,
                    graveto ? Rnd(rnd, 0.35f, 0.55f) : Rnd(rnd, 0.25f, 0.45f), rnd, false, graveto ? 0.02f : 0.04f);
                nl++;
            }
        Debug.Log("[FlorestaSelva] marcas no leito da trilha: " + nl);

        // ---- elementos de caminho: discretos, mas é o que a lanterna pega ----

        // pedra meio enterrada nas duas bordas: desenha o limite da trilha sem
        // virar muro — só a corcova aparece, e ela pega a luz
        var borda = G("BordaDaTrilha");
        var cascalho = new[] { P(JP + "Small Rock 1.prefab"), P(JP + "Small Rock 2.prefab"), P(JP + "Small Rock 3.prefab"), P(JP + "Small Rock 4.prefab") };
        int nbd = 0;
        foreach (var t in Suaves)
            for (int i = 0; i + 1 < t.Length; i++)
            {
                if (rnd.NextDouble() > 0.85) continue;
                Vector2 a = t[i], b = t[i + 1];
                if (Mathf.Abs(a.x) > JX - 0.8f || a.y < JZmin + 0.5f || a.y > JZmax - 0.5f) continue;
                if (Mathf.Abs(a.y - ZCorrego(a.x)) < LarguraCorrego(a.x) + 0.4f) continue;
                var dir = (b - a).normalized;
                float lado = rnd.NextDouble() < 0.5 ? -1f : 1f;
                // às vezes os dois lados no mesmo ponto: estreita a trilha ali
                int quantas = rnd.NextDouble() < 0.25 ? 2 : 1;
                for (int q = 0; q < quantas; q++, lado = -lado)
                {
                    var w = a + new Vector2(-dir.y, dir.x) * lado * Rnd(rnd, 0.95f, 1.2f);
                    var go = Por(borda, cascalho[nbd % cascalho.Length], w.x, w.y, Rnd(rnd, 0.55f, 0.95f), rnd, false, 0f);
                    if (go != null) { var b3 = Limites(go); go.transform.position -= new Vector3(0f, b3.size.y * 0.55f, 0f); }
                    nbd++;
                }
            }
        Debug.Log("[FlorestaSelva] pedras de borda: " + nbd);

        // poça rasa de lama no leito, nos pontos baixos: a lanterna reflete nela
        var agua = AssetDatabase.LoadAssetAtPath<Material>(Pasta + "/M_Agua.mat");
        var pocas = G("PocasDaTrilha");
        int npc = 0;
        foreach (var t in Suaves)
            for (int i = 3; i + 3 < t.Length; i += 1)
            {
                var c = t[i];
                if (Mathf.Abs(c.x) > JX - 1f || c.y < JZmin + 1f || c.y > JZmax - 1f) continue;
                // trilha compactada junta água em qualquer dobra: uma poça a cada
                // 12–15 m, metade das vezes (exigir ponto mais baixo deu só 1 poça)
                float h0 = H(c.x, c.y);
                if (rnd.NextDouble() > 0.5) { i += 15; continue; }
                if (Mathf.Abs(c.y - ZCorrego(c.x)) < LarguraCorrego(c.x) + 1f) continue;
                if (!Livre(c.x, c.y, ps, 0f)) continue;
                var pv = new List<Vector3>(); var pt = new List<int>(); var pu = new List<Vector2>();
                float r0 = Rnd(rnd, 0.35f, 0.65f), y = h0 + 0.035f;
                pv.Add(new Vector3(c.x, y, c.y)); pu.Add(new Vector2(0.5f, 0.5f));
                const int lados = 14;
                for (int k = 0; k < lados; k++)
                {
                    float ang = k * Mathf.PI * 2f / lados;
                    float rr = r0 * (0.65f + 0.6f * Mathf.PerlinNoise(c.x * 3f + k * 0.7f, c.y * 3f));
                    pv.Add(new Vector3(c.x + Mathf.Cos(ang) * rr * 1.5f, y, c.y + Mathf.Sin(ang) * rr));
                    pu.Add(new Vector2(0.5f + Mathf.Cos(ang) * 0.5f, 0.5f + Mathf.Sin(ang) * 0.5f));
                    pt.Add(0); pt.Add(1 + (k + 1) % lados); pt.Add(1 + k);
                }
                if (agua != null) Malha(pocas, "PocaDeTrilha", pv, pt, pu, agua);
                npc++;
                i += 18; // nada de poça colada em poça
            }
        Debug.Log("[FlorestaSelva] poças na trilha: " + npc);

        // pegada de bota em trechos: terra compactada pelo uso — e alguém passou
        var bota = MaterialDePegada();
        var pegadas = G("Pegadas");
        var quad = MalhaDeQuadrado();
        int npg = 0;
        for (int k = 0; k < Suaves.Count; k++)
        {
            var t = Suaves[k];
            bool trecho = false;
            for (int i = 0; i + 1 < t.Length; i++)
            {
                // trechos de 6–10 m com pegada, depois nada: rastro que some e volta
                if (i % 10 == 0) trecho = rnd.NextDouble() < (k == 0 ? 0.55 : 0.35);
                if (!trecho) continue;
                Vector2 a = t[i], b = t[i + 1];
                if (Mathf.Abs(a.x) > JX - 0.5f || a.y < JZmin + 0.5f || a.y > JZmax - 0.5f) continue;
                if (Mathf.Abs(a.y - ZCorrego(a.x)) < LarguraCorrego(a.x) + 0.3f) continue;
                var dir = (b - a).normalized;
                var lado = new Vector2(-dir.y, dir.x);
                // um passo a cada 0,8 m de trilha, alternando pé
                float pe = (i % 2 == 0) ? -0.13f : 0.13f;
                var w = a + lado * pe + lado * Rnd(rnd, -0.08f, 0.08f);
                var go = new GameObject("Pegada", typeof(MeshFilter), typeof(MeshRenderer));
                go.transform.SetParent(pegadas, false);
                go.transform.position = new Vector3(w.x, H(w.x, w.y) + 0.02f, w.y);
                float giro = Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg + Rnd(rnd, -8f, 8f);
                go.transform.rotation = Quaternion.Euler(0f, giro, 0f);
                go.transform.localScale = new Vector3(0.13f * (pe < 0 ? -1f : 1f), 1f, 0.3f);
                go.GetComponent<MeshFilter>().sharedMesh = quad;
                var r = go.GetComponent<MeshRenderer>();
                r.sharedMaterial = bota;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                npg++;
            }
        }
        Debug.Log("[FlorestaSelva] pegadas: " + npg);

        // o que assusta: os que vieram antes e não saíram
        var caidos = new[] {
            new Vector3(-7.5f, 0, -26f), new Vector3(6.8f, 0, -12.5f), new Vector3(-11.5f, 0, 11f),
            new Vector3(3.5f, 0, 33.5f), new Vector3(-13f, 0, -34f), new Vector3(12.5f, 0, 26f) };
        for (int i = 0; i < caidos.Length; i++)
            PorCorpo(mortos, new Vector3(caidos[i].x, 0, caidos[i].z), Rnd(rnd, 0, 360));

        // cova rasa com duas tábuas cruzadas: alguém teve tempo de enterrar
        var tabua = P("Assets/NikolayFedorov/OldVillage/VilagePropsPack/Prefab/Boards/board01.prefab");
        foreach (var c in new[] { new Vector2(-9f, 3.5f), new Vector2(9.5f, 17f), new Vector2(-6f, 39f) })
            for (int i = 0; i < 3; i++)
            {
                float x = c.x + Rnd(rnd, -1.6f, 1.6f), z = c.y + Rnd(rnd, -1.6f, 1.6f);
                var cruz = new GameObject("Cova").transform;
                cruz.SetParent(mortos, false);
                cruz.position = new Vector3(x, H(x, z) + 0.35f, z);
                cruz.rotation = Quaternion.Euler(0, Rnd(rnd, 0, 360), 0);
                for (int k = 0; k < 2; k++)
                {
                    var go = (GameObject)PrefabUtility.InstantiatePrefab(tabua, cruz);
                    if (go == null) continue;
                    SemColisor(go);
                    go.transform.localPosition = Vector3.zero;
                    go.transform.localRotation = Quaternion.Euler(0, 0, k == 0 ? 8f : 92f);
                    go.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
                }
            }

        // bruma rente ao chão no córrego e nas crateras
        var matBruma = Material_Bruma();
        foreach (var c in new[] { new Vector2(-8f, ZCorrego(-8f)), new Vector2(0f, ZCorrego(0f)), new Vector2(9f, ZCorrego(9f)),
                                  new Vector2(-12f, -33f), new Vector2(-14f, -13f), new Vector2(15f, 23f), new Vector2(-15f, 40f) })
            Bruma(bruma, c.x, c.y, matBruma);

        // mata distante nos morros: só aparece na abertura e nas bordas do cone
        // mata de selva barata: Oaky (3 mil triângulos), árvore pequena e samambaia
        // arbórea. As CommonTree de desenho (folha quadrada) saíram daqui também.
        // A escala vem da altura real de cada modelo (o Oaky mede 70 m no arquivo).
        var comuns = new[] { P("Assets/ForestVision/Optimized/Opt_Oaky.prefab"), P("Assets/ForestVision/Optimized/Opt_SmallBranching.prefab"),
                             P("Assets/ForestVision/Optimized/Opt_Oaky.prefab"), P("Assets/ForestVision/Optimized/Valley Fern Tree.prefab") };
        var alturaDoArquivo = new[] { 69.9f, 5.2f, 69.9f, 7.8f };
        int feitas = 0;
        for (int tent = 0; tent < 9000 && feitas < 700; tent++)
        {
            float x = Rnd(rnd, X0 + 4f, X0 + Largura - 4f), z = Rnd(rnd, Z0 + 4f, Z0 + Comprimento - 4f);
            float fora = DistFora(x, z);
            if (fora < 2.5f || fora > 60f) continue;
            // em moitas, não em grade: ruído decide onde a mata fecha
            if (Mathf.PerlinNoise(x * 0.06f + 300f, z * 0.06f) < 0.38f && rnd.NextDouble() < 0.8) continue;
            if (Mathf.Abs(z - ZCorrego(x)) < LarguraCorrego(x) + 1.5f + fora * 0.3f) continue;
            if (DistTrilha(x, z) < 3f) continue;
            // mais denso perto da parede, rala ao longe
            if (rnd.NextDouble() > 1.15f - fora / 55f) continue;
            float sorte = (float)rnd.NextDouble();
            GameObject pref;
            float e;
            if (sorte < 0.14f && palmeira.Count > 0) { pref = palmeira[feitas % palmeira.Count]; e = Rnd(rnd, 0.4f, 0.6f); }
            else if (sorte < 0.24f) { pref = grandes[feitas % 2]; e = Rnd(rnd, 0.8f, 1.8f); }
            else { int q = feitas % comuns.Length; pref = comuns[q]; e = Rnd(rnd, 8f, 13f) / alturaDoArquivo[q]; }
            var go = Por(longe, pref, x, z, e, rnd, false, 0.2f, inclinar: pref == grandes[0] || pref == grandes[1]);
            if (go != null && palmeira.Contains(pref)) Folhas(go, copa);
            feitas++;
        }
        // sub-bosque nos morros: sem ele a encosta aparece pelada entre os troncos
        var sub = new[] { P(JP + "Foliage 3.prefab"), P(JP + "Foliage 2.prefab"), P(JP + "Foliage 3.prefab"), P("Assets/ForestVision/Optimized/ForestBush.prefab") };
        int moitas = 0;
        for (int tent = 0; tent < 6000 && moitas < 700; tent++)
        {
            float x = Rnd(rnd, X0 + 4f, X0 + Largura - 4f), z = Rnd(rnd, Z0 + 4f, Z0 + Comprimento - 4f);
            float fora = DistFora(x, z);
            if (fora < 1.5f || fora > 50f) continue;
            if (Mathf.Abs(z - ZCorrego(x)) < LarguraCorrego(x) + 0.8f + fora * 0.3f) continue;
            var pref = sub[moitas % sub.Length];
            Por(longe, pref, x, z, pref == sub[3] ? Rnd(rnd, 0.5f, 0.8f) : Rnd(rnd, 1.2f, 2.2f), rnd, false, 0.1f);
            moitas++;
        }
        Debug.Log("[FlorestaSelva] mata distante: " + feitas + " arvores, " + moitas + " moitas");

        foreach (Transform g in floresta)
            foreach (var r in g.GetComponentsInChildren<Renderer>())
                GameObjectUtility.SetStaticEditorFlags(r.gameObject, StaticEditorFlags.BatchingStatic);
    }

    /// Mata das faixas que o alargamento abriu (|x| > 17): árvore de copa com
    /// tronco que bloqueia, em manchas (ruído), nunca em cima de trilha nem
    /// de lugar da história. É o que faz o mapa largo ter "contornar": o
    /// caminho reto entre dois pontos quase nunca existe.
    static void MataNova(Transform pai, List<Proibido> ps, System.Random rnd)
    {
        var especies = new[] { P("Assets/ForestVision/Optimized/Opt_Kapok.prefab"), P("Assets/ForestVision/Optimized/Opt_Oaky.prefab"),
                               P("Assets/ForestVision/Optimized/Opt_TallJungle.prefab"), P("Assets/ForestVision/Optimized/Opt_Tall Forest 1.prefab") };
        var lugares = new[] { new Vector2(30.8f, 4.6f), new Vector2(35f, 7.5f), new Vector2(32.3f, 7.6f),
                              new Vector2(-31f, -19.5f), new Vector2(-24.5f, 31f), new Vector2(34.5f, 15f) };
        int copa = LayerMask.NameToLayer("Copa");
        var postas = new List<Vector2>();
        int n = 0;
        for (int tent = 0; tent < 9000 && n < 190; tent++)
        {
            float x = Rnd(rnd, -JX + 1.5f, JX - 1.5f), z = Rnd(rnd, JZmin + 2f, JZmax - 2f);
            if (Mathf.Abs(x) < 17f) continue;
            if (Mathf.PerlinNoise(x * 0.07f + 700f, z * 0.07f + 90f) < 0.42f) continue;
            if (DistTrilha(x, z) < 2.8f) continue;
            if (Mathf.Abs(z - ZCorrego(x)) < LarguraCorrego(x) + 2.5f) continue;
            if (!Livre(x, z, ps, 2f)) continue;
            var q = new Vector2(x, z);
            bool ruim = false;
            foreach (var l in lugares) if ((l - q).sqrMagnitude < 36f) { ruim = true; break; }
            foreach (var c in Crateras) if (Vector2.Distance(q, new Vector2(c.x, c.y)) < c.z + 1.5f) { ruim = true; break; }
            foreach (var o in postas) if ((o - q).sqrMagnitude < 9f) { ruim = true; break; }
            if (ruim) continue;

            var pref = especies[n % especies.Length];
            if (pref == null) continue;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(pref, pai);
            go.transform.position = new Vector3(x, H(x, z) - 0.15f, z);
            go.transform.rotation = Quaternion.Euler(0f, Rnd(rnd, 0f, 360f), 0f);
            SemColisor(go);
            var b = Limites(go);
            float alvo = Rnd(rnd, 8f, 12.5f);
            if (b.size.y > 0.01f) go.transform.localScale *= alvo / b.size.y;
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                bool folha = false;
                foreach (var m in r.sharedMaterials) if (m != null && m.renderQueue == 2450) folha = true;
                if (folha && copa >= 0) r.gameObject.layer = copa;
            }
            // tronco: colisor no mundo, sem herdar a escala da árvore
            var tronco = new GameObject("Tronco");
            tronco.transform.SetParent(pai, false);
            tronco.transform.position = new Vector3(x, H(x, z) + 1.5f, z);
            var cap = tronco.AddComponent<CapsuleCollider>(); cap.radius = 0.4f; cap.height = 3.5f;
            postas.Add(q);
            n++;
        }
        Debug.Log("[FlorestaSelva] mata nova nas faixas: " + n + " árvores");
    }

    /// O córrego vira barreira: parede invisível na linha d'água de ponta a
    /// ponta, menos em três travessias — a trilha do meio (vigiada), o
    /// tronco-ponte a leste e o vau a oeste (raso, mas a água faz barulho).
    /// Quem vem do sul PRECISA escolher uma delas.
    internal static readonly Vector2[] Travessias = { new Vector2(-1.8f, 2.8f), new Vector2(10.2f, 13.6f), new Vector2(-28f, -24.8f) };

    static void Corrego(Transform pai)
    {
        int n = BarreiraDoCorrego(pai);

        // tronco-ponte: um tronco caído atravessado no córrego, x≈11,8
        var tronco = P(PH + "dead_tree_trunk/dead_tree_trunk.fbx");
        if (tronco != null)
        {
            float x = 11.9f, z = ZCorrego(x);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(tronco, pai);
            go.name = "TroncoPonte";
            go.transform.position = new Vector3(x, H(x, z) + 0.15f, z);
            go.transform.rotation = Quaternion.Euler(0f, 8f, 0f);
            SemColisor(go);
            var b = Limites(go);
            float comp = Mathf.Max(b.size.x, b.size.z);
            if (comp > 0.01f) go.transform.localScale *= 5.5f / comp;
            b = Limites(go);
            // o comprimento tem que ir de margem a margem (norte-sul)
            if (b.size.x > b.size.z) go.transform.rotation = Quaternion.Euler(0f, 98f, 0f);
        }

        // vau: água rasa que faz barulho
        {
            float x = -26.4f, z = ZCorrego(x);
            var go = new GameObject("Vau");
            go.transform.SetParent(pai, false);
            go.transform.position = new Vector3(x, H(x, z) + 1f, z);
            var bc = go.AddComponent<BoxCollider>(); bc.isTrigger = true; bc.size = new Vector3(3.6f, 3f, 5f);
            go.AddComponent<VauBarulhento>();
        }
        Debug.Log("[FlorestaSelva] córrego: " + n + " trechos de barreira, 3 travessias");
    }

    /// Cada volume de ocultação vira UMA moita reconhecível, sempre da mesma
    /// receita: feto grande no miolo, arbusto escuro e capim alto na borda,
    /// fechado até a borda da caixa. Antes o esconderijo não tinha visual
    /// próprio — mostrava o que o acaso deixou ali (pachira, galho seco,
    /// casca, caixa de munição) ou nada. O jogador tem que ler "aqui dá pra
    /// se esconder" de longe.
    static void Esconderijos(Transform floresta, Transform nivel)
    {
        var rnd = new System.Random(4242);
        var raiz = new GameObject("Esconderijos").transform; raiz.SetParent(floresta, false);
        var feto = P(JP + "Foliage 1.prefab"); var feto2 = P(JP + "Foliage 3.prefab");
        var escuro = P("Assets/ForestVision/Optimized/ForestBush.prefab"); var capim = P("Assets/ForestVision/Optimized/Opt_Grasses_01.prefab");
        // massa de folha redonda: é o que lê como "moita" vista de cima à noite.
        // Feto e arbusto escuro sozinhos sumiam no chão.
        var massa = P("Assets/ForestVision/Optimized/Opt_SmallBranching.prefab");
        // o que o acaso pôs dentro do esconderijo sai
        var limpar = new[] { "Miudezas", "PilhasDeFolha", "Arbustos", "MargemDaTrilha", "Pedras", "Tocos", "LeitoDaTrilha", "BordaDaTrilha", "BeiraDaTrilha" };
        int nv = 0, np = 0, tirados = 0;
        foreach (var v in nivel.GetComponentsInChildren<ConcealmentVolume>(false))
        {
            var b = v.GetComponent<Collider>().bounds;
            if (b.size.x < 0.5f || b.size.z < 0.5f) continue;
            float mx = b.min.x - 0.4f, Mx = b.max.x + 0.4f, mz = b.min.z - 0.4f, Mz = b.max.z + 0.4f;
            foreach (var g in limpar)
            {
                var grupo = floresta.Find(g); if (grupo == null) continue;
                for (int k = grupo.childCount - 1; k >= 0; k--)
                {
                    var p = grupo.GetChild(k).position;
                    if (p.x > mx && p.x < Mx && p.z > mz && p.z < Mz) { Object.DestroyImmediate(grupo.GetChild(k).gameObject); tirados++; }
                }
            }
            // árvore do Nivel com o tronco dentro da moita: só a copa fica, tudo bem
            var moita = new GameObject("Moita_" + v.name).transform; moita.SetParent(raiz, false);
            float cx = b.center.x, cz = b.center.z, rx = b.extents.x, rz = b.extents.z;
            void Planta(GameObject pref, float u, float w, float e)
            {
                var go = Por(moita, pref, cx + u * rx, cz + w * rz, e, rnd, false, 0.06f);
                if (go != null) np++;
            }
            // miolo: massa de folha fechando a caixa (1,6–2 m de altura)
            int nMassa = Mathf.Max(2, Mathf.RoundToInt(b.size.x * b.size.z / 2.4f));
            for (int k = 0; k < nMassa; k++)
            {
                float a = k * 2.4f + Rnd(rnd, -0.3f, 0.3f), r = k == 0 ? 0f : Rnd(rnd, 0.3f, 0.55f);
                Planta(massa, Mathf.Cos(a) * r, Mathf.Sin(a) * r, Rnd(rnd, 0.31f, 0.39f));
            }
            // anel: mais massa de folha na borda, com feto verde baixo entre elas.
            // Sem o feto-estrela (Foliage 1): a 1,5× ele media 5–7 m de fronde
            // preta espetada e cobria a moita — de cima parecia galho retorcido.
            int anel = 5 + (int)((rx + rz) * 1.0f);
            for (int k = 0; k < anel; k++)
            {
                float a = k * Mathf.PI * 2f / anel + Rnd(rnd, -0.2f, 0.2f), r = Rnd(rnd, 0.6f, 0.85f);
                if (k % 2 == 0) Planta(massa, Mathf.Cos(a) * r, Mathf.Sin(a) * r, Rnd(rnd, 0.26f, 0.33f));
                else Planta(feto2, Mathf.Cos(a) * r, Mathf.Sin(a) * r, Rnd(rnd, 0.9f, 1.2f));
            }
            // capim alto saindo da borda
            for (int k = 0; k < 4; k++)
            {
                float a = Rnd(rnd, 0f, Mathf.PI * 2f);
                Planta(capim, Mathf.Cos(a) * 0.95f, Mathf.Sin(a) * 0.95f, Rnd(rnd, 0.6f, 0.8f));
            }
            nv++;
        }
        Debug.Log("[FlorestaSelva] esconderijos: " + nv + " moitas, " + np + " plantas, " + tirados + " objetos soltos tirados de dentro");
    }


    /// O corrego so se atravessa em tres lugares — mas quem barra tem que ser
    /// PEDRA e TRONCO, nao caixa invisivel. Antes eram 57 blocos sem malha
    /// nenhuma: o jogador batia no nada e o mapa parecia quebrado.
    ///
    /// Cada objeto aqui e visivel e o colisor mora dentro dele. No fim o
    /// proprio gerador anda pela margem conferindo se sobrou vao pra capsula
    /// do jogador (1 m de largura) passar, e tapa o que achar.
    static int BarreiraDoCorrego(Transform pai)
    {
        var rnd = new System.Random(9110);
        var grandes = new[] { P(JP + "Large Rock 2.prefab"), P(JP + "Large Rock 1.prefab") };
        var pequenas = new[] { P(JP + "Small Rock 1.prefab"), P(JP + "Small Rock 2.prefab"),
                               P(JP + "Small Rock 3.prefab"), P(JP + "Small Rock 4 Moss.prefab") };
        var tora = P("Assets/NikolayFedorov/OldVillage/VilagePropsPack/Prefab/Logs/log01.prefab");

        bool Travessia(float x)
        {
            foreach (var t in Travessias) if (x > t.x - 0.3f && x < t.y + 0.3f) return true;
            return false;
        }

        // poe um objeto no leito e devolve o quanto ele cobre ao longo do corrego.
        // tapaVao = pedra curta, pra fechar fresta rente a uma travessia sem
        // invadir a passagem.
        float Por(float x, int tipo, bool tapaVao = false)
        {
            float z = ZCorrego(x);
            float dz = ZCorrego(x + 0.8f) - ZCorrego(x - 0.8f);
            float anguloDoLeito = Mathf.Atan2(dz, 1.6f) * Mathf.Rad2Deg;
            GameObject go = null;
            float cobre = 1.2f;

            if (tipo == 0 && tora != null)
            {
                // tronco caido deitado no leito, acompanhando a curva
                go = (GameObject)PrefabUtility.InstantiatePrefab(tora, pai);
                go.name = "Tronco_Caido";
                float escala = Rnd(rnd, 0.75f, 1.05f);
                go.transform.localScale *= escala;
                go.transform.rotation = Quaternion.Euler(Rnd(rnd, -6f, 6f), anguloDoLeito + 90f + Rnd(rnd, -7f, 7f), Rnd(rnd, -8f, 8f));
                go.transform.position = new Vector3(x, H(x, z) + 0.18f * escala, z + Rnd(rnd, -0.25f, 0.25f));
                cobre = 4.8f * escala * 0.8f;
            }
            else
            {
                var pref = tapaVao ? P(JP + "Large Rock 2.prefab") : grandes[rnd.Next(grandes.Length)];
                if (pref == null) return 1f;
                go = (GameObject)PrefabUtility.InstantiatePrefab(pref, pai);
                go.name = "Pedra_Corrego";
                float escala = tapaVao ? Rnd(rnd, 0.4f, 0.55f) : Rnd(rnd, 0.55f, 0.85f);
                go.transform.localScale *= escala;
                go.transform.rotation = Quaternion.Euler(Rnd(rnd, -12f, 12f), Rnd(rnd, 0f, 360f), Rnd(rnd, -12f, 12f));
                go.transform.position = new Vector3(x, H(x, z) - Rnd(rnd, 0.15f, 0.4f), z + Rnd(rnd, -0.3f, 0.3f));
                var bb = Limites(go);
                cobre = Mathf.Max(0.9f, bb.size.x * 0.75f);
                // pedrinhas em volta pra base nao ficar um corte seco no chao
                for (int k = 0; k < 2; k++)
                {
                    var pq = pequenas[rnd.Next(pequenas.Length)];
                    if (pq == null) continue;
                    var s = (GameObject)PrefabUtility.InstantiatePrefab(pq, pai);
                    s.name = "Pedra_Miuda";
                    float ex = Rnd(rnd, 0.8f, 1.6f);
                    s.transform.localScale *= ex;
                    float sx = x + Rnd(rnd, -0.7f, 0.7f), sz = z + Rnd(rnd, -0.9f, 0.9f);
                    s.transform.position = new Vector3(sx, H(sx, sz) - Rnd(rnd, 0.05f, 0.2f), sz);
                    s.transform.rotation = Quaternion.Euler(Rnd(rnd, -20f, 20f), Rnd(rnd, 0f, 360f), Rnd(rnd, -20f, 20f));
                    SemColisor(s);
                }
            }

            if (go == null) return 1f;
            SemColisor(go);                       // fora colisor de prefab; o nosso e do tamanho certo

            // tronco de 4 m posto rente a uma travessia invadia a passagem e
            // fechava a rota: se a PEGADA dele encosta na travessia, ele sai
            var bb2 = Limites(go);
            float folga = tapaVao ? 0.05f : 0.4f;
            foreach (var t in Travessias)
            {
                if (bb2.max.x + folga <= t.x || bb2.min.x - folga >= t.y) continue;
                if (!tapaVao) { Object.DestroyImmediate(go); return cobre; }
                // fechando fresta: em vez de desistir, empurra a pedra pra fora
                float empurra = x < (t.x + t.y) * 0.5f ? (t.x - folga) - bb2.max.x : (t.y + folga) - bb2.min.x;
                go.transform.position += new Vector3(empurra, 0f, 0f);
                bb2 = Limites(go);
            }

            Bloquear(go, cobre);
            return cobre;
        }

        int n = 0;
        for (float x = -JX; x <= JX; )
        {
            if (Travessia(x)) { x += 0.6f; continue; }
            // tronco cobre muito e e barato; pedra fecha o resto
            int tipo = rnd.NextDouble() < 0.45 ? 0 : 1;
            float cobre = Por(x, tipo);
            n++;
            x += Mathf.Max(0.9f, cobre * 0.85f);
        }

        // ---- conferencia: anda pela linha do corrego e tapa vao de verdade
        int tapados = 0;
        for (float x = -JX; x <= JX; x += 0.35f)
        {
            if (Travessia(x)) continue;
            float z = ZCorrego(x);
            var ponto = new Vector3(x, H(x, z) + 0.9f, z);
            bool temCoisa = false;
            foreach (var c in Physics.OverlapSphere(ponto, 0.5f, ~0, QueryTriggerInteraction.Ignore))
                if (!(c is TerrainCollider)) { temCoisa = true; break; }   // o terreno nao conta como barreira
            if (temCoisa) continue;
            Por(x, 1, true);
            tapados++;
            n++;
        }
        Debug.Log("[FlorestaSelva] barreira do corrego: " + n + " pedras/troncos (" + tapados + " tapando vao), 3 travessias");
        return n;
    }

    /// Colisor do tamanho do que se ve, dentro do objeto. Caixa alinhada ao
    /// mundo porque o corrego corre quase reto em x — e caixa dentro de pedra
    /// ninguem percebe; caixa sem pedra, todo mundo percebe.
    static void Bloquear(GameObject go, float comprimentoMin)
    {
        var b = Limites(go);
        if (b.size == Vector3.zero) return;
        var col = new GameObject("Colisor");
        col.transform.SetParent(go.transform, true);
        col.transform.position = new Vector3(b.center.x, b.center.y, b.center.z);
        col.transform.rotation = Quaternion.identity;
        col.transform.localScale = Vector3.one;
        var bc = col.AddComponent<BoxCollider>();
        // um palmo a mais de cada lado pra nao sobrar fresta entre uma pedra e
        // a outra, e altura suficiente pra ninguem passar por cima
        var tam = new Vector3(Mathf.Max(b.size.x, comprimentoMin) + 0.2f, Mathf.Max(b.size.y, 1.6f), Mathf.Max(b.size.z, 1.1f) + 0.2f);
        bc.size = new Vector3(tam.x / Mathf.Max(0.001f, col.transform.lossyScale.x),
                              tam.y / Mathf.Max(0.001f, col.transform.lossyScale.y),
                              tam.z / Mathf.Max(0.001f, col.transform.lossyScale.z));
        GameObjectUtility.SetStaticEditorFlags(col, StaticEditorFlags.NavigationStatic);
    }

    /// Fronde de palmeira vai pra camada Copa: some quando o jogador passa embaixo.
    static void Folhas(GameObject go, int copa)
    {
        if (copa < 0) return;
        foreach (var r in go.GetComponentsInChildren<Renderer>())
            foreach (var m in r.sharedMaterials)
                if (m != null && (m.renderQueue == 2450 || m.name.ToLower().Contains("leaf") || m.name.ToLower().Contains("frond")))
                { r.gameObject.layer = copa; break; }
    }

    // ---------------------------------------------------------------- copa e navegação

    static void Canopia()
    {
        // Luz de jogo. Com o cone de visão, a escuridão já vem da malha preta fora
        // dele; a cena não precisa mais ser escura por si. A neblina era por
        // distância da câmera, que fica 25 m acima: comia metade da imagem.
        var lp = Object.FindFirstObjectByType<LightingPreset>();
        if (lp != null)
        {
            Undo.RecordObject(lp, "luz");
            lp.jogoAmbiente = new Color(0.16f, 0.18f, 0.22f);
            lp.jogoSol = 0.45f;
            lp.jogoDensidade = 0.006f;
            EditorUtility.SetDirty(lp);
        }
        var perfil = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.VolumeProfile>("Assets/_Game/Materiais/VP_Darkwood.asset");
        if (perfil != null && perfil.TryGet(out UnityEngine.Rendering.Universal.ColorAdjustments ca))
        {
            ca.postExposure.Override(0.8f);
            ca.contrast.Override(10f);
            EditorUtility.SetDirty(perfil);
        }

        // a copa volta a ser desenhada de cima; FolhagemTransparente abre ela em volta do jogador
        var cam = GameObject.Find("_Setup/Cam_TopDown");
        int copa = LayerMask.NameToLayer("Copa");
        if (cam != null && copa >= 0)
        {
            var c = cam.GetComponent<Camera>();
            Undo.RecordObject(c, "copa");
            c.cullingMask |= 1 << copa;
            EditorUtility.SetDirty(c);
        }
        // fora do cone o mundo aparece um pouco (antes 90% coberto): é o que
        // deixa a copa preta da paralaxe se destacar nos cantos
        var escuro = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Materiais/M_Escuridao.mat");
        if (escuro != null)
        {
            var c = escuro.GetColor("_Color");
            escuro.SetColor("_Color", new Color(c.r, c.g, c.b, 0.78f));
            EditorUtility.SetDirty(escuro);
        }
        var vis = Object.FindFirstObjectByType<VisaoDarkwood>();
        if (vis != null)
        {
            Undo.RecordObject(vis, "olho");
            // de dentro da cratera ou do córrego o jogador ainda enxerga por cima da borda
            vis.alturaDoOlho = 1.5f;
            EditorUtility.SetDirty(vis);
        }
    }

    internal static void AssarNavegacao()
    {
        var s = Object.FindFirstObjectByType<NavMeshSurface>();
        if (s == null) return;
        string caminho = s.navMeshData != null ? AssetDatabase.GetAssetPath(s.navMeshData) : null;
        s.BuildNavMesh();
        if (string.IsNullOrEmpty(caminho)) caminho = "Assets/_Game/Scenes/Selva/NavMesh-Navegacao.asset";
        var pasta = Path.GetDirectoryName(caminho).Replace('\\', '/');
        if (!AssetDatabase.IsValidFolder(pasta)) AssetDatabase.CreateFolder(Path.GetDirectoryName(pasta).Replace('\\', '/'), Path.GetFileName(pasta));
        AssetDatabase.DeleteAsset(caminho);
        AssetDatabase.CreateAsset(s.navMeshData, caminho);
        Undo.RecordObject(s, "navmesh");
        EditorUtility.SetDirty(s);
        Debug.Log("[FlorestaSelva] navmesh em " + caminho);
    }
}
