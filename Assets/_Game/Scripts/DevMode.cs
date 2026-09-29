using UnityEngine;
using UnityEngine.InputSystem;

/// Modo de desenvolvedor: validar o mapa sem precisar jogar o jogo.
///
///   F2  liga/desliga o modo (desligado, nada disto existe)
///   F3  ESP        — guardas visíveis através da escuridão, com nome, distância,
///                    estado, detecção e o cone de visão desenhado no chão
///   F4  traçador   — linha até o próximo elo da corrente, mais todos os pontos
///                    de interação e exploração com nome e distância
///   F5  deus       — ser pego não conta
///   F6  invisível  — guarda não vê e não ouve o jogador
///   F7  luz        — levanta a escuridão da tela inteira
///   F8  pular      — teleporta pro próximo elo da corrente
///   F9  velocidade — anda 3x mais rápido
///
/// O resto do jogo não sabe de nada disto: GuardAI, Noise, PrototypeDirector e
/// VisaoDarkwood só consultam as chaves estáticas daqui.
public class DevMode : MonoBehaviour
{
    public static bool Ativo, Esp = true, Tracador = true, Deus = true, Invisivel = true, Luz, Rapido;

    // só valem com o modo ligado: o jogo normal nunca vê isto
    public static bool Fantasma => Ativo && Invisivel;
    public static bool Imortal => Ativo && Deus;
    public static bool VerTudo => Ativo && Esp;

    static readonly int Relampago = Shader.PropertyToID("_EcosRelampago");

    Camera cam;
    Transform jogador;
    SoldierController sc;
    float velBase = -1f, corridaBase, agachadoBase;
    GUIStyle rot, painel;
    static Texture2D pixel;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Zerar() { Ativo = false; Luz = false; Rapido = false; }

    // entra em qualquer cena sozinho: não precisa pôr nada na hierarquia
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Criar()
    {
        if (FindFirstObjectByType<DevMode>() != null) return;
        var go = new GameObject("DevMode");
        DontDestroyOnLoad(go);
        go.AddComponent<DevMode>();
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;
        if (kb.f2Key.wasPressedThisFrame) { Ativo = !Ativo; if (!Ativo) Restaurar(); }
        if (!Ativo) return;
        if (kb.f3Key.wasPressedThisFrame) Esp = !Esp;
        if (kb.f4Key.wasPressedThisFrame) Tracador = !Tracador;
        if (kb.f5Key.wasPressedThisFrame) Deus = !Deus;
        if (kb.f6Key.wasPressedThisFrame) Invisivel = !Invisivel;
        if (kb.f7Key.wasPressedThisFrame) { Luz = !Luz; if (!Luz) Shader.SetGlobalFloat(Relampago, 0f); }
        if (kb.f8Key.wasPressedThisFrame) PularProElo();
        if (kb.f9Key.wasPressedThisFrame) Rapido = !Rapido;

        Achar();
        if (Luz) Shader.SetGlobalFloat(Relampago, 1f);
        Velocidade();
    }

    void Achar()
    {
        if (sc == null) { sc = FindFirstObjectByType<SoldierController>(); if (sc != null) jogador = sc.transform; }
        if (cam == null)
        {
            var rig = FindFirstObjectByType<CameraRig>();
            cam = rig != null && rig.topDownCamera != null ? rig.topDownCamera : Camera.main;
        }
    }

    void Velocidade()
    {
        if (sc == null) return;
        if (velBase < 0f) { velBase = sc.walkSpeed; corridaBase = sc.runSpeed; agachadoBase = sc.crouchSpeed; }
        float k = Rapido ? 3f : 1f;
        sc.walkSpeed = velBase * k; sc.runSpeed = corridaBase * k; sc.crouchSpeed = agachadoBase * k;
    }

    void Restaurar()
    {
        Shader.SetGlobalFloat(Relampago, 0f);
        Rapido = false;
        Velocidade();
    }

    void PularProElo()
    {
        var h = Historia.Instance;
        if (h == null || jogador == null) return;
        if (!h.ProximoElo(out Vector3 alvo, out _, out _)) return;
        var cc = jogador.GetComponent<CharacterController>();
        var t = Terrain.activeTerrain;
        // um passo antes do alvo, pra não cair dentro do objeto
        var dir = alvo - jogador.position; dir.y = 0f;
        var p = alvo - (dir.sqrMagnitude > 0.01f ? dir.normalized * 1.2f : Vector3.zero);
        float y = t != null ? t.SampleHeight(p) + t.transform.position.y : p.y;
        if (cc != null) cc.enabled = false;
        jogador.position = new Vector3(p.x, y + (cc != null ? cc.height * 0.5f - cc.center.y + 0.05f : 1f), p.z);
        if (cc != null) cc.enabled = true;
    }

    // ---------------------------------------------------------------- tela

    void OnGUI()
    {
        if (!Ativo) return;
        Achar();
        if (pixel == null) { pixel = new Texture2D(1, 1); pixel.SetPixel(0, 0, Color.white); pixel.Apply(); }
        float esc = Screen.height / 1080f;
        if (rot == null) rot = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
        if (painel == null) painel = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };
        rot.fontSize = Mathf.RoundToInt(13 * esc);
        painel.fontSize = Mathf.RoundToInt(15 * esc);

        Painel(esc);
        if (cam == null || jogador == null) return;
        if (Esp) DesenharEsp();
        if (Tracador) DesenharTracador();
    }

    void Painel(float esc)
    {
        string L(bool b) => b ? "<color=#7CFC7C>ON</color>" : "<color=#FF7070>off</color>";
        painel.richText = true;
        string h = Historia.Instance != null ? Historia.Instance.Estado : "sem Historia";
        string t = "<b>MODO DEV</b>  [F2]\n" +
                   "[F3] ESP " + L(Esp) + "   [F4] traçador " + L(Tracador) + "\n" +
                   "[F5] deus " + L(Deus) + "   [F6] invisível " + L(Invisivel) + "\n" +
                   "[F7] luz " + L(Luz) + "   [F9] rápido " + L(Rapido) + "   [F8] pular pro elo\n" +
                   "<size=" + Mathf.RoundToInt(12 * esc) + ">" + h + "</size>";
        var r = new Rect(Screen.width - 470f * esc, 14f * esc, 456f * esc, 110f * esc);
        GUI.color = new Color(0f, 0f, 0f, 0.6f); GUI.DrawTexture(r, pixel); GUI.color = Color.white;
        GUI.Label(new Rect(r.x + 10f * esc, r.y + 4f * esc, r.width - 20f * esc, r.height), t, painel);
    }

    void DesenharEsp()
    {
        foreach (var g in FindObjectsByType<GuardAI>(FindObjectsSortMode.None))
        {
            var p = g.transform.position;
            float d = Plano(p, jogador.position);
            Color cor = g.Detection > 0.5f ? new Color(1f, 0.2f, 0.2f) : g.CurrentState != GuardAI.State.Patrulha ? new Color(1f, 0.75f, 0.2f) : new Color(1f, 0.45f, 0.45f);

            // cone de visão no chão (alcance efetivo, com lanterna)
            float alc = g.EffectiveRange, meio = g.viewAngle * 0.5f;
            var f = g.transform.forward; f.y = 0f; f.Normalize();
            Vector3 anterior = p;
            for (int k = 0; k <= 12; k++)
            {
                var dirK = Quaternion.Euler(0f, -meio + meio * 2f * k / 12f, 0f) * f;
                var q = p + dirK * alc;
                Linha(anterior, q, new Color(cor.r, cor.g, cor.b, 0.55f), 1.5f);
                anterior = q;
            }
            Linha(anterior, p, new Color(cor.r, cor.g, cor.b, 0.55f), 1.5f);

            // caixa + texto
            var s = cam.WorldToScreenPoint(p + Vector3.up * 1f);
            if (s.z <= 0f) continue;
            float lado = 26f;
            var caixa = new Rect(s.x - lado, Screen.height - s.y - lado * 1.6f, lado * 2f, lado * 2.6f);
            Borda(caixa, cor, 2f);
            string txt = g.name + "  " + d.ToString("0") + "m\n" + g.CurrentState + (g.sentinela ? " (sentinela)" : "") + "  det " + Mathf.RoundToInt(g.Detection * 100f) + "%";
            Texto(new Rect(s.x - 150f, Screen.height - s.y - lado * 1.6f - 36f, 300f, 34f), txt, cor);
        }
        foreach (var l in FindObjectsByType<Listener>(FindObjectsSortMode.None))
        {
            var s = cam.WorldToScreenPoint(l.transform.position + Vector3.up);
            if (s.z <= 0f) continue;
            var cor = l.Alarmed ? Color.red : new Color(0.55f, 0.75f, 1f);
            Borda(new Rect(s.x - 20f, Screen.height - s.y - 20f, 40f, 40f), cor, 2f);
            Texto(new Rect(s.x - 150f, Screen.height - s.y - 50f, 300f, 20f), l.name + " (ouvinte) " + Plano(l.transform.position, jogador.position).ToString("0") + "m", cor);
            // raio de "andando" e de chamado
            Circulo(l.transform.position, 6f * l.hearingSensitivity, new Color(0.55f, 0.75f, 1f, 0.35f));
        }
    }

    void DesenharTracador()
    {
        var h = Historia.Instance;
        // todos os pontos de interação e exploração
        foreach (var i in FindObjectsByType<Interagivel>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var s = cam.WorldToScreenPoint(i.transform.position + Vector3.up * 0.5f);
            if (s.z <= 0f) continue;
            bool livre = i.isActiveAndEnabled;
            bool lembranca = i.id.StartsWith("lembranca");
            var cor = !livre ? new Color(0.6f, 0.6f, 0.6f, 0.7f) : lembranca ? new Color(0.6f, 0.9f, 1f) : new Color(1f, 0.9f, 0.35f);
            Borda(new Rect(s.x - 7f, Screen.height - s.y - 7f, 14f, 14f), cor, 2f);
            Texto(new Rect(s.x - 150f, Screen.height - s.y + 8f, 300f, 20f), i.rotulo + "  " + Plano(i.transform.position, jogador.position).ToString("0") + "m" + (livre ? "" : " (travado)"), cor);
        }
        // a linha até o próximo elo
        if (h != null && h.ProximoElo(out Vector3 alvo, out string id, out _))
        {
            var cor = new Color(0.3f, 1f, 0.45f);
            Linha(jogador.position, alvo, cor, 3f);
            var s = cam.WorldToScreenPoint(alvo + Vector3.up * 1.2f);
            string nome = id ?? (h.Atual == Historia.Passo.Acordar ? "o portão (área)" : "a saída");
            if (s.z > 0f && s.x > 0 && s.x < Screen.width && s.y > 0 && s.y < Screen.height)
                Texto(new Rect(s.x - 160f, Screen.height - s.y - 24f, 320f, 22f), "PRÓXIMO: " + nome.ToUpper() + "  " + Plano(alvo, jogador.position).ToString("0") + "m", cor);
            else
            {
                // fora da tela: seta na borda apontando pro objetivo
                var sj = cam.WorldToScreenPoint(jogador.position);
                var v = new Vector2(s.x - sj.x, s.y - sj.y); if (s.z <= 0f) v = -v;
                v.Normalize();
                var c = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
                float k = Mathf.Min((Screen.width * 0.45f) / Mathf.Max(0.001f, Mathf.Abs(v.x)), (Screen.height * 0.42f) / Mathf.Max(0.001f, Mathf.Abs(v.y)));
                var b = c + v * k;
                Texto(new Rect(b.x - 160f, Screen.height - b.y - 11f, 320f, 22f), "▶ " + nome.ToUpper() + "  " + Plano(alvo, jogador.position).ToString("0") + "m", cor);
            }
        }
    }

    // ---------------------------------------------------------------- desenho

    static float Plano(Vector3 a, Vector3 b) { a.y = 0f; b.y = 0f; return Vector3.Distance(a, b); }

    void Linha(Vector3 a, Vector3 b, Color cor, float largura)
    {
        var sa = cam.WorldToScreenPoint(a); var sb = cam.WorldToScreenPoint(b);
        if (sa.z <= 0f || sb.z <= 0f) return;
        var p1 = new Vector2(sa.x, Screen.height - sa.y); var p2 = new Vector2(sb.x, Screen.height - sb.y);
        float ang = Mathf.Atan2(p2.y - p1.y, p2.x - p1.x) * Mathf.Rad2Deg;
        float comp = (p2 - p1).magnitude;
        var m = GUI.matrix;
        GUIUtility.RotateAroundPivot(ang, p1);
        GUI.color = cor;
        GUI.DrawTexture(new Rect(p1.x, p1.y - largura * 0.5f, comp, largura), pixel);
        GUI.matrix = m;
        GUI.color = Color.white;
    }

    void Circulo(Vector3 c, float r, Color cor)
    {
        Vector3 ant = c + Vector3.forward * r;
        for (int k = 1; k <= 24; k++)
        {
            float a = k * Mathf.PI * 2f / 24f;
            var q = c + new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * r;
            Linha(ant, q, cor, 1f);
            ant = q;
        }
    }

    static void Borda(Rect r, Color cor, float e)
    {
        GUI.color = cor;
        GUI.DrawTexture(new Rect(r.x, r.y, r.width, e), pixel);
        GUI.DrawTexture(new Rect(r.x, r.yMax - e, r.width, e), pixel);
        GUI.DrawTexture(new Rect(r.x, r.y, e, r.height), pixel);
        GUI.DrawTexture(new Rect(r.xMax - e, r.y, e, r.height), pixel);
        GUI.color = Color.white;
    }

    void Texto(Rect r, string t, Color cor)
    {
        GUI.color = new Color(0f, 0f, 0f, 0.85f); GUI.Label(new Rect(r.x + 1, r.y + 1, r.width, r.height), t, rot);
        GUI.color = cor; GUI.Label(r, t, rot);
        GUI.color = Color.white;
    }
}
