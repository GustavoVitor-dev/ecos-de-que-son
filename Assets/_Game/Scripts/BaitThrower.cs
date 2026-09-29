using UnityEngine;
using UnityEngine.InputSystem;

/// Arremesso da isca sonora. Segure para mirar, solte para jogar.
///
/// Mirar com marcador visivel e deliberado: sem saber onde vai cair, a isca vira
/// aposta e o jogador nao consegue planejar. E planejar e o ponto — esta e a fase
/// MANIPULAR do loop, a unica que ainda nao existia.
///
/// A quantidade e limitada porque recurso infinito nao gera decisao.
public class BaitThrower : MonoBehaviour
{
    [Header("Recurso")]
    public int count = 3;
    public float maxRange = 14f;
    public float cooldown = 0.6f;

    [Header("Controle")]
    public Key key = Key.G;

    [Header("Isca")]
    public float noiseRadius = 13f;
    public Vector3 handOffset = new Vector3(0f, 0.6f, 0.4f);

    public int Remaining => count;
    public bool Aiming { get; private set; }

    CameraRig rig;
    Transform head;
    Material matMarca;
    GameObject marker;
    float cd;

    void Start()
    {
        // iscas sao da run inteira, nao do bloco
        if (RunState.Iscas < 0) RunState.Iscas = count;
        count = RunState.Iscas;

        rig = FindFirstObjectByType<CameraRig>();
        var sc = GetComponent<SoldierController>();
        if (sc != null) head = sc.head;

        matMarca = Resources.Load<Material>("gb_Marca");   // opcional; cai no padrao se nao achar

        marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        marker.name = "MiraIsca";
        Destroy(marker.GetComponent<Collider>());
        marker.transform.localScale = new Vector3(1.1f, 0.03f, 1.1f);
        marker.SetActive(false);
    }

    void Update()
    {
        if (cd > 0f) cd -= Time.deltaTime;

        var kb = Keyboard.current;
        if (kb == null) return;

        bool podeUsar = count > 0 && cd <= 0f;

        if (kb[key].isPressed && podeUsar)
        {
            Aiming = true;
            if (TryAim(out Vector3 alvo))
            {
                marker.SetActive(true);
                marker.transform.position = alvo + Vector3.up * 0.03f;
            }
            else marker.SetActive(false);
        }
        else if (Aiming)
        {
            Aiming = false;
            marker.SetActive(false);
            if (podeUsar && TryAim(out Vector3 alvo)) Throw(alvo);
        }
    }

    bool TryAim(out Vector3 ponto)
    {
        ponto = transform.position;

        bool fps = rig != null && rig.mode == CameraMode.FirstPerson;
        Ray ray;

        if (fps)
        {
            if (head == null) return false;
            ray = new Ray(head.position, head.forward);
        }
        else
        {
            Camera cam = rig != null ? rig.topDownCamera : Camera.main;
            if (cam == null || Mouse.current == null) return false;
            ray = cam.ScreenPointToRay(Mouse.current.position.ReadValue());
        }

        if (!Physics.Raycast(ray, out RaycastHit hit, 300f, ~0, QueryTriggerInteraction.Ignore))
            return false;

        Vector3 p = hit.point;
        Vector3 plano = p - transform.position;
        plano.y = 0f;

        // alcance limitado: a isca nao resolve o mapa inteiro de onde voce esta
        if (plano.magnitude > maxRange)
        {
            Vector3 limite = transform.position + plano.normalized * maxRange;
            if (Physics.Raycast(limite + Vector3.up * 30f, Vector3.down, out RaycastHit chao,
                                60f, ~0, QueryTriggerInteraction.Ignore))
                p = chao.point;
            else
                p = new Vector3(limite.x, transform.position.y - 1f, limite.z);
        }

        ponto = p;
        return true;
    }

    void Throw(Vector3 alvo)
    {
        count--;
        RunState.Iscas = count;
        cd = cooldown;

        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "Isca";
        go.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);
        Destroy(go.GetComponent<Collider>());          // voo e escrito, nao simulado
        if (matMarca != null) go.GetComponent<MeshRenderer>().sharedMaterial = matMarca;

        go.AddComponent<AudioSource>();
        var bait = go.AddComponent<SoundBait>();
        bait.noiseRadius = noiseRadius;

        Vector3 mao = transform.position
                    + transform.right * handOffset.x
                    + Vector3.up * handOffset.y
                    + transform.forward * handOffset.z;

        bait.Launch(mao, alvo);
    }
}
