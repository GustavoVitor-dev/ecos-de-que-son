using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// Corpo morto: um ragdoll que é içado mole pela corda, tomba e cai uma vez só.
///
/// A primeira versão espernava no chão feito gente viva. Três causas, todas
/// reais, medidas e corrigidas aqui:
///
/// 1. As cápsulas eram montadas no eixo Y local do osso. Rig nenhum garante
///    isso — em vários ossos a cápsula ficava ATRAVESSADA no membro, sobrando
///    pra fora e batendo em tudo. Agora o eixo sai da direção do osso filho.
/// 2. Os pedaços do próprio corpo colidiam entre si. As duas coxas nascem
///    encavaladas no quadril: a física empurrava uma contra a outra todo
///    quadro, pra sempre. Agora o corpo ignora a si mesmo.
/// 3. As juntas esticavam sob o próprio peso e voltavam com tranco. Agora
///    projetam de volta (enableProjection).
///
/// Some-se a isso: nada de torque aleatório, velocidade angular com teto, e
/// congelamento assim que assenta. Corpo morto bate uma vez e fica.
[DisallowMultipleComponent]
public class Cadaver : MonoBehaviour
{
    [Tooltip("Massa total distribuída pelos ossos.")]
    public float massa = 62f;
    [Tooltip("Abaixo desta velocidade por 0,35 s, o corpo é considerado assentado.")]
    public float paradoAbaixoDe = 0.1f;
    [Tooltip("Corpo cortado na cintura: não monta osso de perna nenhum.")]
    public bool semPernas;

    readonly List<Rigidbody> pedacos = new List<Rigidbody>();
    readonly List<Collider> cascas = new List<Collider>();
    Animator an;
    Transform quadril, peito;
    Rigidbody quadrilRb, peitoRb;
    bool solto, pendurado, bateu, comPeito;
    Vector3 gancho, ganchoAnterior, ganchoPeito;
    static AudioClip baque;

    public bool Assentado { get; private set; }
    public bool Solto => solto;

    void Awake()
    {
        // IgnoreCollision é estado de runtime: não vem salvo da cena, tem que
        // ser refeito toda vez que o jogo começa.
        if (pedacos.Count == 0) Montar();
    }

    /// Monta colisores e juntas. Serve no editor (montagem da cena) e no
    /// runtime; nasce travado (cinemático).
    public void Montar()
    {
        an = GetComponentInChildren<Animator>();
        if (an == null || !an.isHuman) return;
        pedacos.Clear();
        cascas.Clear();

        Transform O(HumanBodyBones b) => an.GetBoneTransform(b);

        quadril = O(HumanBodyBones.Hips);
        if (quadril == null) return;

        peito = O(HumanBodyBones.Chest) ?? O(HumanBodyBones.UpperChest);
        var espinha = O(HumanBodyBones.Spine);
        var cabeca = O(HumanBodyBones.Head);

        // tronco: cada pedaço aponta pro osso seguinte
        Pedaco(quadril, espinha, 0.17f, massa * 0.15f);
        Pedaco(espinha, peito, 0.16f, massa * 0.13f);
        Pedaco(peito, cabeca, 0.17f, massa * 0.16f);
        Pedaco(cabeca, null, 0.1f, massa * 0.08f);

        var bracos = new[]
        {
            (O(HumanBodyBones.LeftUpperArm), O(HumanBodyBones.LeftLowerArm)),
            (O(HumanBodyBones.LeftLowerArm), O(HumanBodyBones.LeftHand)),
            (O(HumanBodyBones.RightUpperArm), O(HumanBodyBones.RightLowerArm)),
            (O(HumanBodyBones.RightLowerArm), O(HumanBodyBones.RightHand)),
        };
        foreach (var (t, filho) in bracos) Pedaco(t, filho, 0f, massa * 0.06f);

        if (!semPernas)
        {
            var pernas = new[]
            {
                (O(HumanBodyBones.LeftUpperLeg), O(HumanBodyBones.LeftLowerLeg)),
                (O(HumanBodyBones.LeftLowerLeg), O(HumanBodyBones.LeftFoot)),
                (O(HumanBodyBones.RightUpperLeg), O(HumanBodyBones.RightLowerLeg)),
                (O(HumanBodyBones.RightLowerLeg), O(HumanBodyBones.RightFoot)),
            };
            foreach (var (t, filho) in pernas) Pedaco(t, filho, 0f, massa * 0.06f);
        }

        // as tripas entram na mesma lista dos ossos: ignoram o corpo, caem
        // junto e congelam junto
        foreach (var v in GetComponentsInChildren<Viscera>(true))
        {
            var rbv = v.GetComponent<Rigidbody>();
            var colv = v.GetComponent<Collider>();
            if (rbv != null && !pedacos.Contains(rbv))
            {
                rbv.maxAngularVelocity = 4f;
                rbv.interpolation = RigidbodyInterpolation.Interpolate;
                rbv.sleepThreshold = 0.06f;
                pedacos.Add(rbv);
            }
            if (colv != null && !cascas.Contains(colv)) cascas.Add(colv);
        }

        // juntas: cada pedaço pendurado no pedaço ancestral mais próximo
        foreach (var rb in pedacos)
        {
            if (rb.transform == quadril) continue;
            var pai = Acima(rb.transform);
            if (pai == null) continue;
            var j = rb.GetComponent<CharacterJoint>();
            if (j == null) j = rb.gameObject.AddComponent<CharacterJoint>();
            j.connectedBody = pai;
            j.enablePreprocessing = false;
            j.enableProjection = true;          // junta esticada volta sem tranco
            j.projectionDistance = 0.04f;
            j.projectionAngle = 12f;
            j.lowTwistLimit = new SoftJointLimit { limit = -18f };
            j.highTwistLimit = new SoftJointLimit { limit = 18f };
            j.swing1Limit = new SoftJointLimit { limit = 42f };
            j.swing2Limit = new SoftJointLimit { limit = 26f };
        }

        quadrilRb = quadril.GetComponent<Rigidbody>();
        peitoRb = peito != null ? peito.GetComponent<Rigidbody>() : null;
        IgnorarASiMesmo();
        Travar(true);
    }

    /// Cápsula no eixo REAL do osso: a direção sai do osso filho, em espaço
    /// local, que é onde height/radius/center do CapsuleCollider vivem.
    void Pedaco(Transform t, Transform filho, float grossura, float peso)
    {
        if (t == null) return;

        Vector3 osso = filho != null
            ? t.InverseTransformPoint(filho.position)
            : Vector3.up * 0.12f;
        float comp = Mathf.Max(0.08f, osso.magnitude);

        int eixo = 0;
        float maior = Mathf.Abs(osso.x);
        if (Mathf.Abs(osso.y) > maior) { eixo = 1; maior = Mathf.Abs(osso.y); }
        if (Mathf.Abs(osso.z) > maior) { eixo = 2; }

        var cap = t.GetComponent<CapsuleCollider>();
        if (cap == null) cap = t.gameObject.AddComponent<CapsuleCollider>();
        cap.direction = eixo;
        cap.radius = grossura > 0f ? grossura : Mathf.Clamp(comp * 0.24f, 0.05f, 0.12f);
        cap.height = Mathf.Max(comp, cap.radius * 2.05f);
        var centro = Vector3.zero;
        centro[eixo] = osso[eixo] * 0.5f;
        cap.center = centro;
        cascas.Add(cap);

        var rb = t.GetComponent<Rigidbody>();
        if (rb == null) rb = t.gameObject.AddComponent<Rigidbody>();
        rb.mass = Mathf.Max(0.5f, peso);
        rb.linearDamping = 0.7f;                // corpo morto não escorrega
        rb.angularDamping = 2.2f;               // nem roda solto
        rb.maxAngularVelocity = 4f;             // teto: sem hélice
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        rb.solverIterations = 16;
        rb.solverVelocityIterations = 8;
        rb.sleepThreshold = 0.06f;              // dorme cedo: corpo parado é corpo parado
        pedacos.Add(rb);
    }

    /// O corpo não briga consigo mesmo. As coxas nascem encavaladas no quadril
    /// e os braços no peito: sem isto a física empurra pedaço contra pedaço
    /// todo quadro e o cadáver esperneia pra sempre.
    void IgnorarASiMesmo()
    {
        if (!Application.isPlaying) return;
        for (int i = 0; i < cascas.Count; i++)
            for (int k = i + 1; k < cascas.Count; k++)
                if (cascas[i] != null && cascas[k] != null)
                    Physics.IgnoreCollision(cascas[i], cascas[k], true);
    }

    /// O corpo também não briga com o cenário que ele atravessa de propósito
    /// (o poço, por onde ele sobe).
    public void Ignorar(Collider outro)
    {
        if (!Application.isPlaying || outro == null) return;
        if (pedacos.Count == 0) Montar();
        foreach (var c in cascas) if (c != null) Physics.IgnoreCollision(c, outro, true);
    }

    Rigidbody Acima(Transform t)
    {
        for (var p = t.parent; p != null; p = p.parent)
        {
            var rb = p.GetComponent<Rigidbody>();
            if (rb != null && pedacos.Contains(rb)) return rb;
        }
        return null;
    }

    public void Travar(bool travado)
    {
        foreach (var rb in pedacos)
        {
            if (rb == null) continue;
            rb.isKinematic = travado;
            if (!travado) rb.WakeUp();
        }
    }

    void Cascas(bool ligadas)
    {
        foreach (var c in cascas) if (c != null) c.enabled = ligadas;
    }

    /// Içado pela corda: o quadril segue o gancho e o resto pende mole.
    /// Os colisores ficam DESLIGADOS enquanto sobe — dentro do poço ele
    /// encostaria no colisor da boca e sairia chutando.
    public void Pendurar(Vector3 ponto)
    {
        if (pedacos.Count == 0) Montar();
        if (quadrilRb == null) return;
        if (an != null) an.enabled = false;         // a física manda agora
        Cascas(false);
        Travar(false);
        quadrilRb.isKinematic = true;
        gancho = ganchoAnterior = ponto;
        pendurado = true;
        Assentado = false;
    }

    public void MoverGancho(Vector3 ponto) => gancho = ponto;

    /// Tomba o corpo pra HORIZONTAL antes de largar.
    ///
    /// Solto ainda pendurado em pé, ele desabava de cima pra baixo como quem
    /// desmaia: 4,5 s se dobrando e quase 1 m de deriva — chegou a parar em
    /// cima das pedras da boca. Segurando quadril e peito nos dois lados do
    /// ponto de queda, ele sai da corda já deitado, cai reto e bate uma vez.
    /// Os colisores voltam aqui, e não no solto: assim braços e pernas
    /// encostam no chão em vez de atravessá-lo e pipocar depois.
    public IEnumerator Deitar(Vector3 centroFinal, Vector3 direcaoFinal, float tempo)
    {
        if (quadrilRb == null) yield break;
        if (peitoRb == null) peitoRb = peito != null ? peito.GetComponent<Rigidbody>() : null;
        if (peitoRb == null) yield break;

        Cascas(true);
        quadrilRb.isKinematic = true;
        peitoRb.isKinematic = true;
        comPeito = true;

        // O tronco é RÍGIDO: quadril e peito giram juntos mantendo a distância.
        // Interpolar os dois em linha reta encurtava essa distância no meio do
        // caminho (corda contra arco), a junta esticava e voltava com tranco —
        // era isso que fazia o início da queda parecer quebrado.
        Vector3 c0 = (quadrilRb.position + peitoRb.position) * 0.5f;
        Vector3 eixo = peitoRb.position - quadrilRb.position;
        float meio = eixo.magnitude * 0.5f;
        Vector3 d0 = eixo.sqrMagnitude > 1e-6f ? eixo.normalized : Vector3.down;
        Vector3 d1 = direcaoFinal.sqrMagnitude > 1e-6f ? direcaoFinal.normalized : Vector3.forward;

        for (float t = 0f; t < tempo; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / tempo);
            Vector3 c = Vector3.Lerp(c0, centroFinal, k);
            Vector3 d = Vector3.Slerp(d0, d1, k);
            gancho = c - d * meio;
            ganchoPeito = c + d * meio;
            yield return null;
        }
        gancho = centroFinal - d1 * meio;
        ganchoPeito = centroFinal + d1 * meio;
    }

    void FixedUpdate()
    {
        if (!pendurado) return;
        if (quadrilRb != null) quadrilRb.MovePosition(gancho);
        if (comPeito && peitoRb != null) peitoRb.MovePosition(ganchoPeito);
        ganchoAnterior = gancho;
    }

    /// A corda solta. Daqui pro chão é queda livre, uma vez só.
    public void Soltar(Vector3 empurrao)
    {
        if (pedacos.Count == 0) Montar();
        if (an != null) an.enabled = false;
        pendurado = false;
        comPeito = false;
        Cascas(true);
        Travar(false);
        solto = true;
        bateu = false;
        Assentado = false;

        // VelocityChange, não Impulse por massa: com impulso o corpo era
        // ARREMESSADO 3 m em vez de tombar da borda. E nada de torque
        // aleatório — era ele que fazia o cadáver rodar no ar.
        foreach (var rb in pedacos)
        {
            if (rb == null) continue;
            rb.linearVelocity = empurrao;
            rb.angularVelocity = Vector3.zero;
            var b = rb.gameObject.GetComponent<BatidaDoCorpo>();
            if (b == null) b = rb.gameObject.AddComponent<BatidaDoCorpo>();
            b.aoBater = Bateu;
        }
        StartCoroutine(EsperarAssentar());
    }

    /// Primeiro contato com o chão: um baque seco e nada mais.
    ///
    /// O impacto também MATA a inércia. Medido três vezes: solto a 1,5 m do
    /// poço, o corpo escorregava no barro e parava ora a 2,5 m, ora a 0,7 m —
    /// essa última em cima das pedras da boca, atravessando a pedra. Corpo
    /// morto não desliza: bate, afunda no barro e fica onde bateu.
    void Bateu(Vector3 onde)
    {
        if (bateu) return;
        bateu = true;
        foreach (var rb in pedacos)
        {
            if (rb == null || rb.isKinematic) continue;
            rb.linearVelocity *= 0.18f;
            rb.angularVelocity *= 0.25f;
            rb.linearDamping = 3.2f;
            rb.angularDamping = 5f;
        }
        if (baque == null) baque = Resources.Load<AudioClip>("Baque");
#if UNITY_EDITOR
        if (baque == null) baque = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Game/Audio/Baque.wav");
#endif
        if (baque == null) return;
        var go = new GameObject("Baque");
        go.transform.position = onde;
        var a = go.AddComponent<AudioSource>();
        a.clip = baque;
        a.spatialBlend = 1f;
        a.volume = 0.95f;
        a.minDistance = 2f;
        a.maxDistance = 26f;
        a.rolloffMode = AudioRolloffMode.Linear;
        a.Play();
        Destroy(go, baque.length + 0.2f);
    }

    /// Assentou quando PAROU DE ANDAR, não quando a velocidade baixou.
    ///
    /// Medido: o corpo ficava imóvel no chão (o quadril não saía de
    /// (31.34, 0.14, 3.88) por 3 s seguidos) mas com velocidade oscilando
    /// entre 0,11 e 0,25 m/s — tremor de contato do PhysX. Pelo critério de
    /// velocidade ele nunca assentava, e só congelava pela rede de segurança,
    /// 5 s depois. Deslocamento não mente.
    IEnumerator EsperarAssentar()
    {
        var antes = new Vector3[pedacos.Count];
        for (int i = 0; i < pedacos.Count; i++)
            antes[i] = pedacos[i] != null ? pedacos[i].position : Vector3.zero;

        float quieto = 0f, limite = Time.time + 5f;   // rede de segurança
        while (Time.time < limite)
        {
            yield return new WaitForSeconds(0.1f);
            float andou = 0f;
            for (int i = 0; i < pedacos.Count; i++)
            {
                if (pedacos[i] == null) continue;
                var p = pedacos[i].position;
                andou = Mathf.Max(andou, Vector3.Distance(p, antes[i]));
                antes[i] = p;
            }
            quieto = andou < 0.015f ? quieto + 0.1f : 0f;
            if (quieto >= 0.3f) break;
        }
        Travar(true);                               // congela: corpo parado não gasta física
        Assentado = true;
        AlinharRaiz();
    }

    /// Depois que assenta, a raiz do objeto está longe do corpo (a física moveu
    /// os OSSOS, não o transform de cima) — e é a raiz que o jogador interage.
    /// Traz a raiz pro corpo sem arrastar o corpo junto.
    public void AlinharRaiz()
    {
        if (quadril == null) return;
        var alvo = new Vector3(quadril.position.x, transform.position.y, quadril.position.z);
        var delta = alvo - transform.position;
        if (delta.sqrMagnitude < 0.0004f) return;

        var filhos = new List<Transform>();
        for (int i = 0; i < transform.childCount; i++) filhos.Add(transform.GetChild(i));
        var mundo = new List<Vector3>();
        foreach (var f in filhos) mundo.Add(f.position);

        transform.position = alvo;
        for (int i = 0; i < filhos.Count; i++) filhos[i].position = mundo[i];
    }

    /// Onde o corpo REALMENTE está (o transform de cima mente enquanto a física
    /// não assentou).
    public Vector3 Centro => quadril != null ? quadril.position : transform.position;

    /// O osso do quadril: o que as moscas e o sangue devem seguir, já que a
    /// raiz do objeto só reencontra o corpo depois que ele assenta.
    public Transform Osso => quadril;
}

/// Avisa o cadáver do primeiro contato. Morre depois de avisar.
public class BatidaDoCorpo : MonoBehaviour
{
    public System.Action<Vector3> aoBater;

    void OnCollisionEnter(Collision c)
    {
        if (c.relativeVelocity.sqrMagnitude < 0.4f) return;   // encostar não é bater
        aoBater?.Invoke(c.GetContact(0).point);
        aoBater = null;
        enabled = false;
    }
}
