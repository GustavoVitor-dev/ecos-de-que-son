using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// Chuva o tempo todo e, de vez em quando, relâmpago.
///
/// O relâmpago é a única hora em que o jogador vê o mapa inteiro: por uma
/// fração de segundo a escuridão fora do cone levanta (_EcosRelampago no
/// shader da escuridão), a lua vira flash, e o que estiver em volta aparece.
/// Às vezes o que aparece não é ninguém: silhuetas paradas olhando pra ele,
/// fora do cone da lanterna, que somem quando a luz apaga. Sutil de propósito
/// — sem som, sem marcador. Quem notou, notou.
///
/// Também é a nova lacuna de percepção: logo depois do clarão a vista está
/// ofuscada, e é aí que o BlinkSystem avisa que o mundo pode mudar. Antes
/// isso era um corte preto de tela cheia, que piscava a cada salto de estresse.
public class Tempestade : MonoBehaviour
{
    [Header("Chuva")]
    public float gotasPorSegundo = 1400f;
    public Vector2 areaDaChuva = new Vector2(30f, 22f);
    [Range(0f, 1f)] public float volumeDaChuva = 0.32f;
    [Tooltip("Reflexo do céu no chão molhado. Baixo: só a lanterna brilha no molhado.")]
    [Range(0f, 1f)] public float reflexoDoCeu = 0.08f;

    [Header("Relâmpago")]
    [Tooltip("Segundos entre relâmpagos, calmo (min, max).")]
    public Vector2 intervaloCalmo = new Vector2(28f, 55f);
    [Tooltip("Segundos entre relâmpagos em ruptura/hipervigilância (min, max).")]
    public Vector2 intervaloTenso = new Vector2(14f, 26f);
    public float primeiroRelampago = 12f;
    [Tooltip("Intensidade da lua durante o clarão.")]
    public float luaNoClarao = 3.2f;
    [Range(0f, 1f)] public float volumeDoTrovao = 0.8f;

    [Header("Alucinação no clarão")]
    [Tooltip("Modelo das silhuetas (soldado). Fica preto chapado.")]
    public GameObject figura;
    public RuntimeAnimatorController poseDaFigura;
    [Range(0f, 1f)] public float chanceCalmo = 0.2f;
    [Range(0f, 1f)] public float chanceTenso = 0.55f;
    public Vector2 distanciaDaFigura = new Vector2(5f, 9f);

    static readonly int Relampago = Shader.PropertyToID("_EcosRelampago");

    Transform jogador;
    Light lua;
    float luaBase;
    Color corBase;
    Color ambienteBase;
    float reflexoBase = 1f;
    ParticleSystem gotas, respingos;
    AudioSource chuva, trovao;
    AudioClip[] trovoes;
    BlinkSystem lacuna;
    float timer;
    readonly List<GameObject> figuras = new List<GameObject>();
    Material preto;

    void Start()
    {
        var sc = FindFirstObjectByType<SoldierController>();
        if (sc != null) jogador = sc.transform;
        lacuna = FindFirstObjectByType<BlinkSystem>();
        foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
            if (l.type == LightType.Directional && l.enabled) { lua = l; break; }
        if (lua != null) { luaBase = lua.intensity; corBase = lua.color; }
        ambienteBase = RenderSettings.ambientLight;
        Shader.SetGlobalFloat(Relampago, 0f);
        // céu de tempestade não tem o que refletir: sem isto o chão molhado
        // espelhava o skybox e acinzentava até o escuro fora do cone. Assim o
        // molhado só brilha onde a lanterna (ou o relâmpago) bate.
        reflexoBase = RenderSettings.reflectionIntensity;
        RenderSettings.reflectionIntensity = reflexoDoCeu;

        CriarChuva();
        CriarSom();
        preto = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        preto.SetColor("_BaseColor", new Color(0.004f, 0.004f, 0.005f));
        timer = primeiroRelampago;
    }

    void OnDisable()
    {
        Shader.SetGlobalFloat(Relampago, 0f);
        if (lua != null) { lua.intensity = luaBase; lua.color = corBase; }
        RenderSettings.ambientLight = ambienteBase;
        RenderSettings.reflectionIntensity = reflexoBase;
    }

    void LateUpdate()
    {
        // A chuva segue o QUADRO, não o jogador.
        //
        // Ela era uma caixa de 30x22 m grudada no personagem. Na cutscene de
        // abertura, que abre o zoom pra 22 (a tela passa a mostrar 104x44 m),
        // isso virava um retângulo de chuva no meio de uma mata seca — dava
        // pra ver a borda. Agora a caixa acompanha o ponto que a câmera
        // enquadra e cresce junto com ele.
        Vector3 p = CameraRig.Foco;
        float fator = FatorDeArea();
        if (gotas != null)
        {
            // um pouco à frente: a câmera olha levemente pro norte
            gotas.transform.position = p + new Vector3(1.5f, 8f, 1.5f);
            veu.transform.position = p + new Vector3(1.5f, 8f, 1.5f);
            coroas.transform.position = p + Vector3.up * 0.03f;
            aneis.transform.position = p + Vector3.up * 0.03f;
            Esticar(gotas, fator, 1f);
            Esticar(veu, fator, 1f);
            Esticar(coroas, fator, 0.75f);
            Esticar(aneis, fator, 0.75f);
        }
        EscalaDaChuva();
        Vento();

        timer -= Time.deltaTime;
        if (timer <= 0f)
        {
            StartCoroutine(Clarao());
            var s = StressSystem.Instance;
            bool tenso = s != null && s.Current >= StressSystem.Tier.Ruptura;
            var faixa = tenso ? intervaloTenso : intervaloCalmo;
            timer = Random.Range(faixa.x, faixa.y);
        }
    }

    /// Relâmpago agora (roteiro e teste). forcarFigura: garante silhueta.
    public void Relampejar(bool forcarFigura = false)
    {
        StartCoroutine(Clarao(forcarFigura));
    }

    IEnumerator Clarao(bool forcarFigura = false)
    {
        var s = StressSystem.Instance;
        bool tenso = s != null && s.Current >= StressSystem.Tier.Ruptura;
        bool assombrar = figura != null && jogador != null && (forcarFigura || Random.value < (tenso ? chanceTenso : chanceCalmo));
        if (assombrar) PorFiguras(tenso ? Random.Range(1, 4) : 1);

        // dois estalos: um curto, um vão, um mais longo que apaga devagar
        yield return Luz(1f, 0.06f);
        yield return Luz(0.15f, 0.07f);
        float dur = Random.Range(0.18f, 0.32f);
        for (float t = 0f; t < dur; t += Time.deltaTime)
        {
            Aplicar(Mathf.Lerp(0.9f, 0f, t / dur));
            yield return null;
        }
        Aplicar(0f);
        TirarFiguras();

        // vista ofuscada: é aqui que o mundo pode mudar sem ser visto
        if (lacuna != null) lacuna.LacunaSilenciosa();

        // o trovão chega depois, mais longe quanto maior o atraso
        float atraso = Random.Range(0.4f, 2.6f);
        yield return new WaitForSeconds(atraso);
        if (trovao != null && trovoes != null)
            trovao.PlayOneShot(trovoes[Random.Range(0, trovoes.Length)], volumeDoTrovao * Mathf.Lerp(1f, 0.55f, atraso / 2.6f));
    }

    IEnumerator Luz(float k, float dur)
    {
        Aplicar(k);
        yield return new WaitForSeconds(dur);
    }

    void Aplicar(float k)
    {
        Shader.SetGlobalFloat(Relampago, k);
        if (lua != null)
        {
            lua.intensity = Mathf.Lerp(luaBase, luaNoClarao, k);
            lua.color = Color.Lerp(corBase, new Color(0.78f, 0.84f, 1f), k);
        }
        RenderSettings.ambientLight = Color.Lerp(ambienteBase, new Color(0.42f, 0.46f, 0.55f), k);
        foreach (var f in figuras) if (f != null) f.SetActive(k > 0.3f);
    }

    // ---------------------------------------------------------------- figuras

    void PorFiguras(int n)
    {
        var fl = jogador.forward; fl.y = 0f; fl.Normalize();
        for (int i = 0; i < n; i++)
        {
            for (int tent = 0; tent < 20; tent++)
            {
                // fora do cone da lanterna: dos lados e atrás
                float ang = Random.Range(75f, 285f);
                var dir = Quaternion.Euler(0f, ang, 0f) * fl;
                var p = jogador.position + dir * Random.Range(distanciaDaFigura.x, distanciaDaFigura.y);
                if (!Physics.Raycast(p + Vector3.up * 30f, Vector3.down, out var hit, 60f, ~0, QueryTriggerInteraction.Ignore)) continue;
                if (hit.collider.GetComponent<Terrain>() == null && hit.collider.GetComponent<TerrainCollider>() == null) continue;
                var go = Instantiate(figura, hit.point, Quaternion.LookRotation(jogador.position - hit.point, Vector3.up));
                go.transform.rotation = Quaternion.Euler(0f, go.transform.eulerAngles.y, 0f);
                foreach (var c in go.GetComponentsInChildren<Collider>()) Destroy(c);
                foreach (var m in go.GetComponentsInChildren<MonoBehaviour>()) Destroy(m);
                var an = go.GetComponentInChildren<Animator>();
                if (an != null && poseDaFigura != null) an.runtimeAnimatorController = poseDaFigura;
                foreach (var r in go.GetComponentsInChildren<Renderer>())
                {
                    var ms = r.sharedMaterials;
                    for (int k = 0; k < ms.Length; k++) ms[k] = preto;
                    r.sharedMaterials = ms;
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
                go.SetActive(false);
                figuras.Add(go);
                break;
            }
        }
    }

    void TirarFiguras()
    {
        foreach (var f in figuras) if (f != null) Destroy(f);
        figuras.Clear();
    }

    // ---------------------------------------------------------------- chuva

    // Chuva em camadas. A primeira versão era um tracinho cinza igual por cima
    // de tudo, inclusive da escuridão — parecia estática de TV. Agora:
    //  - gotas ILUMINADAS (Simple Lit, abaixo da escuridão): só acendem onde bate
    //    luz — no facho da lanterna brilham, no escuro somem, no relâmpago o céu
    //    inteiro aparece chovendo;
    //  - um véu fraquíssimo por cima da escuridão, pra chuva existir no escuro;
    //  - respingo em coroa (gotículas que pulam) + anel no chão, também iluminados;
    //  - vento em rajadas: inclina, adensa e sobe o som.
    ParticleSystem veu, coroas, aneis;
    float fase;

    void CriarChuva()
    {
        var gota = Textura(8, 64, (u, v) => Mathf.Clamp01(1f - Mathf.Abs(u - 0.5f) * 2.2f) * Mathf.Pow(Mathf.Sin(v * Mathf.PI), 0.7f) * (0.4f + 0.6f * v));
        var ponto = Textura(16, 16, (u, v) => Mathf.Clamp01(1f - new Vector2(u - 0.5f, v - 0.5f).magnitude * 2.2f));
        var anel = Textura(32, 32, (u, v) => { float d = new Vector2(u - 0.5f, v - 0.5f).magnitude * 2f; return Mathf.Clamp01(1f - Mathf.Abs(d - 0.78f) * 9f) * (d < 1f ? 1f : 0f); });

        // gotas iluminadas
        gotas = NovoSistema("Chuva", MaterialDeParticula(gota, new Color(1.6f, 1.7f, 1.9f, 0.9f), true, 3000));
        Gotas(gotas, gotasPorSegundo, new Vector2(0.03f, 0.05f), 1.45f);
        // véu por cima da escuridão
        veu = NovoSistema("ChuvaVeu", MaterialDeParticula(gota, new Color(0.55f, 0.6f, 0.7f, 0.07f), false, 3150));
        Gotas(veu, gotasPorSegundo * 0.35f, new Vector2(0.02f, 0.03f), 1f);

        // coroa: gotículas pulando do chão
        coroas = NovoSistema("Coroas", MaterialDeParticula(ponto, new Color(1.6f, 1.72f, 1.92f, 1f), true, 3000));
        var mc = coroas.main;
        mc.startLifetime = new ParticleSystem.MinMaxCurve(0.16f, 0.26f); mc.startSpeed = 0f;
        mc.startSize = new ParticleSystem.MinMaxCurve(0.018f, 0.035f); mc.gravityModifier = 1.3f;
        mc.maxParticles = 4000; mc.simulationSpace = ParticleSystemSimulationSpace.World;
        var ec = coroas.emission; ec.rateOverTime = gotasPorSegundo * 0.9f;
        var sc = coroas.shape; sc.shapeType = ParticleSystemShapeType.Box; sc.scale = new Vector3(areaDaChuva.x * 0.75f, 0.01f, areaDaChuva.y * 0.75f);
        var vc = coroas.velocityOverLifetime; vc.enabled = true; vc.space = ParticleSystemSimulationSpace.World;
        vc.x = new ParticleSystem.MinMaxCurve(-0.6f, 0.6f); vc.y = new ParticleSystem.MinMaxCurve(0.9f, 1.8f); vc.z = new ParticleSystem.MinMaxCurve(-0.6f, 0.6f);

        // anel que abre no chão
        aneis = NovoSistema("Aneis", MaterialDeParticula(anel, new Color(1.5f, 1.64f, 1.84f, 0.63f), true, 3000));
        var ma = aneis.main;
        ma.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.4f); ma.startSpeed = 0f;
        ma.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.22f);
        ma.maxParticles = 2500; ma.simulationSpace = ParticleSystemSimulationSpace.World;
        var ea = aneis.emission; ea.rateOverTime = gotasPorSegundo * 0.5f;
        var sa = aneis.shape; sa.shapeType = ParticleSystemShapeType.Box; sa.scale = new Vector3(areaDaChuva.x * 0.75f, 0.01f, areaDaChuva.y * 0.75f);
        var ta = aneis.sizeOverLifetime; ta.enabled = true; ta.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.25f, 1f, 1.6f));
        var ca = aneis.colorOverLifetime; ca.enabled = true; ca.color = Some();
        aneis.GetComponent<ParticleSystemRenderer>().renderMode = ParticleSystemRenderMode.HorizontalBillboard;
        respingos = aneis;
    }

    /// Tamanho original de cada sistema, pra poder redimensionar com o quadro.
    struct Medida { public ParticleSystem ps; public Vector2 x, y; public float vel; }
    readonly System.Collections.Generic.List<Medida> medidas = new System.Collections.Generic.List<Medida>();
    float escalaAplicada = 1f;

    /// A chuva mantém o tamanho NA TELA, não no mundo.
    ///
    /// A gota tem 0,7 m de risco: no enquadramento de jogo (6) é um fio; no
    /// close da luta de faca (2,3) vira uma barra branca de néon atravessando
    /// a tela inteira, e a briga desaparece atrás dela. Mesmo problema da
    /// silhueta de copa, no outro sentido.
    void EscalaDaChuva()
    {
        var cam = CameraRig.Lente;
        if (cam == null || !cam.orthographic || CameraRig.TamanhoDeJogo <= 0.1f) return;
        float k = Mathf.Clamp(cam.orthographicSize / CameraRig.TamanhoDeJogo, 0.4f, 1f);
        if (Mathf.Abs(k - escalaAplicada) < 0.04f) return;
        escalaAplicada = k;

        foreach (var md in medidas)
        {
            if (md.ps == null) continue;
            var m = md.ps.main;
            m.startSizeX = new ParticleSystem.MinMaxCurve(md.x.x * k, md.x.y * k);
            m.startSizeY = new ParticleSystem.MinMaxCurve(md.y.x * k, md.y.y * k);
            var r = md.ps.GetComponent<ParticleSystemRenderer>();
            // no modo Stretch quem estica de verdade é a velocidade
            if (r != null && md.vel > 0f) r.velocityScale = md.vel * k;
        }
        if (aneis != null)
        {
            var ma = aneis.main;
            ma.startSize = new ParticleSystem.MinMaxCurve(0.1f * k, 0.22f * k);
        }
        if (coroas != null)
        {
            var mc = coroas.main;
            mc.startSize = new ParticleSystem.MinMaxCurve(0.018f * k, 0.035f * k);
        }
    }

    void Gotas(ParticleSystem ps, float taxa, Vector2 largura, float comprimento)
    {
        medidas.Add(new Medida { ps = ps, x = largura, y = new Vector2(0.5f, 0.9f), vel = 0.055f * comprimento });
        var m = ps.main;
        m.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.6f);
        m.startSpeed = 0f;
        m.startSize3D = true;
        m.startSizeX = new ParticleSystem.MinMaxCurve(largura.x, largura.y);
        m.startSizeY = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
        m.startSizeZ = 1f;
        m.maxParticles = 5000;
        m.simulationSpace = ParticleSystemSimulationSpace.World;
        var v = ps.velocityOverLifetime; v.enabled = true; v.space = ParticleSystemSimulationSpace.World;
        v.x = new ParticleSystem.MinMaxCurve(-2.5f, -2f); v.y = new ParticleSystem.MinMaxCurve(-19f, -14f); v.z = new ParticleSystem.MinMaxCurve(-1.3f, -0.9f);
        var e = ps.emission; e.rateOverTime = taxa;
        var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(areaDaChuva.x, 0.1f, areaDaChuva.y);
        var c = ps.colorOverLifetime; c.enabled = true; c.color = Some(0.15f);
        var r = ps.GetComponent<ParticleSystemRenderer>();
        r.renderMode = ParticleSystemRenderMode.Stretch; r.velocityScale = 0.055f * comprimento; r.lengthScale = 1.2f;
    }

    /// Quanto a caixa de chuva precisa crescer pra cobrir o que está na tela.
    /// 1 no zoom de jogo; sobe quando a cutscene abre o quadro. Com teto,
    /// senão um plano bem aberto pede 8 mil gotas por segundo.
    float FatorDeArea()
    {
        var cam = CameraRig.Lente;
        if (cam == null || !cam.orthographic) return 1f;
        // 10% de folga: a caixa tem que passar da borda da tela, senão vê-se
        // a chuva terminar numa linha reta
        float larguraVisivel = cam.orthographicSize * 2f * cam.aspect * 1.1f;
        return Mathf.Clamp(larguraVisivel / areaDaChuva.x, 1f, 3.9f);
    }

    void Esticar(ParticleSystem ps, float fator, float parte)
    {
        if (ps == null) return;
        var sh = ps.shape;
        var alvo = new Vector3(areaDaChuva.x * parte * fator, sh.scale.y, areaDaChuva.y * parte * fator);
        if ((sh.scale - alvo).sqrMagnitude > 0.0001f) sh.scale = alvo;
    }

    /// Rajada de vento: inclina a chuva, adensa e sobe o som. Chamado todo quadro.
    void Vento()
    {
        fase += Time.deltaTime;
        float r = Mathf.Clamp01(Mathf.PerlinNoise(fase * 0.12f, 3.7f) * 1.4f - 0.2f);
        // a caixa cresceu com o quadro: sem mais gotas, a chuva ralearia na
        // mesma proporção e o plano aberto ficaria com quatro gotas na tela
        float area = Mathf.Min(FatorDeArea(), 2.8f);
        // num plano fechado (a luta de faca) a chuva no tamanho certo ainda
        // TAPA a briga: são muitos riscos por metro quadrado de tela. Menos
        // gotas, e o que está acontecendo aparece.
        area *= Mathf.Lerp(0.38f, 1f, Mathf.InverseLerp(0.45f, 0.85f, escalaAplicada));
        foreach (var ps in new[] { gotas, veu })
        {
            if (ps == null) continue;
            var v = ps.velocityOverLifetime;
            v.x = new ParticleSystem.MinMaxCurve(-1.6f - 4f * r, -1.1f - 3.4f * r);
            var e = ps.emission; e.rateOverTime = (ps == gotas ? gotasPorSegundo : gotasPorSegundo * 0.35f) * (0.7f + 0.7f * r) * area;
        }
        if (coroas != null) { var e = coroas.emission; e.rateOverTime = gotasPorSegundo * 0.9f * (0.7f + 0.7f * r) * area; }
        if (chuva != null) chuva.volume = volumeDaChuva * (0.75f + 0.6f * r);
    }

    static Gradient Some(float entrada = 0f)
    {
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  entrada > 0f
                    ? new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, entrada), new GradientAlphaKey(1f, 0.85f), new GradientAlphaKey(0f, 1f) }
                    : new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        return g;
    }

    static Texture2D Textura(int w, int h, System.Func<float, float, float> alfa)
    {
        var t = new Texture2D(w, h, TextureFormat.RGBA32, false);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                t.SetPixel(x, y, new Color(1f, 1f, 1f, alfa((x + 0.5f) / w, (y + 0.5f) / h)));
        t.wrapMode = TextureWrapMode.Clamp;
        t.Apply();
        return t;
    }

    /// Partícula transparente com textura. iluminada = Simple Lit (só aparece
    /// onde há luz); senão Unlit. Sem as chaves de transparência e o _MainTex
    /// o shader ignorava a textura e cada respingo saía um quadrado branco.
    static Material MaterialDeParticula(Texture2D tex, Color cor, bool iluminada, int fila)
    {
        var m = new Material(Shader.Find(iluminada ? "Universal Render Pipeline/Particles/Simple Lit" : "Universal Render Pipeline/Particles/Unlit"));
        m.SetFloat("_Surface", 1f);
        m.SetFloat("_Blend", 0f);
        m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_ZWrite", 0f);
        m.SetOverrideTag("RenderType", "Transparent");
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        if (iluminada) { m.SetFloat("_Smoothness", 0.9f); m.EnableKeyword("_SPECULAR_COLOR"); m.SetColor("_SpecColor", new Color(0.6f, 0.65f, 0.7f)); }
        m.SetTexture("_BaseMap", tex);
        m.SetTexture("_MainTex", tex);
        m.SetColor("_BaseColor", cor);
        m.renderQueue = fila;
        return m;
    }

    ParticleSystem NovoSistema(string nome, Material mat)
    {
        var go = new GameObject(nome);
        go.transform.SetParent(transform, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        ps.Play();
        return ps;
    }

    // ---------------------------------------------------------------- som

    void CriarSom()
    {
        chuva = gameObject.AddComponent<AudioSource>();
        chuva.clip = SomDeChuva();
        chuva.loop = true; chuva.spatialBlend = 0f; chuva.volume = volumeDaChuva;
        chuva.Play();
        trovao = gameObject.AddComponent<AudioSource>();
        trovao.spatialBlend = 0f;
        trovoes = new[] { SomDeTrovao(11), SomDeTrovao(23), SomDeTrovao(37) };
    }

    const int Taxa = 44100;

    /// Chuva: ruído rosa-ish (dois filtros) mais pingos esparsos em folha.
    static AudioClip SomDeChuva()
    {
        int n = Taxa * 6;
        var d = new float[n];
        var rnd = new System.Random(5);
        float b0 = 0f, b1 = 0f, hp = 0f, ant = 0f;
        for (int i = 0; i < n; i++)
        {
            float w = (float)(rnd.NextDouble() * 2.0 - 1.0);
            b0 = 0.97f * b0 + 0.03f * w;
            b1 = 0.6f * b1 + 0.4f * w;
            float chiado = b1 - b0 * 0.6f;
            hp = 0.92f * (hp + chiado - ant); ant = chiado;
            float v = hp * 0.55f + b0 * 0.9f;
            if (rnd.NextDouble() < 0.0009) v += (float)(rnd.NextDouble() * 0.6 - 0.3);
            d[i] = v;
        }
        // costura do loop: meio segundo cruzado
        int x = Taxa / 2;
        for (int i = 0; i < x; i++) { float k = i / (float)x; d[i] = d[i] * k + d[n - x + i] * (1f - k); }
        var ajustado = new float[n - x];
        System.Array.Copy(d, 0, ajustado, 0, n - x);
        float max = 0f; foreach (var v in ajustado) max = Mathf.Max(max, Mathf.Abs(v));
        for (int i = 0; i < ajustado.Length; i++) ajustado[i] = ajustado[i] / max * 0.8f;
        var c = AudioClip.Create("chuva", ajustado.Length, 1, Taxa, false);
        c.SetData(ajustado, 0);
        return c;
    }

    /// Trovão: estalo seco no começo e ronco grave que rola e morre.
    static AudioClip SomDeTrovao(int semente)
    {
        int n = Taxa * 5;
        var d = new float[n];
        var rnd = new System.Random(semente);
        float lp = 0f, lp2 = 0f;
        float rolo = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Taxa;
            float w = (float)(rnd.NextDouble() * 2.0 - 1.0);
            lp = Mathf.Lerp(lp, w, 0.02f);
            lp2 = Mathf.Lerp(lp2, lp, 0.05f);
            // ronda de estrondos: envelope que sobe e desce em ondas
            rolo = 0.6f + 0.4f * Mathf.Sin(t * (2.2f + semente * 0.05f)) * Mathf.Sin(t * 0.9f + semente);
            float corpo = lp2 * 9f * rolo * Mathf.Exp(-0.75f * t) * Mathf.Clamp01(t * 6f);
            float estalo = w * Mathf.Exp(-t * 22f) * 0.5f;
            d[i] = corpo + estalo;
        }
        float max = 0f; foreach (var v in d) max = Mathf.Max(max, Mathf.Abs(v));
        for (int i = 0; i < n; i++) d[i] = d[i] / max * 0.95f;
        var c = AudioClip.Create("trovao_" + semente, n, 1, Taxa, false);
        c.SetData(d, 0);
        return c;
    }
}
