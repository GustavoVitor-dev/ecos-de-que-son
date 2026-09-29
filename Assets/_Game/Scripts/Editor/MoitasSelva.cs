using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// Importa as plantas baixadas do Poly Haven (CC0) do jeito que o resto do
/// projeto espera: textura de cor com o recorte da folha no alfa, normal map
/// marcado como normal, material URP com corte de alfa e dois lados.
///
/// O pacote vem com a cor (.jpg, sem alfa) e o recorte num arquivo separado
/// (_alpha.png). Sem juntar os dois, cada folha vira um retângulo — que é
/// exatamente a cara de "mato de papelão" que a gente não quer.
public static class MoitasSelva
{
    const string PH = "Assets/_Game/Modelos/PolyHaven/";

    [MenuItem("Ecos/Selva/Importar plantas novas")]
    public static void ImportarMenu()
    {
        int n = 0;
        foreach (var pasta in Directory.GetDirectories(PH))
        {
            string nome = Path.GetFileName(pasta);
            if (nome == "Materiais") continue;
            string fbx = PH + nome + "/" + nome + ".fbx";
            if (!File.Exists(fbx)) continue;
            if (Importar(nome)) n++;
        }
        AssetDatabase.SaveAssets();
        Debug.Log("[PLANTAS] " + n + " modelos arrumados");
    }

    /// Devolve true se mexeu em alguma coisa.
    public static bool Importar(string nome)
    {
        string pasta = PH + nome + "/";
        string fbx = pasta + nome + ".fbx";
        var imp = AssetImporter.GetAtPath(fbx) as ModelImporter;
        if (imp == null) return false;

        // texturas: normal como normal, resto como cor
        foreach (var arq in Directory.GetFiles(pasta))
        {
            string ext = Path.GetExtension(arq).ToLower();
            if (ext != ".png" && ext != ".jpg" && ext != ".exr") continue;
            string caminho = arq.Replace('\\', '/').Substring(arq.IndexOf("Assets"));
            var ti = AssetImporter.GetAtPath(caminho) as TextureImporter;
            if (ti == null) continue;
            bool normal = caminho.Contains("_nor_");
            bool dados = caminho.Contains("_rough") || caminho.Contains("_alpha") || caminho.Contains("_disp") || caminho.Contains("_arm");
            var tipo = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            if (ti.textureType != tipo || ti.sRGBTexture == (normal || dados) || !ti.isReadable)
            {
                ti.textureType = tipo;
                ti.sRGBTexture = !(normal || dados);
                ti.isReadable = true;
                ti.maxTextureSize = 2048;
                ti.SaveAndReimport();
            }
        }

        // cor + recorte numa textura só
        var baseMap = CorComRecorte(pasta, nome);

        // material por material embutido no fbx
        bool mexeu = false;
        var mats = new List<Material>();
        foreach (var o in AssetDatabase.LoadAllAssetsAtPath(fbx))
            if (o is Material m) mats.Add(m);
        var nomesDeMaterial = new List<string>();
        foreach (var m in mats) nomesDeMaterial.Add(m.name);
        if (nomesDeMaterial.Count == 0) nomesDeMaterial.Add(nome);

        foreach (var nomeMat in nomesDeMaterial)
        {
            string arqMat = PH + "Materiais/PH_" + nome + (nomeMat == nome ? "" : "_" + nomeMat) + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(arqMat);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                Directory.CreateDirectory(PH + "Materiais");
                AssetDatabase.CreateAsset(mat, arqMat);
                mexeu = true;
            }
            var cor = baseMap ?? Textura(pasta, nomeMat, "_diff") ?? Textura(pasta, nome, "_diff");
            var normal = Textura(pasta, nomeMat, "_nor_gl") ?? Textura(pasta, nome, "_nor_gl");
            if (cor != null) mat.SetTexture("_BaseMap", cor);
            if (normal != null) { mat.SetTexture("_BumpMap", normal); mat.EnableKeyword("_NORMALMAP"); }
            mat.SetFloat("_Smoothness", 0.12f);
            mat.SetFloat("_Metallic", 0f);
            // folha é fina: corte de alfa e dois lados, senão some de um ângulo
            mat.SetFloat("_AlphaClip", 1f);
            mat.SetFloat("_Cutoff", 0.45f);
            mat.EnableKeyword("_ALPHATEST_ON");
            mat.SetOverrideTag("RenderType", "TransparentCutout");
            mat.renderQueue = 2450;
            mat.SetFloat("_Cull", 0f);
            mat.doubleSidedGI = true;
            EditorUtility.SetDirty(mat);

            imp.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), nomeMat), mat);
            mexeu = true;
        }
        if (mexeu)
        {
            imp.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            imp.SaveAndReimport();
        }
        return mexeu;
    }


    // ---------------------------------------------------------------- moitas

    const string FV = "Assets/ForestVision/Optimized/";

    [MenuItem("Ecos/Selva/Refazer moitas")]
    public static void MontarMenu()
    {
        var nivel = GameObject.Find("Nivel");
        if (nivel == null) { Debug.LogError("[MOITAS] sem Nivel na cena"); return; }
        Montar(nivel.transform);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(nivel.scene);
        UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
    }

    /// Refaz TODAS as moitas do mapa. Cada uma e um aglomerado de plantas com
    /// a zona de camuflagem do tamanho dele - nunca uma sem a outra.
    ///
    /// Elas nascem na beira das trilhas (e onde a camuflagem interessa: voce
    /// anda pela trilha e pula pro mato quando a lanterna aparece) e num raio
    /// em volta da ronda de cada guarda.
    public static int Montar(Transform nivel)
    {
        // 1. fora as caixas invisiveis da cena antiga e as moitas de antes
        int apagados = 0;
        foreach (var v in nivel.GetComponentsInChildren<ConcealmentVolume>(true))
        {
            if (v.GetComponentInParent<Moita>() != null) continue;
            Object.DestroyImmediate(v.gameObject);
            apagados++;
        }
        var velhas = nivel.Find("Moitas");
        if (velhas != null) Object.DestroyImmediate(velhas.gameObject);
        var esconderijos = nivel.Find("Floresta/Esconderijos");
        if (esconderijos != null) Object.DestroyImmediate(esconderijos.gameObject);

        var raiz = new GameObject("Moitas").transform;
        raiz.SetParent(nivel, false);

        var rnd = new System.Random(7717);
        var pontos = Pontos(rnd);
        foreach (var p in pontos) Uma(raiz, p, rnd);

        Debug.Log("[MOITAS] " + pontos.Count + " moitas novas (" + apagados + " caixas velhas apagadas)");
        return pontos.Count;
    }

    /// Onde cabe moita: beira de trilha e arredores das rondas, em chao
    /// pisavel, fora do corrego, das crateras e dos lugares da historia.
    static List<Vector3> Pontos(System.Random rnd)
    {
        var achados = new List<Vector3>();
        var proibido = new List<Vector2>();
        foreach (var i in Object.FindObjectsByType<Interagivel>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            proibido.Add(new Vector2(i.transform.position.x, i.transform.position.z));
        foreach (var nome in new[] { "Poco", "Cabana", "Portao_Norte", "Corpo_Unidade" })
        {
            var go = GameObject.Find(nome);
            if (go != null) proibido.Add(new Vector2(go.transform.position.x, go.transform.position.z));
        }

        bool Cabe(Vector3 p, float espaco)
        {
            if (Mathf.Abs(p.x) > FlorestaSelva.JX - 2f || p.z < FlorestaSelva.JZmin + 2f || p.z > FlorestaSelva.JZmax - 2f) return false;
            if (Mathf.Abs(p.z - FlorestaSelva.ZCorrego(p.x)) < FlorestaSelva.LarguraCorrego(p.x) + 1.6f) return false;
            if (FlorestaSelva.DistTrilha(p.x, p.z) < 1.5f) return false;      // nao tapa a trilha
            foreach (var q in proibido) if (Vector2.Distance(new Vector2(p.x, p.z), q) < 3.2f) return false;
            foreach (var a in achados) if (Vector3.Distance(a, p) < espaco) return false;
            // chao ingreme demais nao recebe moita (parece flutuar)
            float h = FlorestaSelva.H(p.x, p.z);
            float inclina = Mathf.Max(Mathf.Abs(FlorestaSelva.H(p.x + 0.8f, p.z) - h), Mathf.Abs(FlorestaSelva.H(p.x, p.z + 0.8f) - h));
            return inclina < 0.55f;
        }

        // 1. beira das trilhas, alternando os lados
        int lado = 1;
        foreach (var t in FlorestaSelva.Suaves)
        {
            float andado = 0f;
            for (int i = 0; i + 1 < t.Length; i++)
            {
                var a = t[i]; var b = t[i + 1];
                andado += Vector2.Distance(a, b);
                if (andado < 7f + (float)rnd.NextDouble() * 4f) continue;
                andado = 0f;
                var dir = (b - a).normalized;
                var perp = new Vector2(-dir.y, dir.x) * lado;
                lado = -lado;
                float dist = 2f + (float)rnd.NextDouble() * 1.6f;
                var q = a + perp * dist;
                var p = new Vector3(q.x, 0f, q.y);
                if (Cabe(p, 5.5f)) achados.Add(p);
            }
        }

        // 2. em volta da ronda dos guardas: e onde a moita vira decisao
        foreach (var g in Object.FindObjectsByType<GuardAI>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (g.waypoints == null) continue;
            foreach (var w in g.waypoints)
            {
                if (w == null) continue;
                for (int k = 0; k < 3; k++)
                {
                    float ang = (float)rnd.NextDouble() * Mathf.PI * 2f;
                    float r = 3.5f + (float)rnd.NextDouble() * 3.5f;
                    var p = w.position + new Vector3(Mathf.Cos(ang) * r, 0f, Mathf.Sin(ang) * r);
                    p.y = 0f;
                    if (Cabe(p, 5f)) { achados.Add(p); break; }
                }
            }
        }
        return achados;
    }

    /// Uma moita: massa de folha no miolo, folha larga de selva em volta,
    /// capim saindo da borda - e a zona de camuflagem do tamanho disso.
    static void Uma(Transform raiz, Vector3 centro, System.Random rnd)
    {
        float raio = 1.15f + (float)rnd.NextDouble() * 0.55f;
        var moita = new GameObject("Moita").transform;
        moita.SetParent(raiz, false);
        moita.position = new Vector3(centro.x, FlorestaSelva.H(centro.x, centro.z), centro.z);

        var massa = FlorestaSelva.P(FV + "Opt_SmallBranching.prefab");
        var capim = FlorestaSelva.P(FV + "Opt_Grasses_01.prefab");
        var folhaLarga = new[]
        {
            FlorestaSelva.P(PH + "anthurium_botany_01/anthurium_botany_01.fbx"),
            FlorestaSelva.P(PH + "calathea_orbifolia_01/calathea_orbifolia_01.fbx"),
            FlorestaSelva.P(PH + "fern_02/fern_02.fbx"),
        };
        var baixas = new[]
        {
            FlorestaSelva.P(PH + "shrub_03/shrub_03.fbx"),
            FlorestaSelva.P(PH + "weed_plant_02/weed_plant_02.fbx"),
            FlorestaSelva.P(PH + "shrub_01/shrub_01.fbx"),
        };

        void Por(GameObject pref, Vector3 pos, float escala, float inclinacao)
        {
            if (pref == null) return;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(pref, moita);
            go.transform.position = new Vector3(pos.x, FlorestaSelva.H(pos.x, pos.z) - 0.05f, pos.z);
            go.transform.rotation = Quaternion.Euler(Rnd(rnd, -inclinacao, inclinacao), Rnd(rnd, 0f, 360f), Rnd(rnd, -inclinacao, inclinacao));
            go.transform.localScale *= escala;
            FlorestaSelva.SemColisor(go);
        }

        // miolo: a massa escura que le como moita de cima
        int nMassa = 3 + Mathf.RoundToInt(raio);
        for (int k = 0; k < nMassa; k++)
        {
            float a = k * 2.39996f + Rnd(rnd, -0.3f, 0.3f);
            float r = k == 0 ? 0f : raio * Rnd(rnd, 0.25f, 0.6f);
            Por(massa, centro + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r), Rnd(rnd, 0.34f, 0.46f), 6f);
        }
        // folha larga de selva: e o que faz a silhueta parecer Vietna e nao parque
        int nFolha = 4 + rnd.Next(3);
        for (int k = 0; k < nFolha; k++)
        {
            float a = k * Mathf.PI * 2f / nFolha + Rnd(rnd, -0.3f, 0.3f);
            float r = raio * Rnd(rnd, 0.35f, 0.8f);
            Por(folhaLarga[rnd.Next(folhaLarga.Length)], centro + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r), Rnd(rnd, 0.5f, 0.85f), 12f);
        }
        // borda: planta baixa e capim, pra moita nao terminar num corte seco
        int nBorda = 4 + rnd.Next(3);
        for (int k = 0; k < nBorda; k++)
        {
            float a = Rnd(rnd, 0f, Mathf.PI * 2f);
            float r = raio * Rnd(rnd, 0.85f, 1.15f);
            var p = centro + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
            if (k % 2 == 0) Por(baixas[rnd.Next(baixas.Length)], p, Rnd(rnd, 0.45f, 0.75f), 8f);
            else Por(capim, p, Rnd(rnd, 0.55f, 0.8f), 5f);
        }

        // a zona de camuflagem: do tamanho da folhagem, nao do capricho de quem
        // arrastou a caixa na cena
        var zona = new GameObject("Zona").transform;
        zona.SetParent(moita, false);
        zona.localPosition = new Vector3(0f, 0.9f, 0f);
        var col = zona.gameObject.AddComponent<CapsuleCollider>();
        col.isTrigger = true;
        col.radius = raio * 0.85f;
        col.height = 2.2f;
        var cv = zona.gameObject.AddComponent<ConcealmentVolume>();
        cv.strength = 0.22f;
        cv.rustleRadius = 4.5f;

        var m = moita.gameObject.AddComponent<Moita>();
        m.raio = raio;
        m.zona = cv;
    }

    static float Rnd(System.Random r, float a, float b) => a + (float)r.NextDouble() * (b - a);

    static Texture2D Textura(string pasta, string nome, string sufixo)
    {
        foreach (var arq in Directory.GetFiles(pasta))
        {
            string c = arq.Replace('\\', '/');
            if (!c.Contains(sufixo)) continue;
            if (!c.Contains(nome) && !c.Contains(nome.Replace("_01", ""))) continue;
            c = c.Substring(c.IndexOf("Assets"));
            var t = AssetDatabase.LoadAssetAtPath<Texture2D>(c);
            if (t != null) return t;
        }
        return null;
    }

    /// Junta a cor (jpg) com o recorte (_alpha.png) numa PNG só, que é o que o
    /// corte de alfa do URP lê.
    static Texture2D CorComRecorte(string pasta, string nome)
    {
        var diff = Textura(pasta, nome, "_diff");
        var alfa = Textura(pasta, nome, "_alpha");
        if (diff == null || alfa == null) return null;
        string saida = pasta + nome + "_base.png";
        var pronta = AssetDatabase.LoadAssetAtPath<Texture2D>(saida);
        if (pronta != null) return pronta;

        var tmp = new Texture2D(diff.width, diff.height, TextureFormat.RGBA32, false);
        var cores = diff.GetPixels();
        var recorte = alfa.width == diff.width && alfa.height == diff.height
            ? alfa.GetPixels()
            : Redimensionar(alfa, diff.width, diff.height);
        for (int i = 0; i < cores.Length; i++) cores[i].a = recorte[i].r;
        tmp.SetPixels(cores);
        tmp.Apply();
        File.WriteAllBytes(saida, tmp.EncodeToPNG());
        Object.DestroyImmediate(tmp);
        AssetDatabase.ImportAsset(saida, ImportAssetOptions.ForceUpdate);
        var ti = AssetImporter.GetAtPath(saida) as TextureImporter;
        if (ti != null)
        {
            ti.alphaIsTransparency = true;
            ti.alphaSource = TextureImporterAlphaSource.FromInput;
            ti.maxTextureSize = 2048;
            ti.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(saida);
    }

    static Color[] Redimensionar(Texture2D t, int w, int h)
    {
        var fora = new Color[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                fora[y * w + x] = t.GetPixelBilinear(x / (float)w, y / (float)h);
        return fora;
    }
}
