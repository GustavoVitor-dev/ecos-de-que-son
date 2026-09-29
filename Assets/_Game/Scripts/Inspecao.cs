using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// Tela de inspeção: o item que você acabou de pegar, girando devagar, com o
/// nome e uma linha na voz do Miller.
///
/// O modelo é uma CÓPIA do objeto do mundo, levada pra baixo do mapa
/// (y = -1000), com uma câmera e duas luzes só dela. Nada de modelo separado
/// pra manter igual ao do chão: o que você vê aqui é o que estava lá.
///
/// Pausa o jogo enquanto está aberta (Time.timeScale = 0). Ler não pode custar
/// a vida — o custo é o tempo de ir buscar, não o de olhar.
public class Inspecao : MonoBehaviour
{
    public static Inspecao Instance { get; private set; }
    public static bool Aberta => Instance != null && Instance.aberta;
    /// O piloto automático não lê: pega e segue.
    public static bool Pular;

    const int Camada = 30;
    static readonly Vector3 Palco = new Vector3(0f, -1000f, 0f);

    Camera cam;
    RenderTexture rt;
    Transform modelo;
    string nome, texto;
    Action aoFechar;
    bool aberta;
    float abertaEm, alfa, giro, inclina = -38f;
    float escalaDeTempoAntes = 1f;
    bool travadoAntes;
    SoldierController jogador;
    GUIStyle stNome, stTexto, stDica;

    /// zoom > 1 aproxima: pra item com parte comprida e fina (a antena do
    /// rádio) que senão encolhe o resto no quadro.
    public static void Mostrar(GameObject fonte, string nome, string texto, Action aoFechar = null, float zoom = 1f)
    {
        if (Pular || fonte == null) { aoFechar?.Invoke(); return; }
        if (Instance == null) new GameObject("Inspecao").AddComponent<Inspecao>();
        Instance.Abrir(fonte, nome, texto, aoFechar, zoom);
    }

    void Awake()
    {
        Instance = this;
        var go = new GameObject("CameraDeInspecao");
        go.transform.SetParent(transform, false);
        cam = go.AddComponent<Camera>();
        cam.enabled = false;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
        cam.cullingMask = 1 << Camada;
        cam.fieldOfView = 26f;
        cam.nearClipPlane = 0.02f; cam.farClipPlane = 20f;
        cam.transform.position = Palco + new Vector3(0f, 0.12f, -1.4f);
        cam.transform.LookAt(Palco);
        var dados = go.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
        dados.renderPostProcessing = false;
        dados.renderShadows = false;

        // luz de chave (lanterna amarelada, de cima e da esquerda) e um recorte frio atrás
        Luz(new Vector3(-0.7f, 0.9f, -0.9f), new Color(1f, 0.86f, 0.64f), 11f);
        Luz(new Vector3(0.8f, 0.5f, 0.7f), new Color(0.5f, 0.62f, 0.8f), 6f);
        Luz(new Vector3(0.6f, -0.3f, -1f), new Color(0.45f, 0.42f, 0.38f), 3f);
    }

    void Luz(Vector3 onde, Color cor, float forca)
    {
        var l = new GameObject("Luz").AddComponent<Light>();
        l.transform.SetParent(transform, false);
        l.transform.position = Palco + onde;
        l.type = LightType.Point;
        l.range = 4f; l.color = cor; l.intensity = forca;
        l.shadows = LightShadows.None;
        l.cullingMask = 1 << Camada;
    }

    void Abrir(GameObject fonte, string n, string t, Action fechar, float zoom)
    {
        if (aberta) Fechar();
        nome = n; texto = t; aoFechar = fechar;

        // cópia só com o que se vê
        var copia = Instantiate(fonte);
        copia.name = "Inspecionando_" + fonte.name;
        foreach (var mb in copia.GetComponentsInChildren<MonoBehaviour>(true)) Destroy(mb);
        foreach (var c in copia.GetComponentsInChildren<Collider>(true)) Destroy(c);
        foreach (var a in copia.GetComponentsInChildren<Animator>(true)) Destroy(a);
        foreach (var l in copia.GetComponentsInChildren<Light>(true)) Destroy(l);
        foreach (var r in copia.GetComponentsInChildren<Renderer>(true))
        {
            r.enabled = true;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        foreach (var tr in copia.GetComponentsInChildren<Transform>(true)) { tr.gameObject.layer = Camada; tr.gameObject.SetActive(true); }

        // centro e tamanho: cabe no quadro, girando em volta do próprio meio
        var pivo = new GameObject("Pivo").transform;
        pivo.SetParent(transform, false);
        pivo.position = Palco;
        copia.transform.position = Palco;
        copia.transform.rotation = Quaternion.identity * Quaternion.Inverse(Quaternion.Euler(0f, fonte.transform.eulerAngles.y, 0f)) * fonte.transform.rotation;
        var b = Limites(copia);
        float maior = Mathf.Max(b.size.x, b.size.y, b.size.z, 0.001f);
        copia.transform.localScale = copia.transform.localScale * (0.42f * zoom / maior);
        b = Limites(copia);
        copia.transform.position += Palco - b.center;
        copia.transform.SetParent(pivo, true);
        modelo = pivo;
        giro = 25f; inclina = -38f;

        if (rt == null) { rt = new RenderTexture(1024, 1024, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 }; rt.Create(); }
        cam.targetTexture = rt;
        cam.enabled = true;

        jogador = FindFirstObjectByType<SoldierController>();
        travadoAntes = jogador != null && jogador.Travado;
        if (jogador != null) jogador.Travado = true;
        escalaDeTempoAntes = Time.timeScale > 0f ? Time.timeScale : 1f;
        Time.timeScale = 0f;
        aberta = true;
        abertaEm = Time.unscaledTime;
        alfa = 0f;
    }

    public void Fechar()
    {
        aberta = false;
        cam.enabled = false;
        if (modelo != null) Destroy(modelo.gameObject);
        Time.timeScale = escalaDeTempoAntes;
        if (jogador != null) jogador.Travado = travadoAntes;
        var f = aoFechar; aoFechar = null;
        f?.Invoke();
    }

    void Update()
    {
        if (!aberta) { alfa = 0f; return; }
        float dt = Time.unscaledDeltaTime;
        alfa = Mathf.MoveTowards(alfa, 1f, dt * 3f);

        var kb = Keyboard.current;
        var ms = Mouse.current;
        float manual = 0f;
        if (kb != null)
        {
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) manual += 1f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) manual -= 1f;
        }
        if (ms != null && ms.leftButton.isPressed)
        {
            var d = ms.delta.ReadValue();
            giro -= d.x * 0.35f;
            inclina = Mathf.Clamp(inclina - d.y * 0.25f, -80f, 60f);
        }
        giro += (manual != 0f ? manual * 90f : 22f) * dt;
        // gira no próprio eixo vertical e depois inclina pra câmera: o painel
        // (a face de cima) fica sempre à vista
        if (modelo != null) modelo.rotation = Quaternion.Euler(inclina, 0f, 0f) * Quaternion.Euler(0f, giro, 0f);

        // meio segundo de carência: o [E] que pegou o item não pode fechar ele
        if (Time.unscaledTime - abertaEm < 0.5f || kb == null) return;
        if (kb.eKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame)
            Fechar();
    }

    void OnGUI()
    {
        if (!aberta || rt == null) return;
        GUI.depth = -800;
        float esc = Screen.height / 1080f;
        var prev = GUI.color;

        GUI.color = new Color(0f, 0f, 0f, 0.86f * alfa);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);

        float lado = 640f * esc;
        var r = new Rect((Screen.width - lado) * 0.5f, Screen.height * 0.08f, lado, lado);
        GUI.color = new Color(1f, 1f, 1f, alfa);
        GUI.DrawTexture(r, rt, ScaleMode.ScaleToFit, true);

        if (stNome == null)
        {
            stNome = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            stTexto = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperCenter, fontStyle = FontStyle.Italic, wordWrap = true };
            stDica = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter };
        }
        stNome.fontSize = Mathf.RoundToInt(30 * esc);
        stTexto.fontSize = Mathf.RoundToInt(21 * esc);
        stDica.fontSize = Mathf.RoundToInt(15 * esc);

        float w = Mathf.Min(Screen.width * 0.7f, 900f * esc);
        float y = r.yMax + 6f * esc;
        GUI.color = new Color(0.92f, 0.86f, 0.7f, alfa);
        GUI.Label(new Rect((Screen.width - w) * 0.5f, y, w, 44f * esc), nome, stNome);
        GUI.color = new Color(0.82f, 0.82f, 0.8f, alfa);
        GUI.Label(new Rect((Screen.width - w) * 0.5f, y + 50f * esc, w, 160f * esc), texto, stTexto);
        GUI.color = new Color(0.7f, 0.68f, 0.6f, 0.7f * alfa);
        GUI.Label(new Rect(0, Screen.height - 48f * esc, Screen.width, 30f * esc), "[E] guardar        [A / D] ou arrastar: girar", stDica);
        GUI.color = prev;
    }

    /// Pela malha, não por Renderer.bounds: objeto recém-instanciado e movido
    /// no mesmo quadro ainda devolve os limites velhos.
    static Bounds Limites(GameObject go)
    {
        bool tem = false; var b = new Bounds();
        foreach (var mf in go.GetComponentsInChildren<MeshFilter>(true))
        {
            if (mf.sharedMesh == null) continue;
            var mb = mf.sharedMesh.bounds; var m = mf.transform.localToWorldMatrix;
            for (int i = 0; i < 8; i++)
            {
                var c = mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                var w = m.MultiplyPoint3x4(c);
                if (!tem) { b = new Bounds(w, Vector3.zero); tem = true; } else b.Encapsulate(w);
            }
        }
        return tem ? b : new Bounds(go.transform.position, Vector3.one * 0.1f);
    }

    void OnDestroy()
    {
        if (aberta) Time.timeScale = escalaDeTempoAntes;
        if (rt != null) rt.Release();
    }
}
