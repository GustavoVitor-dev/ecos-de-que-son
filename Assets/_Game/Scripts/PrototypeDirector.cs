using UnityEngine;

/// Estado minimo do prototipo: conta quantas vezes o jogador foi pego e o devolve ao spawn.
/// Sem tela de morte de proposito — reset instantaneo mantem o teste rapido.
public class PrototypeDirector : MonoBehaviour
{
    public static PrototypeDirector Instance { get; private set; }

    public Transform spawnPoint;

    /// Fôlego depois de renascer: guarda nenhum acumula detecção até esta
    /// hora. Sem isso, renascer no campo de visão de alguém era ser pego de
    /// novo antes de conseguir se mexer.
    public static float GracaAte;
    public float segundosDeGraca = 3f;
    public int caughtCount;

    float lastCaught = -99f;

    // estático sobrevive entre sessões de Play (domínio não recarrega) e
    // Time.time volta a zero: sem zerar aqui, os guardas ficavam cegos
    void Awake() { Instance = this; GracaAte = 0f; }

    public static void ReportCaught(GuardAI by)
    {
        if (Instance != null) Instance.Caught(by);
    }

    /// O Miller caiu (a Vida chegou a zero). Antes isto acontecia no instante
    /// em que um guarda te via; agora e o fim de uma briga que voce perdeu.
    public void Morrer()
    {
        if (DevMode.Imortal) return;
        if (Time.time - lastCaught < 1f) return;
        lastCaught = Time.time;
        caughtCount++;

        var onde = FindFirstObjectByType<SoldierController>();
        Debug.Log($"[MORREU] com o jogador em {(onde != null ? onde.transform.position.ToString("F0") : "?")} " +
                  $"aos {Time.timeSinceLevelLoad:0.0}s — total: {caughtCount}");
        Renascer();
    }

    void Caught(GuardAI by)
    {
        if (DevMode.Imortal) return;
        if (Time.time - lastCaught < 1f) return;
        lastCaught = Time.time;
        caughtCount++;

        var quem = FindFirstObjectByType<SoldierController>();
        Debug.Log($"[PEGO] por {by.name} (em {by.transform.position:F0}, estado {by.CurrentState}) com o jogador em " +
                  $"{(quem != null ? quem.transform.position.ToString("F0") : "?")} aos {Time.timeSinceLevelLoad:0.0}s — total: {caughtCount}");
        Renascer();
    }

    /// Volta o jogador pro checkpoint e devolve os guardas pra ronda.
    void Renascer()
    {

        // ser pego deixa marca: a proxima tentativa comeca pior
        if (StressSystem.Instance != null)
            StressSystem.Instance.Spike(StressSystem.Instance.spikeOnCaught);

        var pc = FindFirstObjectByType<SoldierController>();
        if (pc != null && spawnPoint != null)
        {
            var cc = pc.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            pc.transform.SetPositionAndRotation(spawnPoint.position, spawnPoint.rotation);
            if (cc != null) cc.enabled = true;
            // teleporte não dispara OnTriggerExit: sem isto renascia com o mato na tela
            var oc = pc.GetComponent<PlayerConcealment>();
            if (oc != null) oc.Limpar();
        }

        foreach (var g in FindObjectsByType<GuardAI>(FindObjectsSortMode.None))
            g.ResetToPatrol();
        GracaAte = Time.time + segundosDeGraca;
    }
}
