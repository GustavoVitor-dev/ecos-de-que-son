using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// Arranca as pernas de um corpo humanoide e pendura as tripas no corte.
///
/// O corte é feito na MALHA, não na escala dos ossos: cada triângulo cujos
/// vértices pesam mais na perna que no resto é jogado fora, em todos os LODs.
/// Assim sobra um coto aberto de verdade na bacia, e é ele que as tripas
/// tampam — um corpo com as pernas encolhidas até zero fica com a pele
/// esticada num bico, e se vê na hora que é truque.
///
/// As tripas são correntes de cápsulas com junta: penduram quando ele sobe
/// pela corda e se espalham no chão quando ele cai. Entram na física do
/// Cadaver pelo componente Viscera.
public static class CorpoRasgado
{
    const string PASTA = "Assets/_Game/Modelos";

    public static void Estripar(GameObject corpo)
    {
        var an = corpo.GetComponentInChildren<Animator>();
        if (an == null || !an.isHuman) { Debug.LogWarning("[CORPO] sem rig humanoide"); return; }

        if (!AssetDatabase.IsValidFolder(PASTA)) AssetDatabase.CreateFolder("Assets/_Game", "Modelos");

        // tudo da coxa pra baixo, dos dois lados
        var daPerna = new HashSet<Transform>();
        void Marcar(HumanBodyBones b)
        {
            var t = an.GetBoneTransform(b);
            if (t == null) return;
            foreach (var d in t.GetComponentsInChildren<Transform>(true)) daPerna.Add(d);
        }
        Marcar(HumanBodyBones.LeftUpperLeg);
        Marcar(HumanBodyBones.RightUpperLeg);

        int cortadas = 0;
        foreach (var smr in corpo.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            var nova = Cortar(smr, daPerna, corpo.name);
            if (nova != null) { smr.sharedMesh = nova; cortadas++; }
        }
        Debug.Log("[CORPO] " + cortadas + " malhas cortadas na cintura");

        // a física das pernas some junto
        foreach (var t in daPerna)
        {
            if (t == null) continue;
            var j = t.GetComponent<CharacterJoint>(); if (j != null) Object.DestroyImmediate(j);
            var c = t.GetComponent<Collider>(); if (c != null) Object.DestroyImmediate(c);
            var r = t.GetComponent<Rigidbody>(); if (r != null) Object.DestroyImmediate(r);
        }

        var cad = corpo.GetComponent<Cadaver>();
        if (cad == null) cad = corpo.AddComponent<Cadaver>();
        cad.semPernas = true;

        Tripas(corpo, an);
        cad.Montar();
        EditorUtility.SetDirty(cad);
        EditorUtility.SetDirty(corpo);
    }

    /// Joga fora os triângulos da perna. Os vértices ficam (não compensa
    /// remapear índice de malha com bone weights por causa de meio kB).
    static Mesh Cortar(SkinnedMeshRenderer smr, HashSet<Transform> daPerna, string nome)
    {
        var malha = smr.sharedMesh;
        if (malha == null || !malha.isReadable) return null;

        var ossos = smr.bones;
        var ehPerna = new bool[ossos.Length];
        for (int i = 0; i < ossos.Length; i++)
            ehPerna[i] = ossos[i] != null && daPerna.Contains(ossos[i]);

        var pesos = malha.boneWeights;
        if (pesos == null || pesos.Length != malha.vertexCount) return null;

        var vertDaPerna = new bool[malha.vertexCount];
        for (int v = 0; v < pesos.Length; v++)
        {
            var p = pesos[v];
            float w = 0f;
            if (p.boneIndex0 < ehPerna.Length && ehPerna[p.boneIndex0]) w += p.weight0;
            if (p.boneIndex1 < ehPerna.Length && ehPerna[p.boneIndex1]) w += p.weight1;
            if (p.boneIndex2 < ehPerna.Length && ehPerna[p.boneIndex2]) w += p.weight2;
            if (p.boneIndex3 < ehPerna.Length && ehPerna[p.boneIndex3]) w += p.weight3;
            vertDaPerna[v] = w > 0.5f;
        }

        var nova = Object.Instantiate(malha);
        nova.name = nome + "_" + malha.name + "_semPernas";
        int jogados = 0;
        for (int s = 0; s < malha.subMeshCount; s++)
        {
            var tris = malha.GetTriangles(s);
            var fica = new List<int>(tris.Length);
            for (int i = 0; i < tris.Length; i += 3)
            {
                if (vertDaPerna[tris[i]] || vertDaPerna[tris[i + 1]] || vertDaPerna[tris[i + 2]]) { jogados++; continue; }
                fica.Add(tris[i]); fica.Add(tris[i + 1]); fica.Add(tris[i + 2]);
            }
            nova.SetTriangles(fica, s, true);
        }
        if (jogados == 0) { Object.DestroyImmediate(nova); return null; }
        nova.RecalculateBounds();

        string caminho = AssetDatabase.GenerateUniqueAssetPath(PASTA + "/" + nova.name + ".asset");
        AssetDatabase.CreateAsset(nova, caminho);
        return nova;
    }

    /// Quatro alças de tripa penduradas no coto, cada uma com 4 gomos.
    static void Tripas(GameObject corpo, Animator an)
    {
        foreach (var velha in corpo.GetComponentsInChildren<Viscera>(true))
            if (velha != null) Object.DestroyImmediate(velha.gameObject);

        var quadril = an.GetBoneTransform(HumanBodyBones.Hips);
        if (quadril == null) return;
        var quadrilRb = quadril.GetComponent<Rigidbody>();

        var mat = MaterialDeTripa();
        float escala = Mathf.Max(0.01f, corpo.transform.lossyScale.x);
        var rnd = new System.Random(7);
        float F(float a, float b) => a + (float)rnd.NextDouble() * (b - a);

        for (int fio = 0; fio < 3; fio++)
        {
            Rigidbody pai = quadrilRb;
            // saem de pontos diferentes do corte, não todas do mesmo buraco
            float ang = fio * 120f + F(-25f, 25f);
            Vector3 saida = quadril.position
                + Quaternion.Euler(0f, ang, 0f) * new Vector3(F(0.05f, 0.10f), 0f, F(0.02f, 0.06f))
                + Vector3.down * F(0.02f, 0.07f);

            int gomos = fio == 0 ? 7 : 5;              // uma alça bem mais longa
            for (int g = 0; g < gomos; g++)
            {
                // gordo e curto, com 25% de sobreposição: em gomo fino e
                // comprido a tripa vira colar de contas
                float comp = F(0.15f, 0.185f);
                float raio = F(0.052f, 0.068f) * (1f - g * 0.05f);

                var seg = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                seg.name = "Tripa_" + fio + "_" + g;
                seg.transform.SetParent(corpo.transform, true);
                seg.transform.position = saida + Vector3.down * (comp * 0.5f);
                seg.transform.rotation = Quaternion.Euler(F(-14f, 14f), 0f, F(-14f, 14f));
                seg.transform.localScale = new Vector3(raio * 2f, comp * 0.5f, raio * 2f) / escala;
                seg.AddComponent<Viscera>();

                var r = seg.GetComponent<Renderer>();
                r.sharedMaterial = mat;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;

                var col = seg.GetComponent<CapsuleCollider>();
                col.direction = 1;

                var rb = seg.AddComponent<Rigidbody>();
                rb.mass = 0.5f;
                rb.linearDamping = 1.6f;              // tripa não balança feito corda de relógio
                rb.angularDamping = 4f;
                rb.maxAngularVelocity = 4f;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                rb.solverIterations = 16;

                if (pai != null)
                {
                    var j = seg.AddComponent<CharacterJoint>();
                    j.connectedBody = pai;
                    j.anchor = new Vector3(0f, 0.82f, 0f);   // acima do topo: os gomos se encavalam
                    j.enablePreprocessing = false;
                    j.enableProjection = true;
                    j.projectionDistance = 0.03f;
                    j.projectionAngle = 10f;
                    j.lowTwistLimit = new SoftJointLimit { limit = -30f };
                    j.highTwistLimit = new SoftJointLimit { limit = 30f };
                    j.swing1Limit = new SoftJointLimit { limit = 62f };
                    j.swing2Limit = new SoftJointLimit { limit = 48f };
                }

                pai = rb;
                saida += Vector3.down * comp * 0.75f;
            }
        }
    }

    static Material MaterialDeTripa()
    {
        const string arq = "Assets/_Game/Terreno/M_Tripas.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(arq);
        if (m == null)
        {
            var sh = Shader.Find("Universal Render Pipeline/Lit");
            m = new Material(sh);
            AssetDatabase.CreateAsset(m, arq);
        }
        // vermelho escuro de sangue parado, e MOLHADO: o brilho é metade do
        // efeito, sem ele vira massinha
        m.SetColor("_BaseColor", new Color(0.21f, 0.035f, 0.032f));
        m.SetFloat("_Smoothness", 0.86f);
        m.SetFloat("_Metallic", 0f);
        EditorUtility.SetDirty(m);
        return m;
    }
}
