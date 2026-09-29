using UnityEngine;

/// Falsos positivos calibrados.
///
/// A regra que faz isso funcionar: o passo falso usa EXATAMENTE os mesmos
/// parametros de sintese do passo do guarda. Byte por byte, o mesmo som.
/// Se desse pra diferenciar, a alucinacao nao contaminaria nada e o jogador
/// simplesmente aprenderia a ignora-la — e o sistema inteiro perderia a funcao.
///
/// Som antes de imagem, de proposito: audio duvidoso assusta, visual duvidoso
/// parece bug.
public class Hallucination : MonoBehaviour
{
    [Header("Segundos entre eventos, por faixa de estresse")]
    public float intervalAlerta = 26f;
    public float intervalRuptura = 13f;
    public float intervalHiper = 6f;
    [Range(0f, 0.8f)] public float jitter = 0.4f;

    [Header("Posicionamento")]
    public float minDistance = 6f;
    public float maxDistance = 14f;

    [Header("Volumes")]
    public float stepVolume = 0.5f;
    public float gruntVolume = 0.7f;

    StressSystem stress;
    Transform player;
    Ambience ambience;
    AudioSource src;
    AudioClip[] falseSteps;
    AudioClip falseGrunt;
    float timer;
    int lastKind = -1;

    void Start()
    {
        stress = StressSystem.Instance;
        ambience = FindFirstObjectByType<Ambience>();

        var soldier = FindFirstObjectByType<SoldierController>();
        if (soldier != null) player = soldier.transform;

        var go = new GameObject("FonteAlucinacao");
        go.transform.SetParent(transform, false);
        src = go.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.spatialBlend = 1f;
        src.rolloffMode = AudioRolloffMode.Linear;
        src.minDistance = 2f;
        src.maxDistance = 26f;

        // MESMOS parametros do GuardAudio. E isso que torna o falso indistinguivel.
        falseSteps = new AudioClip[3];
        for (int i = 0; i < falseSteps.Length; i++)
            falseSteps[i] = ProtoAudio.Footstep("passo_guarda_" + i, 0.7f + 0.1f * i, 0.19f, 31 + i * 5);
        falseGrunt = ProtoAudio.Grunt("grunhido", 118f);

        Schedule();
    }

    void Update()
    {
        if (stress == null) stress = StressSystem.Instance;
        if (stress == null || player == null) return;

        float interval = IntervalForTier();
        if (interval <= 0f) return;           // Calmo: o mundo nao mente

        timer -= Time.deltaTime;
        if (timer > 0f) return;

        Fire();
        Schedule();
    }

    float IntervalForTier()
    {
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

    void Fire()
    {
        int kind;
        do { kind = Random.Range(0, 3); } while (kind == lastKind);
        lastKind = kind;

        src.transform.position = RandomPointAround();

        switch (kind)
        {
            case 0:   // passos que nao sao de ninguem
                StartCoroutine(FalseSteps());
                break;

            case 1:   // grunhido de um guarda que nao esta la
                src.pitch = Random.Range(0.95f, 1.05f);
                src.PlayOneShot(falseGrunt, gruntVolume);
                break;

            case 2:   // o mundo emudece por um instante
                if (ambience != null) ambience.Hush(Random.Range(1.5f, 3f));
                break;
        }
    }

    System.Collections.IEnumerator FalseSteps()
    {
        int count = Random.Range(2, 5);
        for (int i = 0; i < count; i++)
        {
            src.pitch = Random.Range(0.92f, 1.08f);
            src.PlayOneShot(falseSteps[i % falseSteps.Length], stepVolume);
            yield return new WaitForSeconds(Random.Range(0.42f, 0.58f));
        }
    }

    Vector3 RandomPointAround()
    {
        float angle = Random.Range(0f, Mathf.PI * 2f);
        float dist = Random.Range(minDistance, maxDistance);
        return player.position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * dist;
    }
}
