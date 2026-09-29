using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// Monta na cena da Selva o que a Historia precisa: portão acorrentado de
/// verdade (parede até a chave), a aldeia com cabana e poço, o fuzil do
/// Miller ao lado do sargento, o corpo do Doyle escondido, os gatilhos.
/// E desliga o que era da história antiga: os avisos de tutorial, o rádio no
/// chão, a zona de extração que não pedia nada.
///
/// Idempotente: apaga a raiz "Historia" e refaz. O FlorestaSelva chama isto
/// no fim da construção, antes de assar o NavMesh.
public static class HistoriaSelva
{
    // a corrente no mapa atual
    static readonly Vector3 Portao = new Vector3(1f, 0f, 38f);
    // aldeia no extremo leste do mapa largo (FlorestaSelva.JX = 40)
    static readonly Vector3 Poco = new Vector3(30.8f, 0f, 4.6f);
    static readonly Vector3 Cabana = new Vector3(35f, 0f, 7.5f);
    static readonly Vector3 Mochila = new Vector3(32.3f, 0f, 7.6f);
    static readonly Vector3 TrilhaDoMeio = new Vector3(6f, 0f, 12f);
    static readonly float LadoOeste = -FlorestaSelva.JX, LadoLeste = FlorestaSelva.JX;
    static readonly Vector3 Acordar = new Vector3(0f, 0f, -36f);

    // os inimigos que caíram na noite da emboscada, ao sul do córrego. Três
    // têm peça pro receptor; um já foi revistado. Cada um num canto (oeste,
    // leste, meio) e perto de quem ainda patrulha: é a primeira furtividade.
    static readonly (string id, Vector3 pos, float giro, int peca)[] Inimigos =
    {
        ("corpo_cratera", new Vector3(-9.2f, 0f, -31.4f), 130f, 0),   // fita isolante — perto de onde você acorda
        ("corpo_radista", new Vector3(9.3f, 0f, -23.2f), 250f, 1),    // válvula — o operador deles, sob a sentinela
        ("corpo_vazio", new Vector3(-12.3f, 0f, -16.2f), 30f, -1),    // nada
        ("corpo_trilha", new Vector3(-21.6f, 0f, -16.2f), 300f, 2),   // fio de cobre — na rota oeste, a caminho do vau
    };

    // fora da corrente: lugar pra achar, com lembrança e uma isca de recompensa
    static readonly (string id, string rotulo, Vector3 pos)[] Lembrancas =
    {
        ("lembranca_oeste", "A MOCHILA RASGADA", new Vector3(-31f, 0f, -19.5f)),
        ("lembranca_noroeste", "OS CARTUCHOS", new Vector3(-24.5f, 0f, 31f)),
        ("lembranca_leste", "AS PEGADAS", new Vector3(34.5f, 0f, 15f)),
    };

    const string NP = "Assets/NikolayFedorov/OldVillage/VilagePropsPack/Prefab/";
    const string JP = "Assets/JP Environmental Asset Pack/Prefabs/";

    [MenuItem("Ecos/Selva/Montar história")]
    public static void MontarMenu()
    {
        var nivel = GameObject.Find("Nivel");
        if (nivel == null) { Debug.LogError("[HISTORIA] sem Nivel na cena"); return; }
        Montar(nivel.transform);
        FlorestaSelva.AssarNavegacao();
        EditorSceneManager.MarkSceneDirty(nivel.scene);
        EditorSceneManager.SaveOpenScenes();
    }

    public static void Montar(Transform nivel)
    {
        var velha = GameObject.Find("Historia");
        if (velha != null) Object.DestroyImmediate(velha);
        var raiz = new GameObject("Historia").transform;
        var hist = raiz.gameObject.AddComponent<Historia>();
        raiz.gameObject.AddComponent<RadioDoyle>();

        DesligarHistoriaAntiga(nivel);
        Abertura();

        // ---------------------------------------------------------------- o que caiu com você
        Limpar(nivel, Acordar, 2.2f);
        Item(raiz, ItensSelva.RadioQuebrado(raiz), "radio", "O RÁDIO", Acordar + new Vector3(0.7f, 0f, 0.35f), 22f);
        Item(raiz, ItensSelva.Faca(raiz), "faca", "A FACA", Acordar + new Vector3(-0.72f, 0f, -0.25f), -58f);
        Item(raiz, ItensSelva.CadernoDeCampo(raiz), "caderno", "O CADERNO", Acordar + new Vector3(0.15f, 0f, -0.8f), 14f);

        // a bateria só aparece na tela de inspeção do fim; mora longe e desligada
        var bat = ItensSelva.BateriaPodre(raiz);
        bat.transform.position = new Vector3(0f, -60f, 0f);
        hist.bateriaPodre = bat;

        // ---------------------------------------------------------------- os corpos inimigos
        foreach (var (id, pos, giro, peca) in Inimigos) CorpoInimigo(raiz, nivel, id, pos, giro, peca);

        // ---------------------------------------------------------------- portão
        var cerca = new GameObject("Cerca_Norte").transform; cerca.SetParent(raiz, false);
        var arame = FlorestaSelva.P("Assets/Assets_FenceChained/prefabs/chainlink_single.prefab");
        float meio = 2.3f; // metade do vão do portão
        foreach (var (de, ate) in new[] { (LadoOeste, Portao.x - meio), (Portao.x + meio, LadoLeste) })
        {
            float passo = 2.8f;
            for (float x = de + passo * 0.5f; x < ate + 0.2f; x += passo)
            {
                float cx = Mathf.Min(x, ate - passo * 0.5f);
                var go = Por(cerca, arame, cx, Portao.z, 0f);
                if (go != null) go.transform.rotation = Quaternion.Euler(0f, Random.Range(-2f, 2f), 0f);
            }
            // parede invisível do trecho de cerca (a cerca do pacote não tem colisor)
            var p = new GameObject("Parede").transform; p.SetParent(cerca, false);
            p.position = new Vector3((de + ate) * 0.5f, FlorestaSelva.H((de + ate) * 0.5f, Portao.z) + 1.5f, Portao.z);
            var bc = p.gameObject.AddComponent<BoxCollider>(); bc.size = new Vector3(ate - de, 4f, 0.5f);
            GameObjectUtility.SetStaticEditorFlags(p.gameObject, StaticEditorFlags.NavigationStatic);
        }

        var portao = (GameObject)PrefabUtility.InstantiatePrefab(FlorestaSelva.P(NP + "Wrata01Wood.prefab"), raiz);
        portao.name = "Portao_Norte";
        // o prefab vem com giro de importação: soma, não substitui (substituir
        // deitou o portão de lado e a moldura fechou a passagem)
        portao.transform.rotation = Quaternion.Euler(0f, 90f, 0f) * portao.transform.rotation;
        var bp = FlorestaSelva.Limites(portao);
        portao.transform.position += new Vector3(Portao.x - bp.center.x, FlorestaSelva.H(Portao.x, Portao.z) - bp.min.y, Portao.z - bp.center.z);
        Transform folha = null;
        foreach (Transform c in portao.transform) if (c.name.Contains("Door")) folha = c;
        hist.folhaEsquerda = folha;

        // o bloqueio cobre só a folha que abre; a outra metade é moldura fixa
        var bd = folha != null ? FlorestaSelva.Limites(folha.gameObject) : FlorestaSelva.Limites(portao);
        var bloqueio = new GameObject("Bloqueio_Portao"); bloqueio.transform.SetParent(raiz, false);
        bloqueio.transform.position = new Vector3(bd.center.x, FlorestaSelva.H(Portao.x, Portao.z) + 1.5f, Portao.z);
        var bb = bloqueio.AddComponent<BoxCollider>(); bb.size = new Vector3(Mathf.Max(1.6f, bd.size.x + 0.3f), 4f, 0.6f);
        hist.bloqueioDoPortao = bb;
        Debug.Log("[HISTORIA] portao " + FlorestaSelva.Limites(portao).size.ToString("F2") + " folha " + bd.center.ToString("F2") + " " + bd.size.ToString("F2"));

        // corrente e cadeado: um cilindro escuro atravessado no portão
        var corrente = GameObject.CreatePrimitive(PrimitiveType.Cylinder); corrente.name = "Corrente";
        Object.DestroyImmediate(corrente.GetComponent<Collider>());
        corrente.transform.SetParent(folha != null ? folha : portao.transform, true);
        corrente.transform.position = new Vector3(bd.center.x, FlorestaSelva.H(Portao.x, Portao.z) + 1.25f, Portao.z - 0.15f);
        corrente.transform.rotation = Quaternion.Euler(0f, 0f, 90f);
        corrente.transform.localScale = new Vector3(0.05f, 0.5f, 0.05f);
        corrente.GetComponent<Renderer>().sharedMaterial = Cor("M_Corrente", new Color(0.12f, 0.11f, 0.1f), 0.6f);

        Interagir(raiz, "portao", "O PORTÃO", new Vector3(bd.center.x, 0f, Portao.z - 1.2f), 2.3f);
        Gatilho(raiz, "portao", new Vector3(Portao.x, 0f, Portao.z - 3.5f), new Vector3(12f, 4f, 5f));
        Gatilho(raiz, "saida", new Vector3(Portao.x + 1f, 0f, 43.5f), new Vector3(10f, 4f, 3f));

        // travessias do córrego livres de tronco/pedra solta
        foreach (var x in new[] { 0.5f, 11.9f, -26.4f })
            Limpar(nivel, new Vector3(x, 0f, FlorestaSelva.ZCorrego(x)), x < -20f ? 3.2f : 2.4f);

        // ---------------------------------------------------------------- o sargento e o fuzil
        var corpo = GameObject.Find("Atores/Corpo_Unidade");
        Vector3 ward = corpo != null ? corpo.transform.position : new Vector3(-11.6f, 0f, 19.7f);
        Interagir(raiz, "ward", "O SARGENTO", new Vector3(ward.x, 0f, ward.z), 1.9f);
        var fuzilPos = new Vector3(ward.x + 1.5f, 0f, ward.z - 1.1f);
        var fuzil = new GameObject("Fuzil_Miller").transform; fuzil.SetParent(raiz, false);
        fuzil.position = new Vector3(fuzilPos.x, FlorestaSelva.H(fuzilPos.x, fuzilPos.z) + 0.05f, fuzilPos.z);
        var arma = (GameObject)PrefabUtility.InstantiatePrefab(FlorestaSelva.P("Assets/_Game/Modelos/Q_Armas/AssaultRifle_1.fbx"), fuzil);
        if (arma != null)
        {
            arma.transform.localPosition = new Vector3(0.4f, 0f, 0f);
            arma.transform.localRotation = Quaternion.Euler(90f, 0f, 25f);
            arma.transform.localScale = Vector3.one * 0.3f;
        }
        var ifu = fuzil.gameObject.AddComponent<Interagivel>(); ifu.id = "fuzil"; ifu.rotulo = "O FUZIL"; ifu.raio = 1.6f;

        // ---------------------------------------------------------------- a aldeia
        Limpar(nivel, Poco, 3.2f);
        Limpar(nivel, Cabana, 3.6f);
        Limpar(nivel, Mochila, 1.5f);

        var cabana = (GameObject)PrefabUtility.InstantiatePrefab(FlorestaSelva.P("Assets/Blackant Master Studio/The Shed/The Shed Optimized/Prefab/The Shed.prefab"), raiz);
        cabana.name = "Cabana";
        cabana.transform.localScale = Vector3.one * 0.46f;
        cabana.transform.rotation = Quaternion.Euler(0f, -90f, 0f);
        var bcab = FlorestaSelva.Limites(cabana);
        cabana.transform.position += new Vector3(Cabana.x - bcab.center.x, FlorestaSelva.H(Cabana.x, Cabana.z) - bcab.min.y - 0.05f, Cabana.z - bcab.center.z);
        bcab = FlorestaSelva.Limites(cabana);
        var ccab = new GameObject("Colisor").AddComponent<BoxCollider>(); ccab.transform.SetParent(cabana.transform, true);
        ccab.transform.position = bcab.center; ccab.transform.rotation = Quaternion.identity; ccab.transform.localScale = Vector3.one;
        ccab.size = new Vector3(bcab.size.x / 0.46f * 0.92f, bcab.size.y / 0.46f, bcab.size.z / 0.46f * 0.92f);
        GameObjectUtility.SetStaticEditorFlags(ccab.gameObject, StaticEditorFlags.NavigationStatic);

        var moch = GameObject.CreatePrimitive(PrimitiveType.Cube); moch.name = "Mochila";
        Object.DestroyImmediate(moch.GetComponent<Collider>());
        moch.transform.SetParent(raiz, false);
        moch.transform.position = new Vector3(Mochila.x, FlorestaSelva.H(Mochila.x, Mochila.z) + 0.14f, Mochila.z);
        moch.transform.rotation = Quaternion.Euler(0f, 23f, 8f);
        moch.transform.localScale = new Vector3(0.42f, 0.28f, 0.3f);
        moch.GetComponent<Renderer>().sharedMaterial = Cor("M_Mochila", new Color(0.2f, 0.22f, 0.13f), 0.15f);
        var imo = moch.AddComponent<Interagivel>(); imo.id = "mochila"; imo.rotulo = "A MOCHILA"; imo.raio = 1.6f;

        MontarPoco(raiz);
        Gatilho(raiz, "aldeia", new Vector3(31.5f, 0f, 6f), new Vector3(12f, 4f, 12f));

        // ---------------------------------------------------------------- exploração
        var municao = FlorestaSelva.P("Assets/_Game/Modelos/PolyHaven/ammo_box/ammo_box.fbx");
        var lata = FlorestaSelva.P("Assets/_Game/Modelos/PolyHaven/can_rusted/can_rusted.fbx");
        foreach (var (id, rotulo, pos) in Lembrancas)
        {
            Limpar(nivel, pos, 1.6f);
            var go = new GameObject("Lembranca_" + id); go.transform.SetParent(raiz, false);
            go.transform.position = new Vector3(pos.x, FlorestaSelva.H(pos.x, pos.z), pos.z);
            var it = go.AddComponent<Interagivel>(); it.id = id; it.rotulo = rotulo; it.raio = 1.7f;
            if (id == "lembranca_oeste" && municao != null) { var m = Por(go.transform, municao, pos.x, pos.z, 30f); m.transform.localScale = Vector3.one * 2.2f; }
            if (id == "lembranca_noroeste" && lata != null)
                for (int k = 0; k < 5; k++) { var c = Por(go.transform, lata, pos.x + Random.Range(-0.7f, 0.7f), pos.z + Random.Range(-0.7f, 0.7f), Random.Range(0f, 360f)); c.transform.rotation = Quaternion.Euler(90f, Random.Range(0f, 360f), 0f); c.transform.localScale = Vector3.one * 0.6f; }
            if (id == "lembranca_leste")
            {
                var bota = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Terreno/M_Pegada.mat");
                if (bota != null)
                    for (int k = 0; k < 10; k++)
                    {
                        // dois pares lado a lado até a beira; na volta, um só
                        bool volta = k >= 6;
                        float t = volta ? (k - 6) * 0.7f : k * 0.7f;
                        for (int lado = 0; lado < (volta ? 1 : 2); lado++)
                        {
                            var q = GameObject.CreatePrimitive(PrimitiveType.Quad); q.name = "Pegada";
                            Object.DestroyImmediate(q.GetComponent<Collider>());
                            q.transform.SetParent(go.transform, true);
                            float px = pos.x - 1.2f + lado * 0.5f + (volta ? 0.9f : 0f), pz = pos.z - 3.5f + (volta ? 3f - t : t);
                            q.transform.position = new Vector3(px, FlorestaSelva.H(px, pz) + 0.03f, pz);
                            q.transform.rotation = Quaternion.Euler(90f, volta ? 180f : 0f, 0f);
                            q.transform.localScale = new Vector3(0.14f, 0.3f, 1f);
                            q.GetComponent<Renderer>().sharedMaterial = bota;
                        }
                    }
            }
        }

        // ---------------------------------------------------------------- guardas nos gargalos
        // O Guarda_Posto fazia ronda no meio do mapa, bem no caminho posto→aldeia,
        // herança do corredor estreito. No mapa largo ele vigia a cabeça do
        // tronco-ponte: cada travessia do córrego tem o seu vigia.
        Rota("Atores/Rota_Guarda_Posto", "Atores/Guarda_Posto", new[] { new Vector3(8f, 0f, 3.5f), new Vector3(15.5f, 0f, 3f), new Vector3(16f, 0f, 9f), new Vector3(8.5f, 0f, 9.5f) });

        // ---------------------------------------------------------------- guardas das faixas novas
        Guarda(raiz, "Atores/Guarda_Batedor", "Guarda_Aldeia", new[] { new Vector3(26f, 0f, 2.5f), new Vector3(28f, 0f, 12.5f), new Vector3(37f, 0f, 13f), new Vector3(38f, 0f, 2f) });
        Guarda(raiz, "Atores/Guarda_Batedor", "Guarda_Oeste", new[] { new Vector3(-31f, 0f, 5f), new Vector3(-21f, 0f, 5f), new Vector3(-20f, 0f, 13f), new Vector3(-31f, 0f, 12f) });

        // o Doyle, escondido até o balde subir
        // ao LADO do poço, no chão: em cima do anel de pedra ele ficava flutuando
        var doyle = CorpoDoDoyle(raiz, new Vector3(Poco.x + 1.75f, 0f, Poco.z - 1.15f), 214f);
        if (doyle != null)
        {
            doyle.name = "Doyle";
            var idy = doyle.AddComponent<Interagivel>(); idy.id = "doyle"; idy.rotulo = "O DOYLE"; idy.raio = 1.8f;
            hist.corpoDoDoyle = doyle;
        }

        // ---------------------------------------------------------------- pontos
        var meioT = new GameObject("Ponto_TrilhaDoMeio").transform; meioT.SetParent(raiz, false);
        meioT.position = new Vector3(TrilhaDoMeio.x, FlorestaSelva.H(TrilhaDoMeio.x, TrilhaDoMeio.z), TrilhaDoMeio.z);
        hist.ponto_TrilhaDoMeio = meioT;
        var ck = new GameObject("Checkpoint").transform; ck.SetParent(raiz, false);
        hist.checkpoint = ck;

        // as moitas: cada uma cria a própria zona de camuflagem
        MoitasSelva.Montar(nivel);

        // a execução da faca: par de animações do atacante e da vítima
        ExecucaoFaca.Montar(Object.FindFirstObjectByType<MeleeKill>(), raiz);
        ExecucaoFaca.ArmarGuardas();

        // a cápsula do jogador tinha 1 m de largura e 30 cm de degrau: ele
        // enganchava em pedra pequena e raiz. 84 cm e 45 cm de degrau passam
        // por onde a silhueta passa.
        var jogador = Object.FindFirstObjectByType<SoldierController>();
        var capsula = jogador != null ? jogador.GetComponent<CharacterController>() : null;
        if (capsula != null)
        {
            Undo.RecordObject(capsula, "capsula");
            capsula.radius = 0.42f;
            capsula.stepOffset = 0.45f;
            EditorUtility.SetDirty(capsula);
        }

        // o caderno por último: o mapa marca a ronda de todos os guardas, inclusive os de cima
        CadernoSelva.Montar(raiz.gameObject.AddComponent<Caderno>());

        Debug.Log("[HISTORIA] capítulo montado: portão, sargento, fuzil, aldeia, poço, Doyle");
    }

    static void Item(Transform raiz, GameObject go, string id, string rotulo, Vector3 pos, float giro)
    {
        // em tamanho real some na câmera de cima: 1,6× pra ler no chão
        go.transform.localScale = Vector3.one * 1.6f;
        go.transform.rotation = Quaternion.Euler(0f, giro, 0f);
        go.transform.position = new Vector3(pos.x, FlorestaSelva.H(pos.x, pos.z) + 0.01f, pos.z);
        var it = go.AddComponent<Interagivel>(); it.id = id; it.rotulo = rotulo; it.raio = 1.1f;
    }

    static void CorpoInimigo(Transform raiz, Transform nivel, string id, Vector3 pos, float giro, int peca)
    {
        Limpar(nivel, pos, 1.5f);
        var c = FlorestaSelva.PorCorpo(raiz, pos, giro);
        if (c == null) return;
        c.name = "Inimigo_" + id;
        var it = c.AddComponent<Interagivel>(); it.id = id; it.rotulo = "REVISTAR O CORPO"; it.raio = 1.7f;
        if (peca < 0) return;
        GameObject go; string nome, texto;
        switch (peca)
        {
            case 0: go = ItensSelva.Fita(c.transform); nome = "FITA ISOLANTE"; texto = "Meio rolo, grudento de lama.\nSegura a válvula no lugar."; break;
            case 1: go = ItensSelva.Valvula(c.transform); nome = "UMA VÁLVULA"; texto = "O operador deles carregava uma sobressalente, embrulhada num pano.\nNão é do mesmo modelo. Encaixa."; break;
            default: go = ItensSelva.FioDeCobre(c.transform); nome = "FIO DE COBRE"; texto = "Fio de campo, uns dois metros.\nO que queimou no receptor era mais fino. Serve."; break;
        }
        go.transform.localPosition = Vector3.zero;
        foreach (var r in go.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
        var e = go.AddComponent<Espolio>(); e.nome = nome; e.texto = texto;
    }

    /// O corpo no fundo do poço.
    ///
    /// Era o mesmo boneco de soldado dos outros mortos — e isso matava a cena:
    /// o balde sobe, e o que aparece é mais um placeholder deitado. Agora é o
    /// zumbi sem pele do pacote Tensori, inchado e apodrecido: dois dias no
    /// fundo de um poço com água parada, na chuva do Vietnã.
    ///
    /// O inchaço é feito nos OSSOS (barriga, peito, cabeça, mãos), não na
    /// escala do objeto: assim ele incha onde um afogado incha e continua
    /// com o tamanho de gente.
    static GameObject CorpoDoDoyle(Transform raiz, Vector3 pos, float giro)
    {
        var modelo = FlorestaSelva.P("Assets/Tensori/SkinlessZombie/Art/Mesh/skinless zombie.fbx");
        var ctrl = FlorestaSelva.ControladorDeMorte();
        if (modelo == null || ctrl == null)
        {
            Debug.LogWarning("[HISTORIA] sem o zumbi sem pele; usando o corpo padrão");
            return FlorestaSelva.PorCorpo(raiz, pos, giro);
        }

        var go = (GameObject)PrefabUtility.InstantiatePrefab(modelo, raiz);
        go.name = "Doyle";
        go.transform.position = new Vector3(pos.x, FlorestaSelva.H(pos.x, pos.z), pos.z);
        go.transform.rotation = Quaternion.Euler(0f, giro, 0f);
        FlorestaSelva.SemColisor(go);

        // tamanho de gente: o modelo vem com ~1,97 m de bounding box em T
        var b = FlorestaSelva.Limites(go);
        if (b.size.y > 0.1f) go.transform.localScale *= 1.78f / b.size.y;

        var an = go.GetComponent<Animator>();
        if (an == null) an = go.AddComponent<Animator>();
        an.runtimeAnimatorController = ctrl;
        an.applyRootMotion = false;
        an.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        // Só o ragdoll. Tinha um MortoParado junto, congelando a pose "Caido",
        // e os dois reescreviam os MESMOS ossos no mesmo quadro: o corpo caía
        // se debatendo. Quem manda na pose, do içamento à queda, é a física.
        var cadaver = go.AddComponent<Cadaver>();
        cadaver.Montar();
        // e o que sobrou dele: sem as pernas, com as tripas penduradas no corte
        CorpoRasgado.Estripar(go);

        // putrefação: pele esverdeada e arroxeada, molhada; calça encharcada
        foreach (var r in go.GetComponentsInChildren<Renderer>(true))
        {
            var mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                if (mats[i] == null) continue;
                bool pele = mats[i].name.ToLower().Contains("skin");
                string arq = "Assets/_Game/Terreno/M_Doyle_" + (pele ? "Pele" : "Pano") + ".mat";
                var m = AssetDatabase.LoadAssetAtPath<Material>(arq);
                if (m == null) { m = new Material(mats[i]); AssetDatabase.CreateAsset(m, arq); }
                m.CopyPropertiesFromMaterial(mats[i]);
                // verde-acinzentado de dois dias na água, com o roxo do sangue parado
                m.SetColor("_BaseColor", pele ? new Color(0.42f, 0.45f, 0.36f) : new Color(0.24f, 0.24f, 0.22f));
                m.SetFloat("_Smoothness", pele ? 0.62f : 0.35f);   // molhado
                m.SetFloat("_Metallic", 0f);
                EditorUtility.SetDirty(m);
                mats[i] = m;
            }
            r.sharedMaterials = mats;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        }

        // inchaço nos ossos: barriga e peito estufados, cabeça e mãos gordas
        if (an.isHuman)
        {
            void Inchar(HumanBodyBones osso, float x, float y, float z)
            {
                var t = an.GetBoneTransform(osso);
                if (t == null) return;
                Undo.RecordObject(t, "inchaço");
                t.localScale = new Vector3(t.localScale.x * x, t.localScale.y * y, t.localScale.z * z);
                EditorUtility.SetDirty(t);
            }
            Inchar(HumanBodyBones.Spine, 1.3f, 1.12f, 1.35f);
            Inchar(HumanBodyBones.Chest, 1.18f, 1.05f, 1.2f);
            Inchar(HumanBodyBones.Head, 1.14f, 1.1f, 1.14f);
            Inchar(HumanBodyBones.LeftLowerArm, 1.2f, 1f, 1.2f);
            Inchar(HumanBodyBones.RightLowerArm, 1.22f, 1f, 1.22f);
            Inchar(HumanBodyBones.LeftHand, 1.3f, 1.2f, 1.3f);
            Inchar(HumanBodyBones.RightHand, 1.32f, 1.2f, 1.32f);
            Inchar(HumanBodyBones.LeftUpperLeg, 1.15f, 1f, 1.15f);
            Inchar(HumanBodyBones.RightUpperLeg, 1.15f, 1f, 1.15f);
        }
        return go;
    }

    static void MontarPoco(Transform raiz)
    {
        var poco = new GameObject("Poco").transform; poco.SetParent(raiz, false);
        float y = FlorestaSelva.H(Poco.x, Poco.z);
        poco.position = new Vector3(Poco.x, y, Poco.z);

        // boca: disco preto e um anel de pedra
        var boca = GameObject.CreatePrimitive(PrimitiveType.Cylinder); boca.name = "Boca";
        Object.DestroyImmediate(boca.GetComponent<Collider>());
        boca.transform.SetParent(poco, false);
        boca.transform.localPosition = new Vector3(0f, 0.12f, 0f);
        boca.transform.localScale = new Vector3(1.25f, 0.02f, 1.25f);
        const string arqBoca = "Assets/_Game/Terreno/M_BocaDoPoco.mat";
        var mb = AssetDatabase.LoadAssetAtPath<Material>(arqBoca);
        if (mb == null) { mb = new Material(Shader.Find("Universal Render Pipeline/Unlit")); AssetDatabase.CreateAsset(mb, arqBoca); }
        mb.SetColor("_BaseColor", new Color(0.005f, 0.005f, 0.006f));
        boca.GetComponent<Renderer>().sharedMaterial = mb;
        var pedras = new[] { FlorestaSelva.P(JP + "Small Rock 1.prefab"), FlorestaSelva.P(JP + "Small Rock 2.prefab"), FlorestaSelva.P(JP + "Small Rock 3.prefab") };
        for (int k = 0; k < 12; k++)
        {
            float a = k * Mathf.PI * 2f / 12f;
            var p = new Vector3(Poco.x + Mathf.Cos(a) * 0.78f, 0f, Poco.z + Mathf.Sin(a) * 0.78f);
            var go = Por(poco, pedras[k % 3], p.x, p.z, a * Mathf.Rad2Deg);
            if (go != null) { go.transform.localScale = Vector3.one * 0.75f; FlorestaSelva.SemColisor(go); }
        }
        // estrutura: dois postes e o eixo da manivela
        var tora = FlorestaSelva.P(NP + "Logs/log01.prefab");
        foreach (float lado in new[] { -1f, 1f })
        {
            var poste = (GameObject)PrefabUtility.InstantiatePrefab(tora, poco);
            poste.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            poste.transform.localScale = new Vector3(0.35f, 0.35f, 0.4f);
            poste.transform.localPosition = new Vector3(lado * 0.95f, 0f, 0f);
            FlorestaSelva.SemColisor(poste);
        }
        var eixo = (GameObject)PrefabUtility.InstantiatePrefab(tora, poco);
        eixo.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
        eixo.transform.localScale = new Vector3(0.18f, 0.18f, 0.44f);
        eixo.transform.localPosition = new Vector3(-1.05f, 1.8f, 0f);
        FlorestaSelva.SemColisor(eixo);
        var corda = GameObject.CreatePrimitive(PrimitiveType.Cylinder); corda.name = "Corda";
        Object.DestroyImmediate(corda.GetComponent<Collider>());
        corda.transform.SetParent(poco, false);
        corda.transform.localPosition = new Vector3(0f, 1.35f, 0f);
        corda.transform.localScale = new Vector3(0.025f, 0.45f, 0.025f);
        corda.GetComponent<Renderer>().sharedMaterial = Cor("M_Corda", new Color(0.35f, 0.3f, 0.22f), 0.1f);
        var balde = (GameObject)PrefabUtility.InstantiatePrefab(FlorestaSelva.P(NP + "bucket01.prefab"), poco);
        balde.transform.localPosition = new Vector3(0f, 0.72f, 0f);
        balde.transform.localScale = Vector3.one * 1.3f;
        FlorestaSelva.SemColisor(balde);

        var col = poco.gameObject.AddComponent<CapsuleCollider>(); col.radius = 0.95f; col.height = 2.4f; col.center = new Vector3(0f, 1.2f, 0f);
        GameObjectUtility.SetStaticEditorFlags(poco.gameObject, StaticEditorFlags.NavigationStatic);
        var ip = poco.gameObject.AddComponent<Interagivel>(); ip.id = "poco"; ip.rotulo = "O POÇO"; ip.raio = 1.9f; ip.segurar = true; ip.tempoSegurando = 7f;
    }

    /// Muda a ronda de um guarda que já existe na cena (os pontos são filhos
    /// da rota; o guarda vai pro primeiro).
    static void Rota(string caminhoRota, string caminhoGuarda, Vector3[] pontos)
    {
        var r = GameObject.Find(caminhoRota); var g = GameObject.Find(caminhoGuarda);
        if (r == null || g == null) { Debug.LogWarning("[HISTORIA] sem " + caminhoRota); return; }
        for (int i = 0; i < pontos.Length && i < r.transform.childCount; i++)
        {
            var w = r.transform.GetChild(i);
            Undo.RecordObject(w, "rota");
            w.position = new Vector3(pontos[i].x, FlorestaSelva.H(pontos[i].x, pontos[i].z), pontos[i].z);
            EditorUtility.SetDirty(w);
        }
        Undo.RecordObject(g.transform, "rota");
        g.transform.position = r.transform.GetChild(0).position + Vector3.up;
        EditorUtility.SetDirty(g.transform);
    }

    /// Guarda novo copiado de um que já existe (mesma IA, mesma luz, mesmo
    /// corpo), com rota própria. Mora debaixo da raiz Historia: refeito a cada
    /// montagem.
    static void Guarda(Transform raiz, string modelo, string nome, Vector3[] rota)
    {
        var fonte = GameObject.Find(modelo);
        if (fonte == null) { Debug.LogWarning("[HISTORIA] sem " + modelo + " pra copiar"); return; }
        var go = Object.Instantiate(fonte, raiz);
        go.name = nome;
        var wps = new Transform[rota.Length];
        var r = new GameObject("Rota_" + nome).transform; r.SetParent(raiz, false);
        for (int i = 0; i < rota.Length; i++)
        {
            var w = new GameObject("WP_" + i).transform; w.SetParent(r, false);
            w.position = new Vector3(rota[i].x, FlorestaSelva.H(rota[i].x, rota[i].z), rota[i].z);
            wps[i] = w;
        }
        go.transform.position = wps[0].position + Vector3.up;
        var g = go.GetComponent<GuardAI>();
        g.waypoints = wps; g.sentinela = false;
        EditorUtility.SetDirty(g);
    }

    // ---------------------------------------------------------------- limpeza

    static void DesligarHistoriaAntiga(Transform nivel)
    {
        foreach (var caminho in new[] { "Beats", "Portao", "Atores/Radio", "Atores/Zona_Radio" })
        {
            var go = GameObject.Find(caminho);
            if (go != null && go.activeSelf) { Undo.RecordObject(go, "historia"); go.SetActive(false); EditorUtility.SetDirty(go); }
        }
        // as paredes de mata do corredor antigo (Fecho_*) cortavam as trilhas
        // novas: o mapa largo tem a MataNova no lugar delas
        var fechada = nivel.Find("MataFechada");
        if (fechada != null && fechada.gameObject.activeSelf) { Undo.RecordObject(fechada.gameObject, "historia"); fechada.gameObject.SetActive(false); EditorUtility.SetDirty(fechada.gameObject); }
        var ext = nivel.Find("Zona_Extracao");
        if (ext != null && ext.gameObject.activeSelf) { Undo.RecordObject(ext.gameObject, "historia"); ext.gameObject.SetActive(false); EditorUtility.SetDirty(ext.gameObject); }
    }

    /// Tira árvore, arbusto e miudeza de cima de um lugar da história.
    static void Limpar(Transform nivel, Vector3 c, float raio)
    {
        var arv = nivel.Find("Arvores");
        if (arv != null)
            foreach (Transform t in arv)
            {
                var d = t.position - c; d.y = 0f;
                if (d.magnitude < raio && t.gameObject.activeSelf) { Undo.RecordObject(t.gameObject, "historia"); t.gameObject.SetActive(false); EditorUtility.SetDirty(t.gameObject); }
            }
        var fl = nivel.Find("Floresta");
        if (fl != null)
            foreach (Transform g in fl)
            {
                if (g.GetComponent<Terrain>() != null || g.name == "CopaParalaxe" || g.name == "Agua" || g.name == "Corrego_Travessias") continue;
                for (int k = g.childCount - 1; k >= 0; k--)
                {
                    var d = g.GetChild(k).position - c; d.y = 0f;
                    if (d.magnitude < raio) Object.DestroyImmediate(g.GetChild(k).gameObject);
                }
            }
        foreach (var n in new[] { "Folhagem", "Rasteira", "MataFechada" })
        {
            var g = nivel.Find(n); if (g == null) continue;
            foreach (Transform t in g)
            {
                var d = t.position - c; d.y = 0f;
                if (d.magnitude < raio && t.gameObject.activeSelf) { Undo.RecordObject(t.gameObject, "historia"); t.gameObject.SetActive(false); EditorUtility.SetDirty(t.gameObject); }
            }
        }
    }

    // ---------------------------------------------------------------- abertura

    static void Abertura()
    {
        var ab = GameObject.Find("Abertura");
        var cs = ab != null ? ab.GetComponent<Cutscene>() : null;
        if (cs == null || cs.planos == null || cs.planos.Length < 4) return;
        Undo.RecordObject(cs, "abertura");

        // 1: a mata de longe, o título. 2: desce até ele e as três coisas na
        // lama. 3: ele tenta o rádio — só chiado (e, enterrado nele, "ajuda").
        // 4: a primeira frase dele.
        var chiado = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Game/Audio/Radio_Ajuda.wav");
        var alvo = new Vector3(0.1f, FlorestaSelva.H(Acordar.x, Acordar.z) + 0.3f, Acordar.z - 0.1f);
        var p = cs.planos;
        p[0].legenda = "QUE SON, 1968.  TERCEIRO DIA DE CHUVA.";
        p[0].som = null; p[0].pensamento = false;

        Enquadrar(p[1], alvo, 4.6f);
        p[1].nota = "desce ate o soldado e as tres coisas na lama";
        p[1].legenda = ""; p[1].duracao = 2.6f; p[1].tempoDeMovimento = 3.2f; p[1].som = null; p[1].pensamento = false;

        Enquadrar(p[2], alvo, 3f);
        p[2].nota = "ele tenta o radio: chiado descontrolado, 'ajuda' enterrado";
        p[2].legenda = "[ chiado ]"; p[2].som = chiado; p[2].volumeDoSom = 0.9f;
        p[2].duracao = chiado != null ? chiado.length : 6.5f; p[2].tempoDeMovimento = 1.2f;
        p[2].silencio = p[2].duracao + 1f; p[2].estresse = 10f; p[2].pensamento = false;

        Enquadrar(p[3], alvo, 4.2f);
        p[3].nota = "a primeira frase dele";
        p[3].legenda = "Merda de rádio! O receptor está estraçalhado.\nPreciso consertar isso agora, ou o sinal vai sumir para sempre.";
        p[3].duracao = 5.5f; p[3].tempoDeMovimento = 1.4f; p[3].som = null; p[3].pensamento = true; p[3].estresse = 0f; p[3].silencio = 0f;
        EditorUtility.SetDirty(cs);
    }

    /// Põe a câmera do plano olhando pro alvo, no mesmo ângulo que ela já tem,
    /// com o tamanho ortográfico pedido.
    static void Enquadrar(PlanoDeCutscene p, Vector3 alvo, float tamanho)
    {
        p.enquadramento = tamanho;
        if (p.camera == null) return;
        Undo.RecordObject(p.camera, "abertura");
        p.camera.position = alvo - p.camera.forward * 22f;
        EditorUtility.SetDirty(p.camera);
    }

    // ---------------------------------------------------------------- utilidades

    static GameObject Por(Transform pai, GameObject pref, float x, float z, float giro)
    {
        if (pref == null) return null;
        var go = (GameObject)PrefabUtility.InstantiatePrefab(pref, pai);
        go.transform.position = new Vector3(x, FlorestaSelva.H(x, z), z);
        go.transform.rotation = Quaternion.Euler(0f, giro, 0f);
        return go;
    }

    static void Interagir(Transform raiz, string id, string rotulo, Vector3 pos, float raio)
    {
        var go = new GameObject("Interacao_" + id); go.transform.SetParent(raiz, false);
        go.transform.position = new Vector3(pos.x, FlorestaSelva.H(pos.x, pos.z) + 0.5f, pos.z);
        var i = go.AddComponent<Interagivel>(); i.id = id; i.rotulo = rotulo; i.raio = raio;
    }

    static void Gatilho(Transform raiz, string id, Vector3 pos, Vector3 tam)
    {
        var go = new GameObject("Gatilho_" + id); go.transform.SetParent(raiz, false);
        go.transform.position = new Vector3(pos.x, FlorestaSelva.H(pos.x, pos.z) + tam.y * 0.5f, pos.z);
        var bc = go.AddComponent<BoxCollider>(); bc.isTrigger = true; bc.size = tam;
        go.AddComponent<GatilhoHistoria>().id = id;
    }

    static Material Cor(string nome, Color c, float liso)
    {
        string arq = "Assets/_Game/Terreno/" + nome + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(arq);
        if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, arq); }
        m.SetColor("_BaseColor", c); m.SetFloat("_Smoothness", liso);
        EditorUtility.SetDirty(m);
        return m;
    }
}
