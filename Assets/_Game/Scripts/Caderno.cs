using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// O caderno do Miller. Ele é o operador de rádio: anotou o que os batedores
/// passaram antes de sumir e o plano da tropa nova. [N] abre.
///
/// Duas páginas duplas:
///   1. o mapa desenhado à mão (textura feita no editor a partir do mapa de
///      verdade) + o que os batedores disseram;
///   2. o plano da operação + a página arrancada, com sangue.
///
/// A bússola vem presa na capa: gira com o Miller. A letra do topo é pra onde
/// ele está virado; a agulha vermelha aponta o norte. Depois de pegar o
/// caderno, uma bússola pequena fica no canto da tela.
public class Caderno : MonoBehaviour
{
    [System.Serializable]
    public struct Rotulo
    {
        public string texto;
        [Tooltip("0..1 na textura do mapa (y pra cima).")]
        public Vector2 pos;
        public float giro;
        public float tamanho;
        [Tooltip("0 grafite, 1 lápis vermelho, 2 lápis azul")]
        public int cor;
    }

    public static Caderno Instance { get; private set; }
    public static bool Aberto => Instance != null && Instance.aberto;

    [Header("Feito no editor (Ecos/Selva/Montar história)")]
    public Texture2D papel;
    public Texture2D mapa;
    public Texture2D rasgada;
    public Font letra;
    public List<Rotulo> rotulos = new List<Rotulo>();

    [TextArea(6, 20)] public string batedores;
    [TextArea(6, 20)] public string operacao;
    [TextArea(2, 6)] public string restoDaPagina;

    [Header("Bússola")]
    [Tooltip("Quanto o ímã puxa pro norte (graus/s² no erro de 90°).")]
    public float forcaDoIma = 520f;
    [Tooltip("Amortecimento do líquido. Baixo = ela balança muito antes de assentar.")]
    public float atritoDoLiquido = 3.1f;

    /// Pegou o caderno do chão.
    [System.NonSerialized] public bool Tem;

    bool aberto;
    int dupla;
    float alfa;
    float escalaAntes = 1f;
    bool travadoAntes;
    SoldierController jogador;
    Texture2D mostrador, agulha;
    float carta, cartaVel;        // ângulo e velocidade da rosa (graus, graus/s)
    Vector3 ondeEstava;
    GUIStyle stTexto, stRotulo, stDica;

    void Awake() { Instance = this; Tem = false; }

    void Start()
    {
        var sc = FindFirstObjectByType<SoldierController>();
        if (sc != null) jogador = sc;
        mostrador = Mostrador(256);
        agulha = Agulha(32, 256);
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (!Tem) return;
        GirarCarta(Time.unscaledDeltaTime);
        if (kb == null) return;
        alfa = Mathf.MoveTowards(alfa, aberto ? 1f : 0f, Time.unscaledDeltaTime * 5f);

        if (!aberto)
        {
            if (kb.nKey.wasPressedThisFrame && !Inspecao.Aberta && !CutsceneTocando()) Abrir();
            return;
        }
        if (kb.nKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame || kb.eKey.wasPressedThisFrame) { Fechar(); return; }
        if (kb.dKey.wasPressedThisFrame || kb.rightArrowKey.wasPressedThisFrame) dupla = Mathf.Min(1, dupla + 1);
        if (kb.aKey.wasPressedThisFrame || kb.leftArrowKey.wasPressedThisFrame) dupla = Mathf.Max(0, dupla - 1);
    }

    static bool CutsceneTocando()
    {
        var cs = FindFirstObjectByType<Cutscene>();
        return cs != null && cs.Tocando;
    }

    public void Abrir()
    {
        if (aberto) return;
        aberto = true;
        if (jogador != null) { travadoAntes = jogador.Travado; jogador.Travado = true; }
        escalaAntes = Time.timeScale > 0f ? Time.timeScale : 1f;
        Time.timeScale = 0f;
    }

    /// Pros testes: 0 = mapa e batedores, 1 = operação e página arrancada.
    public void IrPara(int d) => dupla = Mathf.Clamp(d, 0, 1);

    public void Fechar()
    {
        if (!aberto) return;
        aberto = false;
        Time.timeScale = escalaAntes;
        if (jogador != null) jogador.Travado = travadoAntes;
    }

    /// Rumo do Miller em graus (0 = norte, 90 = leste).
    float Rumo => jogador != null ? jogador.transform.eulerAngles.y : 0f;

    /// A rosa de uma bússola de verdade não gruda no rumo: ela é um ímã
    /// flutuando em líquido. O torque é proporcional ao SENO do erro (dipolo
    /// magnético), então ela demora a decidir quando está quase 180° trocada,
    /// passa do ponto, volta e só então assenta. O líquido amortece, e o passo
    /// do Miller sacode a agulha.
    void GirarCarta(float dt)
    {
        if (dt <= 0f) return;
        dt = Mathf.Min(dt, 0.05f);                     // integração estável em queda de quadro
        float erro = Mathf.DeltaAngle(carta, -Rumo);
        float torque = forcaDoIma * Mathf.Sin(erro * Mathf.Deg2Rad);
        // ímã fraco perto do alinhamento: sem isto ela nunca assenta de vez
        torque += forcaDoIma * 0.25f * erro / 180f;
        cartaVel += (torque - atritoDoLiquido * cartaVel) * dt;

        // tranco do passo: andar e correr chacoalham o mostrador
        if (jogador != null)
        {
            float andou = (jogador.transform.position - ondeEstava).magnitude / dt;
            ondeEstava = jogador.transform.position;
            if (andou > 0.3f) cartaVel += (Mathf.PerlinNoise(Time.unscaledTime * 9f, 0.3f) - 0.5f) * andou * 26f * dt;
        }

        carta += cartaVel * dt;
        carta = Mathf.Repeat(carta + 180f, 360f) - 180f;
        // pino da agulha tem atrito: quase parada e quase alinhada, para
        if (Mathf.Abs(cartaVel) < 1.2f && Mathf.Abs(Mathf.DeltaAngle(carta, -Rumo)) < 0.7f) cartaVel *= 0.6f;
    }

    // ---------------------------------------------------------------- tela

    void OnGUI()
    {
        if (!Tem) return;
        float esc = Screen.height / 1080f;
        Estilos(esc);

        // bússola pequena no canto, sempre que o caderno está com você
        if (!aberto && !Inspecao.Aberta && !CutsceneTocando())
        {
            float t = 96f * esc;
            Bussola(new Rect(26f * esc, Screen.height - t - 26f * esc, t, t), 0.8f, esc * 0.55f);
            var p = GUI.color;
            GUI.color = new Color(0.8f, 0.76f, 0.64f, 0.55f);
            GUI.Label(new Rect(26f * esc + t + 8f * esc, Screen.height - 54f * esc, 200f * esc, 26f * esc), "[N] caderno", stDica);
            GUI.color = p;
        }

        if (alfa <= 0.01f) return;
        GUI.depth = -700;
        var prev = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.82f * alfa);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);

        // caderno aberto: duas páginas lado a lado
        float h = Screen.height * 0.84f, w = h * 1.42f;
        if (w > Screen.width * 0.94f) { w = Screen.width * 0.94f; h = w / 1.42f; }
        var livro = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.46f, w, h);
        var esq = new Rect(livro.x, livro.y, w * 0.5f, h);
        var dir = new Rect(livro.x + w * 0.5f, livro.y, w * 0.5f, h);

        // capa por baixo (couro escuro aparecendo na borda)
        GUI.color = new Color(0.16f, 0.13f, 0.09f, alfa);
        GUI.DrawTexture(new Rect(livro.x - 12f * esc, livro.y - 12f * esc, w + 24f * esc, h + 24f * esc), Texture2D.whiteTexture);

        GUI.color = new Color(1f, 1f, 1f, alfa);
        if (dupla == 0)
        {
            Papel(esq); Papel(dir);
            Mapa(esq, esc);
            Texto(dir, batedores, esc, 0);
        }
        else
        {
            Papel(esq);
            Texto(esq, operacao, esc, 1);
            // página arrancada: só o toco perto da costura, com sangue
            GUI.color = new Color(1f, 1f, 1f, alfa);
            if (rasgada != null) GUI.DrawTexture(dir, rasgada, ScaleMode.StretchToFill, true);
            else Papel(new Rect(dir.x, dir.y, dir.width * 0.16f, dir.height));
            if (!string.IsNullOrEmpty(restoDaPagina))
            {
                GUI.color = new Color(0.14f, 0.12f, 0.12f, 0.85f * alfa);
                var r = new Rect(dir.x + dir.width * 0.012f, dir.y + dir.height * 0.2f, dir.width * 0.13f, dir.height * 0.6f);
                GUI.Label(r, restoDaPagina, stTexto);
            }
        }

        // sombra da costura
        GUI.color = new Color(0f, 0f, 0f, 0.35f * alfa);
        GUI.DrawTexture(new Rect(livro.x + w * 0.5f - 6f * esc, livro.y, 12f * esc, h), Texture2D.whiteTexture);

        // a bússola presa na capa, embaixo à direita, meio pra fora do caderno
        float tb = 150f * esc;
        GUI.color = new Color(1f, 1f, 1f, alfa);
        Bussola(new Rect(livro.xMax - tb * 0.62f, livro.yMax - tb * 0.62f, tb, tb), alfa, esc);

        GUI.color = new Color(0.8f, 0.76f, 0.64f, 0.75f * alfa);
        string dica = (dupla == 0 ? "[D] próxima página" : "[A] página anterior") + "        [N] fechar";
        GUI.Label(new Rect(0, Screen.height - 40f * esc, Screen.width, 30f * esc), dica, new GUIStyle(stDica) { alignment = TextAnchor.MiddleCenter });
        GUI.color = prev;
    }

    void Estilos(float esc)
    {
        if (stTexto == null)
        {
            stTexto = new GUIStyle(GUI.skin.label) { wordWrap = true, alignment = TextAnchor.UpperLeft, richText = true };
            stRotulo = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, wordWrap = false, clipping = TextClipping.Overflow };
            stDica = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleLeft };
            if (letra != null) { stTexto.font = letra; stRotulo.font = letra; }
        }
        stTexto.fontSize = Mathf.RoundToInt(24 * esc);
        stDica.fontSize = Mathf.RoundToInt(15 * esc);
    }

    void Papel(Rect r)
    {
        var p = GUI.color;
        if (papel != null) GUI.DrawTexture(r, papel, ScaleMode.StretchToFill, false);
        else { GUI.color = new Color(0.78f, 0.72f, 0.58f, alfa); GUI.DrawTexture(r, Texture2D.whiteTexture); }
        GUI.color = p;
    }

    void Mapa(Rect pagina, float esc)
    {
        if (mapa == null) return;
        float m = pagina.width * 0.05f;
        float lw = pagina.width - m * 2f, lh = lw * mapa.height / (float)mapa.width;
        if (lh > pagina.height - m * 2f) { lh = pagina.height - m * 2f; lw = lh * mapa.width / (float)mapa.height; }
        var r = new Rect(pagina.x + (pagina.width - lw) * 0.5f, pagina.y + (pagina.height - lh) * 0.5f, lw, lh);
        var prev = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, alfa);
        GUI.DrawTexture(r, mapa, ScaleMode.StretchToFill, true);

        var matriz = GUI.matrix;
        foreach (var ro in rotulos)
        {
            var c = ro.cor == 1 ? new Color(0.55f, 0.1f, 0.07f) : ro.cor == 2 ? new Color(0.14f, 0.22f, 0.45f) : new Color(0.13f, 0.12f, 0.11f);
            GUI.color = new Color(c.r, c.g, c.b, 0.92f * alfa);
            stRotulo.fontSize = Mathf.RoundToInt((ro.tamanho > 0f ? ro.tamanho : 17f) * 1.9f * lh / 900f);
            var centro = new Vector2(r.x + ro.pos.x * r.width, r.y + (1f - ro.pos.y) * r.height);
            GUIUtility.RotateAroundPivot(ro.giro, centro);
            GUI.Label(new Rect(centro.x - 200f, centro.y - 20f, 400f, 40f), ro.texto, stRotulo);
            GUI.matrix = matriz;
        }
        GUI.color = prev;
    }

    void Texto(Rect pagina, string t, float esc, int semente)
    {
        if (string.IsNullOrEmpty(t)) return;
        var prev = GUI.color;
        var matriz = GUI.matrix;
        GUI.color = new Color(0.12f, 0.11f, 0.13f, 0.9f * alfa);
        var r = new Rect(pagina.x + pagina.width * 0.1f, pagina.y + pagina.height * 0.07f, pagina.width * 0.82f, pagina.height * 0.88f);
        // letra de mão não fica reta na linha
        GUIUtility.RotateAroundPivot(semente == 0 ? -0.8f : 0.6f, r.center);
        GUI.Label(r, t, stTexto);
        GUI.matrix = matriz;
        GUI.color = prev;
    }

    // ---------------------------------------------------------------- bússola

    void Bussola(Rect r, float a, float esc)
    {
        if (mostrador == null) return;
        var prev = GUI.color;
        var matriz = GUI.matrix;
        // a rosa inteira (letras e agulha) é uma peça só, que flutua: a linha
        // de fé fixa no topo é que marca pra onde o Miller está virado
        float rumo = -carta;
        GUIUtility.RotateAroundPivot(-rumo, r.center);
        GUI.color = new Color(1f, 1f, 1f, a);
        GUI.DrawTexture(r, mostrador);
        GUI.matrix = matriz;
        // cada letra com a posição já girada, e uma rotação só em volta dela:
        // RotateAroundPivot aninhado usa o pivô na tela, não no quadro girado
        var st = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, fontSize = Mathf.Max(8, Mathf.RoundToInt(r.width * 0.15f)) };
        string[] letras = { "N", "L", "S", "O" };
        for (int i = 0; i < 4; i++)
        {
            float graus = i * 90f - rumo, ang = graus * Mathf.Deg2Rad;
            var p = r.center + new Vector2(Mathf.Sin(ang), -Mathf.Cos(ang)) * r.width * 0.29f;
            GUIUtility.RotateAroundPivot(graus, p);
            GUI.color = i == 0 ? new Color(0.62f, 0.1f, 0.07f, a) : new Color(0.12f, 0.1f, 0.08f, a);
            GUI.Label(new Rect(p.x - r.width * 0.15f, p.y - r.width * 0.15f, r.width * 0.3f, r.width * 0.3f), letras[i], st);
            GUI.matrix = matriz;
        }

        // agulha: aponta o norte, que na tela gira junto com o mostrador
        GUIUtility.RotateAroundPivot(-rumo, r.center);
        GUI.color = new Color(1f, 1f, 1f, a);
        float aw = r.width * 0.09f, ah = r.width * 0.62f;
        GUI.DrawTexture(new Rect(r.center.x - aw * 0.5f, r.center.y - ah * 0.5f, aw, ah), agulha);
        GUI.matrix = matriz;

        // marca fixa no topo (a linha de fé)
        GUI.color = new Color(0.9f, 0.85f, 0.7f, a);
        GUI.DrawTexture(new Rect(r.center.x - 1.5f, r.y - r.width * 0.05f, 3f, r.width * 0.1f), Texture2D.whiteTexture);
        GUI.color = prev;
    }

    static Texture2D Mostrador(int n)
    {
        var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var px = new Color[n * n];
        float c = (n - 1) * 0.5f;
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = x - c, dy = y - c, d = Mathf.Sqrt(dx * dx + dy * dy) / c;
                Color col = Color.clear;
                float ruido = Mathf.PerlinNoise(x * 0.07f, y * 0.07f);
                if (d < 1f)
                {
                    if (d > 0.86f) col = Color.Lerp(new Color(0.42f, 0.32f, 0.14f), new Color(0.66f, 0.52f, 0.26f), ruido) * (d > 0.93f ? 0.7f : 1f);
                    else
                    {
                        col = Color.Lerp(new Color(0.74f, 0.69f, 0.55f), new Color(0.6f, 0.55f, 0.42f), ruido);
                        // riscos a cada 10 graus, maiores a cada 45
                        float ang = Mathf.Atan2(dx, dy) * Mathf.Rad2Deg;
                        float resto = Mathf.Abs(Mathf.DeltaAngle(ang, Mathf.Round(ang / 10f) * 10f));
                        bool grande = Mathf.Abs(Mathf.DeltaAngle(ang, Mathf.Round(ang / 45f) * 45f)) < 1.2f;
                        if (d > (grande ? 0.68f : 0.76f) && d < 0.85f && resto < 0.9f) col = new Color(0.12f, 0.1f, 0.08f);
                        // vidro riscado e sujo
                        col *= 0.92f + 0.08f * Mathf.PerlinNoise(x * 0.3f, y * 0.02f);
                    }
                    col.a = Mathf.Clamp01((1f - d) * c * 0.5f);
                }
                px[y * n + x] = col;
            }
        t.SetPixels(px); t.Apply();
        return t;
    }

    static Texture2D Agulha(int w, int h)
    {
        var t = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var px = new Color[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float v = y / (float)(h - 1);          // 0 embaixo, 1 em cima
                float meia = (1f - Mathf.Abs(v - 0.5f) * 2f) * 0.5f * w;
                bool dentro = Mathf.Abs(x - (w - 1) * 0.5f) < meia;
                px[y * w + x] = !dentro ? Color.clear : v > 0.5f ? new Color(0.66f, 0.1f, 0.07f) : new Color(0.85f, 0.83f, 0.78f);
            }
        // miolo
        for (int y = h / 2 - 3; y <= h / 2 + 3; y++) for (int x = w / 2 - 3; x <= w / 2 + 3; x++) px[y * w + x] = new Color(0.25f, 0.2f, 0.12f);
        t.SetPixels(px); t.Apply();
        return t;
    }

    void OnDestroy() { if (aberto) Time.timeScale = escalaAntes; }
}
