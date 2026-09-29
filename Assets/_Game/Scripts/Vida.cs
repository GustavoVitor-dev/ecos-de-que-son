using System.Collections;
using UnityEngine;

/// A vida do Miller. Pouca, e ela não volta.
///
/// Antes era morte instantânea: o guarda te via e você renascia, sem nem
/// entender de onde veio. Isso punia o que o jogador não tinha como ver —
/// numa câmera de cima, à noite, com chuva. Agora você aguenta alguns tiros,
/// o que dá tempo de reagir: sumir na moita, correr pra fora do cone, encarar.
///
/// Mas não regenera. Um kit médico transformaria cada tiro num custo temporário
/// e a selva num corredor de tentativa e erro. Sem cura, a bala que você levou
/// no começo ainda está com você no poço — e isso é o capítulo inteiro ficando
/// mais apertado, que é o que um jogo de terror quer.
public class Vida : MonoBehaviour
{
    [Header("Quanto aguenta")]
    [Tooltip("Tiros até cair. Pouco de propósito.")]
    public int maximo = 3;
    [Tooltip("Segundos de carência depois de levar um tiro (senão uma rajada mata de uma vez).")]
    public float carencia = 1.15f;

    [Header("Tela")]
    public float tempoDoVermelho = 0.45f;

    public static Vida Instance { get; private set; }
    public int Atual { get; private set; }
    public bool Morto => Atual <= 0;
    public bool EmCarencia => Time.time < feridoAte;

    float feridoAte;
    float vermelho;
    float tremor;
    SoldierController soldado;
    GUIStyle estilo;

    void Awake()
    {
        Instance = this;
        Atual = maximo;
    }

    void Start() => soldado = GetComponent<SoldierController>();

    /// Leva dano. `de` é de onde veio o tiro: serve pro sangue e pro empurrão.
    public void Levar(int dano, Vector3 de)
    {
        if (DevMode.Imortal || Morto || EmCarencia || dano <= 0) return;

        Atual = Mathf.Max(0, Atual - dano);
        feridoAte = Time.time + carencia;
        vermelho = 1f;
        tremor = 1f;

        var direcao = (transform.position - de); direcao.y = 0f;
        Sangue.Jato(transform.position + Vector3.up * 1.2f, direcao.normalized * 0.6f + Vector3.up * 0.3f, 0.7f, 0.5f);
        if (StressSystem.Instance != null) StressSystem.Instance.Spike(22f);

        Debug.Log("[VIDA] -" + dano + " → " + Atual + "/" + maximo);

        if (Atual <= 0) Cair();
    }

    /// Curar só existe pra recomeço: dentro do capítulo, ninguém cura.
    public void Encher() => Atual = maximo;

    void Cair()
    {
        Debug.Log("[VIDA] o Miller caiu");
        if (PrototypeDirector.Instance != null) PrototypeDirector.Instance.Morrer();
        else Encher();
        StartCoroutine(Renascer());
    }

    IEnumerator Renascer()
    {
        // o diretor já teleportou pro checkpoint; aqui só devolve o fôlego
        yield return new WaitForSeconds(0.2f);
        Encher();
        vermelho = 1f;
    }

    void Update()
    {
        vermelho = Mathf.MoveTowards(vermelho, 0f, Time.deltaTime / Mathf.Max(0.05f, tempoDoVermelho));
        tremor = Mathf.MoveTowards(tremor, 0f, Time.deltaTime * 2.2f);
    }

    // ---------------------------------------------------------------- tela

    void OnGUI()
    {
        float esc = Screen.height / 1080f;

        // sangue na tela quando leva tiro, e uma borda fixa quando está por um fio
        // forte o bastante pra assustar, fraco o bastante pra continuar jogando
        float borda = vermelho * 0.4f + (Atual == 1 ? 0.16f + Mathf.Sin(Time.time * 3.4f) * 0.04f : 0f);
        if (borda > 0.01f)
        {
            var prev = GUI.color;
            GUI.color = new Color(0.35f, 0.02f, 0.02f, borda);
            float h = Screen.height * 0.16f, w = Screen.width * 0.11f;
            GUI.DrawTexture(new Rect(0, 0, Screen.width, h), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(0, Screen.height - h, Screen.width, h), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(0, 0, w, Screen.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(Screen.width - w, 0, w, Screen.height), Texture2D.whiteTexture);
            GUI.color = prev;
        }

        // as plaquetas: uma por vida. Discreto, no canto de baixo à direita.
        if (estilo == null) estilo = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
        estilo.fontSize = Mathf.RoundToInt(15 * esc);
        float lado = 26f * esc, gap = 8f * esc;
        float x0 = Screen.width - 30f * esc - (lado + gap) * maximo;
        for (int i = 0; i < maximo; i++)
        {
            var r = new Rect(x0 + i * (lado + gap), Screen.height - 46f * esc, lado, lado * 1.5f);
            var prev = GUI.color;
            bool tem = i < Atual;
            GUI.color = tem ? new Color(0.78f, 0.74f, 0.62f, 0.85f) : new Color(0.3f, 0.06f, 0.05f, 0.7f);
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = new Color(0f, 0f, 0f, 0.8f);
            GUI.DrawTexture(new Rect(r.x + 2, r.y + 2, r.width - 4, r.height - 4), Texture2D.whiteTexture);
            GUI.color = tem ? new Color(0.72f, 0.68f, 0.56f, 0.9f) : new Color(0.4f, 0.1f, 0.08f, 0.8f);
            GUI.Label(r, tem ? "|" : "×", estilo);
            GUI.color = prev;
        }
    }

    /// Tremor de câmera pra quem quiser ler (o CameraRig pode usar).
    public float Tremor => tremor;
}
