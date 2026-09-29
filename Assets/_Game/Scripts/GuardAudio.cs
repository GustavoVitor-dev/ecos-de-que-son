using UnityEngine;

/// O guarda soa, em 3D.
/// No escuro isso e o que devolve ao jogador a informacao que a falta de luz tirou:
/// da pra rastrear a patrulha de ouvido, sem ver nada.
/// Sem isso, escuridao vira injustica em vez de tensao.
[RequireComponent(typeof(AudioSource))]
public class GuardAudio : MonoBehaviour
{
    public GuardAI guard;

    [Header("Passos")]
    public float metersPerStep = 0.85f;
    public float stepVolume = 0.5f;

    [Header("Reacao")]
    [Tooltip("Grunhido ao passar pra Investigando. E o aviso de que ele te ouviu.")]
    public float gruntVolume = 0.8f;

    AudioSource src;
    AudioClip[] steps;
    AudioClip grunt;
    Vector3 lastPos;
    float travelled;
    int next;
    GuardAI.State lastState;

    void Awake()
    {
        src = GetComponent<AudioSource>();
        src.playOnAwake = false;
        src.spatialBlend = 1f;                       // 3D: e disso que a leitura depende
        src.rolloffMode = AudioRolloffMode.Linear;
        src.minDistance = 2f;
        src.maxDistance = 26f;

        if (guard == null) guard = GetComponentInParent<GuardAI>();

        steps = new AudioClip[3];
        for (int i = 0; i < steps.Length; i++)
            steps[i] = ProtoAudio.Footstep("passo_guarda_" + i, 0.7f + 0.1f * i, 0.19f, 31 + i * 5);
        grunt = ProtoAudio.Grunt("grunhido", 118f);

        lastPos = transform.position;
        if (guard != null) lastState = guard.CurrentState;
    }

    void Update()
    {
        if (guard == null) return;

        travelled += Vector3.Distance(transform.position, lastPos);
        lastPos = transform.position;

        if (travelled >= metersPerStep)
        {
            travelled = 0f;
            src.pitch = Random.Range(0.92f, 1.08f);
            src.PlayOneShot(steps[next], stepVolume);
            next = (next + 1) % steps.Length;
        }

        if (guard.CurrentState != lastState)
        {
            if (guard.CurrentState == GuardAI.State.Investigando)
            {
                src.pitch = 1f;
                src.PlayOneShot(grunt, gruntVolume);
            }
            lastState = guard.CurrentState;
        }
    }
}
