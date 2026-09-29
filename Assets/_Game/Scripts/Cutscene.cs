using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

/// Um plano de cutscene: para onde a câmera vai, o que se lê, quanto dura.
[System.Serializable]
public class PlanoDeCutscene
{
    [Tooltip("Só pro designer se achar na lista.")]
    public string nota;

    [Tooltip("Para onde a câmera vai. Vazio = fica onde está.")]
    public Transform camera;
    [Tooltip("Segundos que a câmera leva pra chegar. 0 = corte seco.")]
    public float tempoDeMovimento = 1.6f;
    // Numa camera ortografica e ISTO que muda o enquadramento: mover a camera
    // so translada o quadro, nao abre nem fecha nele. 0 = mantem o atual.
    [Tooltip("Tamanho ortografico neste plano. 0 = mantem o atual.")]
    public float enquadramento;

    [TextArea] public string legenda;
    public float duracao = 3.5f;

    [Tooltip("Estresse cravado ao entrar neste plano.")]
    public float estresse;
    [Tooltip("Silencia o ambiente por N segundos ao entrar neste plano.")]
    public float silencio;

    [Tooltip("Som tocado ao entrar neste plano (2D, sem posição).")]
    public AudioClip som;
    [Range(0f, 1f)] public float volumeDoSom = 0.8f;
    [Tooltip("A legenda é o Miller pensando/falando: itálico, sem caixa alta.")]
    public bool pensamento;
}

/// Cutscene em câmera de jogo.
///
/// Nada de timeline nem de cena separada: a câmera é a mesma, o nível é o
/// mesmo, o jogador só perde o controle por alguns segundos. Isso é decisão
/// de escopo e também de linguagem — num jogo sobre não confiar na própria
/// percepção, tirar o controle sem tirar o jogador do lugar é mais eficaz do
/// que cortar pra um vídeo.
///
/// As tarjas existem por um motivo prático além do estético: elas avisam que
/// o controle saiu das suas mãos. Sem esse aviso, cutscene lê como travamento.
public class Cutscene : MonoBehaviour
{
    [Header("Quando")]
    public bool aoIniciarACena;
    public float atrasoInicial = 0.8f;

    [Header("Planos")]
    public PlanoDeCutscene[] planos;

    [Header("Apresentação")]
    public float tarjas = 0.12f;              // fração da altura da tela
    public float tempoDasTarjas = 0.5f;
    public int tamanhoDaLegenda = 20;

    public bool Tocando { get; private set; }
    public bool Tocou { get; private set; }

    float enquadramentoDeJogo;

    float tarjaAtual;
    float legendaAlfa;
    string legenda = "";
    bool legendaPensamento;
    AudioSource fonte;

    CameraRig rig;
    SoldierController jogador;
    Coroutine rodando;

    void Start()
    {
        rig = FindFirstObjectByType<CameraRig>();
        jogador = FindFirstObjectByType<SoldierController>();

        if (aoIniciarACena) StartCoroutine(EsperarETocar());
    }

    IEnumerator EsperarETocar()
    {
        // deixa a transição de entrada terminar antes: cutscene por cima de
        // fade é imagem em cima de imagem, e não se lê nem uma nem outra
        while (Transicao.Instance != null && Transicao.Instance.EmTransicao) yield return null;
        yield return new WaitForSeconds(atrasoInicial);
        Tocar();
    }

    public void Tocar()
    {
        if (Tocando || planos == null || planos.Length == 0) return;
        rodando = StartCoroutine(Rodar());
    }

    void Update()
    {
        if (!Tocando) return;
        var kb = Keyboard.current;
        if (kb == null) return;
        if (kb.escapeKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame) Pular();
    }

    /// Cutscene que nao se pula e um imposto cobrado a cada iteracao. Esta aqui
    /// tanto pro jogador na segunda partida quanto pra quem esta afinando o bloco.
    public void Pular()
    {
        if (!Tocando) return;
        if (rodando != null) StopCoroutine(rodando);
        if (fonte != null) fonte.Stop();

        tarjaAtual = 0f;
        legendaAlfa = 0f;
        legenda = "";

        var cam = rig != null && rig.topDownCamera != null ? rig.topDownCamera : Camera.main;
        if (cam != null && enquadramentoDeJogo > 0f) cam.orthographicSize = enquadramentoDeJogo;

        if (rig != null) rig.Cinematica = false;
        if (jogador != null) jogador.Travado = false;

        Tocando = false;
        Debug.Log("[CUTSCENE] pulada");
    }

    IEnumerator Rodar()
    {
        Tocando = true;
        Tocou = true;

        if (jogador != null) jogador.Travado = true;
        if (rig != null) rig.Cinematica = true;

        Camera cam = rig != null && rig.topDownCamera != null ? rig.topDownCamera : Camera.main;
        if (cam != null) enquadramentoDeJogo = cam.orthographicSize;

        for (float t = 0f; t < tempoDasTarjas; t += Time.deltaTime)
        {
            tarjaAtual = Mathf.Lerp(0f, tarjas, t / tempoDasTarjas);
            yield return null;
        }
        tarjaAtual = tarjas;

        foreach (var p in planos)
        {
            if (p.estresse > 0f) StressSystem.Instance?.Spike(p.estresse);
            if (p.silencio > 0f) FindFirstObjectByType<Ambience>()?.Hush(p.silencio);
            if (p.som != null)
            {
                if (fonte == null) { fonte = gameObject.AddComponent<AudioSource>(); fonte.spatialBlend = 0f; fonte.playOnAwake = false; }
                fonte.PlayOneShot(p.som, p.volumeDoSom);
            }

            if (cam != null && (p.camera != null || p.enquadramento > 0f))
            {
                Vector3 de = cam.transform.position;
                Quaternion giroDe = cam.transform.rotation;
                float tamDe = cam.orthographicSize;

                Vector3 para = p.camera != null ? p.camera.position : de;
                Quaternion giroPara = p.camera != null ? p.camera.rotation : giroDe;
                float tamPara = p.enquadramento > 0f ? p.enquadramento : tamDe;

                if (p.tempoDeMovimento <= 0.01f)
                {
                    cam.transform.SetPositionAndRotation(para, giroPara);
                    cam.orthographicSize = tamPara;
                }
                else
                {
                    for (float t = 0f; t < p.tempoDeMovimento; t += Time.deltaTime)
                    {
                        float k = Suavizar(t / p.tempoDeMovimento);
                        cam.transform.SetPositionAndRotation(
                            Vector3.Lerp(de, para, k), Quaternion.Slerp(giroDe, giroPara, k));
                        cam.orthographicSize = Mathf.Lerp(tamDe, tamPara, k);
                        yield return null;
                    }
                    cam.transform.SetPositionAndRotation(para, giroPara);
                    cam.orthographicSize = tamPara;
                }
            }

            legenda = p.legenda;
            legendaPensamento = p.pensamento;
            if (!string.IsNullOrEmpty(legenda))
                for (float t = 0f; t < 0.35f; t += Time.deltaTime) { legendaAlfa = t / 0.35f; yield return null; }

            yield return new WaitForSeconds(p.duracao);

            if (!string.IsNullOrEmpty(legenda))
                for (float t = 0f; t < 0.35f; t += Time.deltaTime) { legendaAlfa = 1f - t / 0.35f; yield return null; }
            legendaAlfa = 0f;
            legenda = "";
        }

        // o quadro volta ao tamanho de jogo junto com as tarjas: devolver o
        // controle com o zoom errado seria devolver outro jogo
        float tamFinal = cam != null ? cam.orthographicSize : 0f;
        for (float t = 0f; t < tempoDasTarjas; t += Time.deltaTime)
        {
            float k = t / tempoDasTarjas;
            tarjaAtual = Mathf.Lerp(tarjas, 0f, k);
            if (cam != null) cam.orthographicSize = Mathf.Lerp(tamFinal, enquadramentoDeJogo, k);
            yield return null;
        }
        tarjaAtual = 0f;
        if (cam != null) cam.orthographicSize = enquadramentoDeJogo;

        // o rig volta a seguir o jogador com o lerp dele: a câmera desliza de
        // volta em vez de teleportar, e a devolução do controle fica legível
        if (rig != null) rig.Cinematica = false;
        if (jogador != null) jogador.Travado = false;

        Tocando = false;
    }

    static float Suavizar(float k) => k * k * (3f - 2f * k);

    void OnGUI()
    {
        if (tarjaAtual <= 0.001f && legendaAlfa <= 0.001f) return;

        GUI.depth = -500;
        var cor = GUI.color;

        if (tarjaAtual > 0.001f)
        {
            float h = Screen.height * tarjaAtual;
            GUI.color = new Color(0f, 0f, 0f, 1f);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, h), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(0f, Screen.height - h, Screen.width, h), Texture2D.whiteTexture);
        }

        if (legendaAlfa > 0.001f && !string.IsNullOrEmpty(legenda))
        {
            var est = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = Mathf.RoundToInt(tamanhoDaLegenda * (legendaPensamento ? 1.15f : 1f) * Screen.height / 1080f),
                wordWrap = true,
                fontStyle = legendaPensamento ? FontStyle.Italic : FontStyle.Normal
            };
            est.normal.textColor = legendaPensamento ? new Color(0.82f, 0.82f, 0.8f, legendaAlfa) : new Color(0.9f, 0.88f, 0.84f, legendaAlfa);
            GUI.color = Color.white;

            float margem = Screen.width * 0.12f;
            GUI.Label(new Rect(margem, Screen.height * 0.78f, Screen.width - margem * 2f, 90f), legenda, est);
        }

        GUI.color = cor;
    }
}
