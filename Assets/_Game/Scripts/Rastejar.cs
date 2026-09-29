using UnityEngine;

/// Faz um corpo humanoide rastejar de bruços na direção do jogador,
/// sem animação: os ossos são posicionados à mão a cada frame.
///
/// Existe porque nenhum pacote do projeto tem animação de rastejar — o Skinless
/// Zombie só traz idle e raiva, e a Universal Animation Library grátis também
/// não tem (rastejar só vem na versão Pro). O corpo precisava ser uma pessoa
/// destroçada se arrastando, não um cadáver parado.
///
/// O ciclo: um braço estica à frente e crava a mão no chão; o corpo é puxado
/// até aquela mão; o outro braço repete. As pernas não fazem nada — arrastam.
/// A cabeça fica erguida, olhando pra quem está vindo.
[DisallowMultipleComponent]
public class Rastejar : MonoBehaviour
{
    [Header("Movimento")]
    [Tooltip("Velocidade média de arrasto, em m/s. Uma pessoa ferida mal passa de 0,3.")]
    public float velocidade = 0.3f;
    [Tooltip("Puxadas por segundo, somando os dois braços.")]
    public float ritmo = 0.75f;
    [Tooltip("Para de avançar quando a cabeça chega a esta distância do jogador.")]
    public float pararA = 1.3f;
    [Tooltip("Graus por segundo que ele consegue virar o corpo.")]
    public float giro = 50f;

    [Header("Postura")]
    public float alturaDoCorpo = 0.16f;
    public float alcance = 0.62f;

    Animator anim;
    Transform alvo;
    Transform cabeca, pescoco;
    readonly Transform[] braco = new Transform[2], antebraco = new Transform[2], mao = new Transform[2];
    Quaternion[] pose;
    Transform[] ossos;
    readonly Vector3[] maoCravada = new Vector3[2];
    readonly bool[] cravada = new bool[2];
    float fase;
    float comprimento = 1.7f;

    void Start()
    {
        anim = GetComponentInChildren<Animator>();
        if (anim == null || anim.avatar == null || !anim.avatar.isHuman) { enabled = false; return; }

        cabeca = anim.GetBoneTransform(HumanBodyBones.Head);
        pescoco = anim.GetBoneTransform(HumanBodyBones.Neck);
        braco[0] = anim.GetBoneTransform(HumanBodyBones.LeftUpperArm);
        antebraco[0] = anim.GetBoneTransform(HumanBodyBones.LeftLowerArm);
        mao[0] = anim.GetBoneTransform(HumanBodyBones.LeftHand);
        braco[1] = anim.GetBoneTransform(HumanBodyBones.RightUpperArm);
        antebraco[1] = anim.GetBoneTransform(HumanBodyBones.RightLowerArm);
        mao[1] = anim.GetBoneTransform(HumanBodyBones.RightHand);

        // Animator desligado deixa os ossos na pose de ligação; guardo ela pra
        // voltar a cada frame. Sem isso as rotações se acumulam e o corpo entorta.
        anim.enabled = false;
        ossos = new[] { braco[0], antebraco[0], mao[0], braco[1], antebraco[1], mao[1], cabeca, pescoco };
        pose = new Quaternion[ossos.Length];
        for (int i = 0; i < ossos.Length; i++) if (ossos[i] != null) pose[i] = ossos[i].localRotation;

        if (cabeca != null) comprimento = Mathf.Max(0.5f, (cabeca.position - transform.position).magnitude);

        var jog = FindFirstObjectByType<SoldierController>();
        if (jog != null) alvo = jog.transform;

        Deitar(Plano(transform.up));
    }

    static Vector3 Plano(Vector3 v)
    {
        v.y = 0f;
        return v.sqrMagnitude < 0.0001f ? Vector3.forward : v.normalized;
    }

    // de bruços: o "para cima" do corpo aponta pra frente do rastejo, a barriga pro chão
    void Deitar(Vector3 dir)
    {
        transform.rotation = Quaternion.LookRotation(Vector3.down, dir);
    }

    static float Chao(Vector3 p)
    {
        return Physics.Raycast(p + Vector3.up * 1.5f, Vector3.down, out RaycastHit h, 4f, ~0, QueryTriggerInteraction.Ignore)
            ? h.point.y : p.y;
    }

    void LateUpdate()
    {
        for (int i = 0; i < ossos.Length; i++) if (ossos[i] != null) ossos[i].localRotation = pose[i];

        Vector3 dir = Plano(transform.up);
        Vector3 ponta = transform.position + dir * comprimento;

        // vira devagar pra quem está vindo
        bool avancar = true;
        if (alvo != null)
        {
            Vector3 para = Plano(alvo.position - ponta);
            dir = Vector3.RotateTowards(dir, para, giro * Mathf.Deg2Rad * Time.deltaTime, 0f);
            Vector2 a = new Vector2(ponta.x, ponta.z), b = new Vector2(alvo.position.x, alvo.position.z);
            avancar = Vector2.Distance(a, b) > pararA;
        }
        Deitar(dir);
        Vector3 lado = Vector3.Cross(Vector3.up, dir);

        fase += Time.deltaTime * ritmo;

        for (int k = 0; k < 2; k++)
        {
            if (braco[k] == null) continue;
            float u = Mathf.Repeat(fase + k * 0.5f, 1f);
            Vector3 ombro = braco[k].position;
            float sinal = k == 0 ? -1f : 1f;
            float chao = Chao(ombro);

            if (u < 0.45f)
            {
                // estica: a mão sobe um pouco e vai pra frente
                cravada[k] = false;
                float e = Mathf.SmoothStep(0f, 1f, u / 0.45f);
                Vector3 t = ombro + dir * Mathf.Lerp(0.1f, alcance, e) + lado * sinal * 0.2f;
                t.y = chao + 0.04f + Mathf.Sin(e * Mathf.PI) * 0.18f;
                IK(k, t, ombro + Vector3.up * 0.5f + lado * sinal * 0.4f);
            }
            else
            {
                // crava e puxa: a mão fica presa no chão enquanto o corpo vem
                if (!cravada[k])
                {
                    cravada[k] = true;
                    maoCravada[k] = ombro + dir * alcance + lado * sinal * 0.2f;
                    maoCravada[k].y = chao + 0.03f;
                }
                IK(k, maoCravada[k], ombro + Vector3.up * 0.6f + lado * sinal * 0.45f);
            }
        }

        if (avancar)
        {
            // o corpo só vem quando um braço está puxando: arranco, não deslize
            float puxa = Mathf.Abs(Mathf.Sin(fase * Mathf.PI * 2f));
            Vector3 p = transform.position + dir * velocidade * (0.25f + 1.5f * puxa) * Time.deltaTime;
            p.y = Chao(p + dir * comprimento * 0.5f) + alturaDoCorpo;
            transform.position = p;
        }

        // cabeça erguida, virada pra quem está vindo
        // Cabeça erguida. A primeira versão girava em torno de um eixo com sinal
        // errado e enfiava a cara no chão. O eixo local de osso de rig do Blender
        // não é confiável como "rosto"; a direção pescoço→cabeça é. Ela aponta pra
        // frente do rastejo; erguer é girá-la na direção de cima.
        if (pescoco != null && cabeca != null)
        {
            Vector3 v = cabeca.position - pescoco.position;
            Vector3 eixo = Vector3.Cross(v, Vector3.up);
            if (eixo.sqrMagnitude > 0.0001f)
                pescoco.rotation = Quaternion.AngleAxis(42f, eixo.normalized) * pescoco.rotation;

            if (alvo != null)
            {
                // e vira pro lado de quem está vindo, só em volta da vertical
                Vector3 plano = cabeca.position - pescoco.position; plano.y = 0f;
                Vector3 para = alvo.position - cabeca.position; para.y = 0f;
                float ang = Mathf.Clamp(Vector3.SignedAngle(plano, para, Vector3.up), -50f, 50f);
                pescoco.rotation = Quaternion.AngleAxis(ang * 0.7f, Vector3.up) * pescoco.rotation;
            }
        }
    }

    // IK de dois ossos: ombro-cotovelo-mão, com o cotovelo pro lado do polo
    void IK(int k, Vector3 alvoMao, Vector3 polo)
    {
        Transform a = braco[k], b = antebraco[k], c = mao[k];
        if (a == null || b == null || c == null) return;
        float la = (b.position - a.position).magnitude, lb = (c.position - b.position).magnitude;
        Vector3 paraAlvo = alvoMao - a.position;
        float d = Mathf.Clamp(paraAlvo.magnitude, 0.05f, la + lb - 0.005f);
        Vector3 dirT = paraAlvo.normalized;

        float cosA = (la * la + d * d - lb * lb) / (2f * la * d);
        float ang = Mathf.Acos(Mathf.Clamp(cosA, -1f, 1f)) * Mathf.Rad2Deg;
        Vector3 eixo = Vector3.Cross(dirT, polo - a.position);
        if (eixo.sqrMagnitude < 0.0001f) eixo = Vector3.Cross(dirT, Vector3.up);
        Vector3 cotovelo = a.position + (Quaternion.AngleAxis(ang, eixo.normalized) * dirT) * la;

        a.rotation = Quaternion.FromToRotation(b.position - a.position, cotovelo - a.position) * a.rotation;
        b.rotation = Quaternion.FromToRotation(c.position - b.position, alvoMao - b.position) * b.rotation;
    }
}
