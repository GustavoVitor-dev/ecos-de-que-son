using UnityEngine;

/// A missao do pitch, reduzida ao osso:
/// recuperar os documentos, sair vivo.
/// Existe pra o teste ter aposta — sem objetivo, "isso e tenso?" nao tem resposta.
public class MissionDirector : MonoBehaviour
{
    public enum Phase { Documentos, Extracao, Concluida }

    public static MissionDirector Instance { get; private set; }

    public Phase Current { get; private set; } = Phase.Documentos;
    public float Elapsed { get; private set; }
    public string StatusLine { get; private set; } = "RECUPERAR OS DOCUMENTOS";

    void Awake() => Instance = this;

    void Update()
    {
        if (Current != Phase.Concluida) Elapsed += Time.deltaTime;
    }

    public void CollectDocuments()
    {
        if (Current != Phase.Documentos) return;
        Current = Phase.Extracao;
        StatusLine = "SAIR VIVO";
        Debug.Log($"[DOCUMENTOS] recolhidos aos {Elapsed:0.0}s");
    }

    public void Extract()
    {
        if (Current != Phase.Extracao) return;
        Current = Phase.Concluida;
        StatusLine = "MISSAO CONCLUIDA em " + Elapsed.ToString("0.0") + "s";
        Debug.Log($"[EXTRAIDO] missao concluida em {Elapsed:0.0}s");
    }

    /// Escrita direta na linha de objetivo. Usada pelos beats do roteiro.
    public void SetStatus(string text)
    {
        if (!string.IsNullOrEmpty(text)) StatusLine = text;
    }

    /// Ser pego nao reinicia a missao — so a posicao. A cicatriz fica no cronometro.
    public void ResetMission()
    {
        Current = Phase.Documentos;
        StatusLine = "RECUPERAR OS DOCUMENTOS";
        Elapsed = 0f;
    }
}
