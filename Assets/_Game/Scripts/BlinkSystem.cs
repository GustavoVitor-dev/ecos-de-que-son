using System.Collections;
using UnityEngine;

/// A lacuna de percepcao: o instante em que o jogador nao ve, e o mundo pode mudar.
///
/// Em top-down a camera nao sao os olhos do personagem, e uma visao externa —
/// entao aqui a lacuna NAO e uma palpebra, e o SINAL CAINDO. Corte seco,
/// vocabulario de transmissao, que e o tema do jogo inteiro.
/// Em primeira pessoa a camera e o olho, e ai sim vira palpebra com fade.
///
/// O corte seco tambem funciona melhor pro truque: fade lento deixa o olho
/// acompanhar, 40ms nao deixa. A substituicao fica mais violenta.
///
/// Regra que a entidade vai seguir (e quebrar uma unica vez):
/// ela so muda de estado durante uma lacuna. Assine o evento Blinked.
public class BlinkSystem : MonoBehaviour
{
    public enum Style { Interferencia, Palpebra }

    [Header("Corte na tela")]
    // O corte preto de tela cheia saiu: piscava a cada salto de estresse (matar
    // um soldado, ser visto) e a cada ~14 s na hipervigilância, e era odiado.
    // A lacuna agora é o instante ofuscado depois do relâmpago (Tempestade),
    // sem nada desenhado. Com isto desligado, as lacunas automáticas também param.
    [Tooltip("Desenha o corte preto e dispara lacunas por pico/ritmo. Desligado = só a lacuna do relâmpago.")]
    public bool corteVisivel = false;

    [Header("Gatilho principal: picos de estresse")]
    [Tooltip("A lacuna dispara quando o estresse da um salto. E o que torna a causa perceptivel: voce foi visto, e o sinal caiu.")]
    public bool dispararEmPicos = true;
    [Tooltip("Segundos minimos entre duas lacunas causadas por pico.")]
    public float cooldownDePico = 6f;

    [Header("Ritmo de fundo, por faixa. 0 = nunca")]
    [Tooltip("Rarissimo de proposito: o fundo e atmosfera, o pico e a causa.")]
    public float intervalAlerta = 0f;
    public float intervalRuptura = 35f;
    public float intervalHiper = 14f;
    [Range(0f, 0.8f)] public float jitter = 0.35f;

    [Header("Interferencia (top-down): corte seco")]
    public float cutHold = 0.13f;
    [Tooltip("Chance de dar um segundo corte logo em seguida, como falha de sinal.")]
    [Range(0f, 1f)] public float doubleCutChance = 0.35f;
    public float doubleCutGap = 0.06f;

    [Header("Palpebra (primeira pessoa): fade")]
    public float closeTime = 0.10f;
    public float holdTime = 0.09f;
    public float openTime = 0.16f;

    public bool IsClosed { get; private set; }
    public Style CurrentStyle =>
        (rig != null && rig.mode == CameraMode.FirstPerson) ? Style.Palpebra : Style.Interferencia;

    /// Disparado com a tela totalmente preta. E aqui que o mundo pode mudar
    /// sem o jogador ver acontecer.
    public event System.Action Blinked;

    float alpha;
    float timer;
    StressSystem stress;
    CameraRig rig;

    float cdPico;

    void Start()
    {
        stress = StressSystem.Instance;
        rig = FindFirstObjectByType<CameraRig>();
        if (stress != null) stress.Salto += AoSaltar;
        Schedule();
    }

    void OnDestroy()
    {
        if (stress != null) stress.Salto -= AoSaltar;
    }

    // O estresse pulou de uma vez. E aqui que o sinal cai — e por isso que
    // o jogador consegue ligar os pontos entre o que ele fez e o que aconteceu.
    void AoSaltar(float quanto)
    {
        if (!corteVisivel || !dispararEmPicos || IsClosed || cdPico > 0f) return;
        if (stress != null && stress.Current == StressSystem.Tier.Calmo) return;

        cdPico = cooldownDePico;
        StartCoroutine(Run());
    }

    void Update()
    {
        if (cdPico > 0f) cdPico -= Time.deltaTime;
        if (IsClosed || !corteVisivel) return;

        if (IntervalForTier() <= 0f) return;   // faixa sem ritmo de fundo

        timer -= Time.deltaTime;
        if (timer <= 0f) StartCoroutine(Run());
    }

    float IntervalForTier()
    {
        if (stress == null) stress = StressSystem.Instance;
        if (stress == null) return 0f;

        switch (stress.Current)
        {
            case StressSystem.Tier.Alerta: return intervalAlerta;
            case StressSystem.Tier.Ruptura: return intervalRuptura;
            case StressSystem.Tier.Hipervigilancia: return intervalHiper;
            default: return 0f;
        }
    }

    void Schedule()
    {
        float b = IntervalForTier();
        if (b <= 0f) b = intervalAlerta;
        timer = b * Random.Range(1f - jitter, 1f + jitter);
    }

    /// Forca uma lacuna. A entidade usa isso em momentos roteirizados.
    public void ForceBlink()
    {
        if (!corteVisivel) { LacunaSilenciosa(); return; }
        if (!IsClosed) StartCoroutine(Run());
    }

    /// A lacuna sem desenho: o relâmpago acabou de ofuscar a vista.
    public void LacunaSilenciosa()
    {
        if (IsClosed) return;
        Blinked?.Invoke();
    }

    IEnumerator Run()
    {
        IsClosed = true;

        if (CurrentStyle == Style.Interferencia) yield return Interference();
        else yield return Eyelid();

        alpha = 0f;
        IsClosed = false;
        Schedule();
    }

    /// Corte seco. Sem transicao: o quadro simplesmente nao esta mais la.
    IEnumerator Interference()
    {
        alpha = 1f;
        Blinked?.Invoke();                       // o mundo muda AQUI
        yield return new WaitForSeconds(cutHold);
        alpha = 0f;

        if (Random.value < doubleCutChance)
        {
            yield return new WaitForSeconds(doubleCutGap);
            alpha = 1f;
            yield return new WaitForSeconds(cutHold * 0.6f);
            alpha = 0f;
        }
    }

    /// Palpebra. So faz sentido quando a camera e o olho.
    IEnumerator Eyelid()
    {
        yield return Fade(0f, 1f, closeTime);
        Blinked?.Invoke();
        yield return new WaitForSeconds(holdTime);
        yield return Fade(1f, 0f, openTime);
    }

    IEnumerator Fade(float from, float to, float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            alpha = Mathf.Lerp(from, to, t / duration);
            yield return null;
        }
        alpha = to;
    }

    void OnGUI()
    {
        if (!corteVisivel || alpha <= 0.001f) return;

        GUI.depth = -1000;                       // por cima de tudo, inclusive do HUD
        Color prev = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, alpha);
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = prev;
    }
}
