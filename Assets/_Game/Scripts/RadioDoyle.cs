using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// O narrador do capítulo: o rádio furado de bala no peito do Miller.
///
/// Fala com legenda, estalo de abrir e fechar o canal e um murmúrio de voz
/// abafada sintetizado no tamanho da frase — não tem dublagem, mas o ouvido
/// reconhece "tem alguém falando no rádio". O pensamento do Miller sai sem
/// rádio: sem estalo, sem chiado, em outra cor.
///
/// Fila: uma fala espera a outra. Quem chama só enfileira.
public class RadioDoyle : MonoBehaviour
{
    public enum Quem { Doyle, Miller, MillerNoRadio }

    public static RadioDoyle Instance { get; private set; }

    [Range(0f, 1f)] public float volumeDaVoz = 0.55f;
    [Range(0f, 1f)] public float volumeDoChiado = 0.22f;
    public int tamanhoDaLetra = 22;

    struct Fala { public Quem quem; public string texto; public float antes; }

    readonly Queue<Fala> fila = new Queue<Fala>();
    AudioSource canal, voz;
    AudioClip estalo, chiado;
    string texto;
    Quem quem;
    float alfa, alvoAlfa;
    bool tocando;

    /// Fala na fila ou tocando agora.
    public bool Ocupado => tocando || fila.Count > 0;

    /// O que está escrito na tela agora, e de quem é. Só pra teste e depuração.
    public string Legenda => texto;
    public Quem QuemFala => quem;

    void Awake()
    {
        Instance = this;
        canal = gameObject.AddComponent<AudioSource>();
        canal.spatialBlend = 0f; canal.loop = true; canal.volume = 0f;
        voz = gameObject.AddComponent<AudioSource>();
        voz.spatialBlend = 0f;
        estalo = Estalo();
        chiado = Chiado();
        canal.clip = chiado;
    }

    /// Depois que o corpo sobe do poço, a voz para de ser uma pessoa falando.
    ///
    /// A revelação de que o rádio nunca funcionou só vem no fim, e até lá o
    /// "Doyle" continuava batendo papo normal — o que lia como bug, não como
    /// terror. Daqui pra frente ele fica grave, afogado no chiado, e só
    /// REPETE frases que já disse: quem está do outro lado não tem o que
    /// dizer de novo, porque nunca teve.
    [System.NonSerialized] public bool VozQuebrada;

    readonly List<string> jaDitas = new List<string>();
    int repeticao;

    /// Surto de chiado: o canal abre sozinho e fica um tempo aberto.
    public void Surto(float segundos) => StartCoroutine(Chiar(segundos));

    IEnumerator Chiar(float segundos)
    {
        canal.volume = volumeDoChiado * 2.6f;
        if (!canal.isPlaying) canal.Play();
        voz.PlayOneShot(estalo, 0.9f);
        yield return new WaitForSeconds(segundos);
        if (!Ocupado) canal.volume = 0f;
    }

    /// `inedito` passa por cima da repetição: para a última fala do jogo,
    /// que precisa ser nova mesmo com a voz já quebrada.
    public void Diga(Quem q, string t, float antes, bool inedito)
    {
        if (VozQuebrada && q == Quem.Doyle && !inedito && jaDitas.Count > 0)
            t = jaDitas[repeticao++ % jaDitas.Count];
        else if (q == Quem.Doyle && !jaDitas.Contains(t))
            jaDitas.Add(t);
        Diga(q, t, antes);
    }

    public void Diga(Quem q, string t, float antes = 0f)
    {
        fila.Enqueue(new Fala { quem = q, texto = t, antes = antes });
        if (!tocando) StartCoroutine(Tocar());
    }

    public void Doyle(string t, float antes = 0f) => Diga(Quem.Doyle, t, antes, false);

    /// Fala do Doyle que NÃO vira repetição, mesmo com a voz já quebrada.
    public void DoyleInedito(string t, float antes = 0f) => Diga(Quem.Doyle, t, antes, true);
    public void Pensa(string t, float antes = 0f) => Diga(Quem.Miller, t, antes);

    IEnumerator Tocar()
    {
        tocando = true;
        while (fila.Count > 0)
        {
            var f = fila.Dequeue();
            if (f.antes > 0f) yield return new WaitForSeconds(f.antes);

            bool pelaRadio = f.quem != Quem.Miller;
            float dur = 1.6f + 0.055f * f.texto.Length;

            if (pelaRadio)
            {
                bool quebrada = VozQuebrada && f.quem == Quem.Doyle;
                voz.PlayOneShot(estalo, 0.7f);
                // com a voz quebrada o chiado quase engole a fala, e o canal
                // demora mais pra "achar" o outro lado
                canal.volume = quebrada ? volumeDoChiado * 2.3f : volumeDoChiado;
                if (!canal.isPlaying) canal.Play();
                yield return new WaitForSeconds(quebrada ? 0.75f : 0.25f);
                voz.pitch = f.quem == Quem.Doyle ? 1f : 0.93f;
                if (quebrada) voz.pitch = 0.78f + Random.Range(-0.05f, 0.05f);
                voz.PlayOneShot(Murmurio(dur * 0.8f, f.texto.GetHashCode()),
                                quebrada ? volumeDaVoz * 0.75f : volumeDaVoz);
            }

            quem = f.quem; texto = f.texto; alvoAlfa = 1f;
            yield return new WaitForSeconds(dur);
            alvoAlfa = 0f;

            if (pelaRadio)
            {
                voz.PlayOneShot(estalo, 0.55f);
                canal.volume = 0f;
            }
            yield return new WaitForSeconds(0.45f);
        }
        tocando = false;
    }

    void Update()
    {
        alfa = Mathf.MoveTowards(alfa, alvoAlfa, Time.deltaTime * 4f);
        // a legenda dele também não se sustenta mais: pisca como sinal ruim
        if (VozQuebrada && quem == Quem.Doyle && alvoAlfa > 0.5f)
            alfa *= 0.72f + 0.28f * Mathf.PerlinNoise(Time.time * 11f, 0.3f);
    }

    void OnGUI()
    {
        if (alfa <= 0.01f || string.IsNullOrEmpty(texto)) return;
        float esc = Screen.height / 1080f;
        var st = new GUIStyle(GUI.skin.label)
        {
            fontSize = Mathf.RoundToInt(tamanhoDaLetra * esc),
            alignment = TextAnchor.MiddleCenter,
            wordWrap = true,
            fontStyle = quem == Quem.Miller ? FontStyle.Italic : FontStyle.Normal
        };
        var nome = new GUIStyle(st) { fontSize = Mathf.RoundToInt(tamanhoDaLetra * 0.62f * esc), fontStyle = FontStyle.Bold };

        Color cor = quem == Quem.Doyle ? new Color(0.72f, 0.86f, 0.7f) : quem == Quem.MillerNoRadio ? new Color(0.86f, 0.7f, 0.62f) : new Color(0.82f, 0.82f, 0.8f);
        string rotulo = quem == Quem.Doyle ? "RÁDIO  —  DOYLE" : quem == Quem.MillerNoRadio ? "RÁDIO  —  MILLER" : "";

        float w = Screen.width * 0.62f, h = 90f * esc;
        var r = new Rect((Screen.width - w) * 0.5f, Screen.height - h - 60f * esc, w, h);

        var prev = GUI.color;
        // sombra pra ler em cima da chuva
        GUI.color = new Color(0f, 0f, 0f, 0.85f * alfa);
        GUI.Label(new Rect(r.x + 2, r.y + 2, r.width, r.height), texto, st);
        GUI.color = new Color(cor.r, cor.g, cor.b, alfa);
        GUI.Label(r, texto, st);
        if (rotulo.Length > 0)
        {
            GUI.color = new Color(cor.r, cor.g, cor.b, 0.6f * alfa);
            GUI.Label(new Rect(r.x, r.y - 26f * esc, r.width, 24f * esc), rotulo, nome);
        }
        GUI.color = prev;
    }

    // ---------------------------------------------------------------- som

    const int Taxa = 22050;

    static AudioClip Estalo()
    {
        int n = Taxa / 12;
        var d = new float[n];
        var rnd = new System.Random(3);
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)n;
            d[i] = (float)(rnd.NextDouble() * 2 - 1) * Mathf.Exp(-t * 18f) * 0.8f
                 + Mathf.Sin(i * 0.9f) * Mathf.Exp(-t * 40f) * 0.4f;
        }
        var c = AudioClip.Create("estalo", n, 1, Taxa, false); c.SetData(d, 0); return c;
    }

    static AudioClip Chiado()
    {
        int n = Taxa * 3;
        var d = new float[n];
        var rnd = new System.Random(9);
        float lp = 0f;
        for (int i = 0; i < n; i++)
        {
            float w = (float)(rnd.NextDouble() * 2 - 1);
            lp = Mathf.Lerp(lp, w, 0.45f);
            float onda = 0.75f + 0.25f * Mathf.Sin(i / (float)Taxa * 7f);
            d[i] = (w - lp) * 0.5f * onda;
        }
        var c = AudioClip.Create("chiado", n, 1, Taxa, false); c.SetData(d, 0); return c;
    }

    /// Voz de rádio abafada: pulsos de sílaba com uma "garganta" (serra em
    /// 95–150 Hz) passada por filtro de banda de rádio. Não se entende palavra,
    /// mas se entende que é gente falando.
    static AudioClip Murmurio(float dur, int semente)
    {
        int n = Mathf.CeilToInt(Taxa * dur);
        var d = new float[n];
        var rnd = new System.Random(semente);
        float fase = 0f, env = 0f, alvo = 0f, f0 = 120f;
        int proxima = 0;
        float hp = 0f, antes = 0f, lp = 0f;
        for (int i = 0; i < n; i++)
        {
            if (i >= proxima)
            {
                bool silaba = rnd.NextDouble() < 0.8;
                alvo = silaba ? 0.6f + (float)rnd.NextDouble() * 0.4f : 0f;
                f0 = 95f + (float)rnd.NextDouble() * 55f;
                proxima = i + (int)(Taxa * (silaba ? 0.09f + rnd.NextDouble() * 0.14f : 0.04f + rnd.NextDouble() * 0.12f));
            }
            env = Mathf.Lerp(env, alvo, 0.004f);
            fase += f0 / Taxa; if (fase > 1f) fase -= 1f;
            float s = (fase * 2f - 1f) + (float)(rnd.NextDouble() * 2 - 1) * 0.35f;
            lp = Mathf.Lerp(lp, s, 0.35f);             // corta o agudo
            hp = 0.9f * (hp + lp - antes); antes = lp;  // corta o grave: banda de rádio
            d[i] = Mathf.Clamp(hp * env * 1.6f, -1f, 1f);
        }
        var c = AudioClip.Create("murmurio", n, 1, Taxa, false); c.SetData(d, 0); return c;
    }
}
