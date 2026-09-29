using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// A passagem de um bloco para o outro.
///
/// Antes disto o jogo dava um `LoadScene` seco: você atravessava o portão e
/// no frame seguinte estava em outro lugar, sem respiro. Corte duro não é
/// ritmo, é falha de montagem — e num jogo cujo efeito central é o jogador
/// não conseguir apontar o momento em que as coisas mudam, cortar na cara
/// destrói justamente o que a lacuna de percepção constrói.
///
/// O preto aqui é o oposto da lacuna: aquele é rápido e você duvida que
/// aconteceu; este é lento e declarado, e serve pra você respirar entre atos.
public class Transicao : MonoBehaviour
{
    public static Transicao Instance { get; private set; }

    [Header("Tempos")]
    public float tempoDeSaida = 1.2f;
    public float tempoDeCartela = 2.8f;
    public float tempoDeEntrada = 1.6f;

    [Header("Cartela")]
    public int tamanhoDoTitulo = 42;
    public int tamanhoDaLegenda = 17;

    public bool EmTransicao { get; private set; }

    float preto;
    float textoAlfa;
    string titulo = "", legenda = "";

    void Awake() => Instance = this;

    void Start()
    {
        // chegando de outro bloco: a cena nasce preta e abre
        if (RunState.Transicionando)
        {
            RunState.Transicionando = false;
            preto = 1f;
            StartCoroutine(Entrar());
        }
    }

    /// Sai deste bloco para o próximo. O texto do beat aceita
    /// "Cena|TÍTULO|legenda" — só o nome da cena é obrigatório.
    public void Ir(string descricao)
    {
        if (EmTransicao || string.IsNullOrEmpty(descricao)) return;

        var partes = descricao.Split('|');
        string cena = partes[0].Trim();
        titulo = partes.Length > 1 ? partes[1].Trim() : "";
        legenda = partes.Length > 2 ? partes[2].Trim() : "";

        StartCoroutine(Sair(cena));
    }

    IEnumerator Sair(string cena)
    {
        EmTransicao = true;
        Travar(true);

        for (float t = 0f; t < tempoDeSaida; t += Time.deltaTime)
        {
            preto = Mathf.Clamp01(t / tempoDeSaida);
            yield return null;
        }
        preto = 1f;

        if (!string.IsNullOrEmpty(titulo))
        {
            // a cartela vive ANTES do load: depois dele este objeto não existe mais
            for (float t = 0f; t < 0.5f; t += Time.deltaTime) { textoAlfa = t / 0.5f; yield return null; }
            textoAlfa = 1f;
            yield return new WaitForSeconds(tempoDeCartela);
            for (float t = 0f; t < 0.5f; t += Time.deltaTime) { textoAlfa = 1f - t / 0.5f; yield return null; }
            textoAlfa = 0f;
        }

        RunState.Transicionando = true;      // a próxima cena nasce preta
        SceneManager.LoadScene(cena);
    }

    IEnumerator Entrar()
    {
        EmTransicao = true;
        Travar(true);

        yield return new WaitForSeconds(0.3f);   // um instante de preto antes de abrir

        for (float t = 0f; t < tempoDeEntrada; t += Time.deltaTime)
        {
            preto = 1f - Mathf.Clamp01(t / tempoDeEntrada);
            yield return null;
        }
        preto = 0f;

        Travar(false);
        EmTransicao = false;
    }

    void Travar(bool v)
    {
        var sol = FindFirstObjectByType<SoldierController>();
        if (sol != null) sol.Travado = v;
    }

    void OnGUI()
    {
        if (preto <= 0.001f) return;

        GUI.depth = -1000;      // por cima de todo o resto, inclusive do HUD

        var cor = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, preto);
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);

        if (textoAlfa > 0.001f)
        {
            var est = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = tamanhoDoTitulo
            };
            est.normal.textColor = new Color(0.88f, 0.86f, 0.82f, textoAlfa);
            GUI.color = Color.white;
            GUI.Label(new Rect(0f, Screen.height * 0.42f, Screen.width, 60f), titulo, est);

            if (!string.IsNullOrEmpty(legenda))
            {
                var sub = new GUIStyle(est) { fontSize = tamanhoDaLegenda };
                sub.normal.textColor = new Color(0.62f, 0.60f, 0.57f, textoAlfa);
                GUI.Label(new Rect(0f, Screen.height * 0.42f + 58f, Screen.width, 40f), legenda, sub);
            }
        }

        GUI.color = cor;
    }
}
