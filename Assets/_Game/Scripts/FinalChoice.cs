using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

/// A decisao final: responder o radio, ou desligar.
///
/// Um botao so, uma mecanica ensinada no primeiro minuto, sem arvore de dialogo.
/// E ela fecha o tema com a frase que ja estava no proprio pitch, na transmissao
/// interceptada: "NAO RESPONDA."
///
/// A coisa aprende vozes. Responder entrega a sua.
/// Desligar significa sair sem saber — e nao saber e o preco de sobreviver.
public class FinalChoice : MonoBehaviour
{
    [Header("Controle")]
    public Key responder = Key.R;
    // NAO usar D nem WASD aqui: dentro da camara o jogador ainda anda, e andar
    // pra direita nao pode encerrar o jogo. Q e a tecla livre mais perto da mao.
    public Key desligar = Key.Q;

    [Header("Tempo")]
    [Tooltip("Segundos de chiado antes da voz chegar.")]
    public float esperaAntes = 3f;
    [Tooltip("Se o jogador nao decidir, o radio insiste. Ele NAO decide por voce.")]
    public float intervaloInsistencia = 12f;

    public bool Ativo { get; private set; }
    public bool Decidido { get; private set; }
    public string Escolha { get; private set; } = "";

    AudioSource src;
    AudioClip chiado, voz;

    void Awake()
    {
        src = GetComponent<AudioSource>();
        if (src == null) src = gameObject.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.spatialBlend = 0f;

        // placeholders: ver radio_static_heavy e radio_voice_wrong_02 em Docs/AUDIO.md
        chiado = ProtoAudio.Drone("radio_chiado", 3f, 55);
        voz = ProtoAudio.Grunt("radio_voz_errada", 96f, 1.4f, 77);
    }

    void OnTriggerEnter(Collider other)
    {
        if (Ativo || Decidido) return;
        if (other.GetComponentInParent<SoldierController>() == null) return;
        StartCoroutine(Sequencia());
    }

    IEnumerator Sequencia()
    {
        Ativo = true;

        var amb = FindFirstObjectByType<Ambience>();
        if (amb != null) amb.Hush(30f);              // o mundo cala. So resta o radio.

        if (chiado != null) { src.clip = chiado; src.loop = true; src.volume = 0.4f; src.Play(); }

        yield return new WaitForSeconds(esperaAntes);

        if (voz != null) src.PlayOneShot(voz, 0.9f);
        if (StressSystem.Instance != null) StressSystem.Instance.Spike(45f);
        if (MissionDirector.Instance != null)
            MissionDirector.Instance.SetStatus("E A VOZ DELE.   [" + Tecla(responder)
                + "] RESPONDER    [" + Tecla(desligar) + "] DESLIGAR");

        float t = 0f;
        while (!Decidido)
        {
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb[responder].wasPressedThisFrame) { Responder(); break; }
                if (kb[desligar].wasPressedThisFrame) { Desligar(); break; }
            }

            t += Time.deltaTime;
            if (t >= intervaloInsistencia)
            {
                t = 0f;
                if (voz != null) src.PlayOneShot(voz, 0.9f);
                if (StressSystem.Instance != null) StressSystem.Instance.Spike(8f);
            }
            yield return null;
        }
    }

    // o texto do HUD sai das proprias teclas, pra nunca mentir se elas mudarem
    static string Tecla(Key k) => k.ToString().ToUpperInvariant();

    void Responder()
    {
        Decidido = true; Escolha = "RESPONDEU";
        src.Stop();

        Debug.Log("[FINAL] o jogador respondeu. Ela tem a voz dele agora.");
        if (MissionDirector.Instance != null)
            MissionDirector.Instance.SetStatus("VOCE RESPONDEU.");

        var blink = FindFirstObjectByType<BlinkSystem>();
        if (blink != null) blink.ForceBlink();

        var dir = FindFirstObjectByType<EntityDirector>();
        if (dir != null) dir.Trigger(EntityPresence.Manifestation.EntreEles);

        if (StressSystem.Instance != null) StressSystem.Instance.Spike(60f);
        StartCoroutine(Epilogo("AGORA ELA SABE FALAR COMO VOCE."));
    }

    void Desligar()
    {
        Decidido = true; Escolha = "DESLIGOU";
        src.Stop();

        Debug.Log("[FINAL] o jogador desligou. Sai vivo, sem saber.");
        if (MissionDirector.Instance != null)
            MissionDirector.Instance.SetStatus("VOCE DESLIGOU.");

        StartCoroutine(Epilogo("VOCE NUNCA VAI SABER SE ERA ELE."));
    }

    IEnumerator Epilogo(string texto)
    {
        yield return new WaitForSeconds(3.5f);
        if (MissionDirector.Instance != null) MissionDirector.Instance.SetStatus(texto);
        yield return new WaitForSeconds(4f);
        if (MissionDirector.Instance != null) MissionDirector.Instance.SetStatus("FIM DA VERTICAL SLICE");
        Debug.Log("[FIM] escolha: " + Escolha);
    }

    void OnDrawGizmos()
    {
        var c = GetComponent<Collider>();
        if (c == null) return;
        Gizmos.color = new Color(1f, 0.3f, 0.25f, 0.3f);
        Gizmos.DrawCube(c.bounds.center, c.bounds.size);
    }
}
