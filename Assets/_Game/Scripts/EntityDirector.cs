using UnityEngine;
using UnityEngine.InputSystem;

/// Decide QUANDO a entidade aparece.
///
/// No jogo final isso vai ser roteirizado no beat sheet (aproximadamente
/// 1:40, 4:00 e 6:30). Enquanto o nivel nao existe, dispara por estresse
/// e por tecla, pra poder ser testado.
///
/// Regra de escopo: uma aparicao por vez, e nunca duas seguidas do mesmo tipo.
/// Repeticao mata medo mais rapido que qualquer outra coisa.
public class EntityDirector : MonoBehaviour
{
    public EntityPresence entity;

    [Header("Disparo automatico por estresse")]
    public bool autoByStress = true;
    [Tooltip("A primeira aparicao acontece quando o estresse cruza este valor.")]
    public float firstAt = 35f;
    [Tooltip("Segundos minimos entre duas aparicoes automaticas.")]
    public float cooldown = 25f;

    [Header("Teclas de teste")]
    public bool debugKeys = true;

    public int Count { get; private set; }

    StressSystem stress;
    float cooldownTimer;
    bool armed = true;

    void Start()
    {
        stress = StressSystem.Instance;
        if (entity == null) entity = FindFirstObjectByType<EntityPresence>();
    }

    void Update()
    {
        if (entity == null) return;

        if (debugKeys && Keyboard.current != null)
        {
            if (Keyboard.current.digit1Key.wasPressedThisFrame) Trigger(EntityPresence.Manifestation.Periferica);
            if (Keyboard.current.digit2Key.wasPressedThisFrame) Trigger(EntityPresence.Manifestation.Confirmada);
            if (Keyboard.current.digit3Key.wasPressedThisFrame) Trigger(EntityPresence.Manifestation.EntreEles);
        }

        if (!autoByStress) return;
        if (stress == null) stress = StressSystem.Instance;
        if (stress == null) return;

        if (cooldownTimer > 0f) { cooldownTimer -= Time.deltaTime; return; }

        if (armed && stress.Value >= firstAt)
        {
            armed = false;
            Trigger(NextInSequence());
        }
        else if (!armed && stress.Value >= firstAt)
        {
            Trigger(NextInSequence());
        }
    }

    /// A ordem importa: cada aparicao so funciona depois da anterior ter acontecido.
    EntityPresence.Manifestation NextInSequence()
    {
        switch (Count)
        {
            case 0: return EntityPresence.Manifestation.Periferica;
            case 1: return EntityPresence.Manifestation.Confirmada;
            default: return EntityPresence.Manifestation.EntreEles;
        }
    }

    public void Trigger(EntityPresence.Manifestation kind) => Trigger(kind, null);

    public void Trigger(EntityPresence.Manifestation kind, Transform ancora)
    {
        if (entity == null || entity.Visible) return;

        entity.Manifest(kind, ancora);
        Count++;
        cooldownTimer = cooldown;
        Debug.Log($"[ENTIDADE] aparicao {Count}: {kind}");

        Ensinar();
    }

    /// A regra dela estava implementada e invisivel: o jogo nunca contava que
    /// ela SO muda de estado dentro da lacuna. Regra que o jogador nao percebe
    /// nao e regra, e coincidencia — e coincidencia le como bug.
    ///
    /// Duas frases, uma em cada uma das duas primeiras aparicoes. Depois disso
    /// o jogo cala a boca e deixa a regra trabalhar sozinha.
    void Ensinar()
    {
        if (MissionDirector.Instance == null) return;

        if (Count == 1)
            MissionDirector.Instance.SetStatus("ELA NAO ESTAVA AI ANTES DE VOCE PISCAR.");
        else if (Count == 2)
            MissionDirector.Instance.SetStatus("VOCE NUNCA A VE CHEGAR. SO CONSTATA QUE CHEGOU.");
    }
}
