using UnityEngine;
using UnityEngine.InputSystem;

public enum CameraMode { TopDown, FirstPerson }

/// Duas cameras na mesma cena, uma ligada por vez. Tab alterna.
/// Existe pra resolver top-down vs FPS jogando, nao discutindo.
public class CameraRig : MonoBehaviour
{
    public CameraMode mode = CameraMode.TopDown;

    [Header("Referencias")]
    public Camera topDownCamera;
    public Camera firstPersonCamera;
    public Transform target;
    [Tooltip("Malhas do jogador. Somem em primeira pessoa pra nao ver o interior da propria cabeca.")]
    public Renderer[] playerBody;

    [Header("Top-down")]
    public float height = 14f;
    public float back = 3f;
    public float pitch = 70f;
    public float followLerp = 12f;

    /// Enquanto a cutscene manda na camera, o rig nao disputa com ela.
    /// Ao soltar, o proprio lerp de sempre traz a camera de volta deslizando.
    [System.NonSerialized] public bool Cinematica;

    /// O ponto do chao que a camera esta enquadrando AGORA, e a lente ativa.
    ///
    /// Existe porque meio jogo seguia o JOGADOR pra decidir onde chover, onde
    /// abrir a copa e onde desbotar folha. Enquanto a camera e a do jogo da no
    /// mesmo; numa cutscene, nao: a chuva caia num retangulo fora do quadro e
    /// a copa abria num buraco que ninguem estava vendo.
    public static Vector3 Foco { get; private set; }
    public static Camera Lente { get; private set; }
    /// O enquadramento do JOGO (o ultimo visto fora de cutscene). Quem
    /// dimensiona efeito na tela compara com isto, nao com um numero fixo.
    public static float TamanhoDeJogo { get; private set; } = 6f;
    /// A camera esta nas maos de uma cutscene.
    public static bool Cena { get; private set; }

    void Start() => Apply();

    void OnDisable() { Lente = null; Cena = false; }

    void Medir()
    {
        var cam = mode == CameraMode.TopDown ? topDownCamera : firstPersonCamera;
        if (cam == null) cam = Camera.main;
        Lente = cam;
        Cena = Cinematica;
        if (!Cinematica && cam != null && cam.orthographic && cam.orthographicSize > 0.1f)
            TamanhoDeJogo = cam.orthographicSize;
        if (cam == null) { if (target != null) Foco = target.position; return; }

        float chao = target != null ? target.position.y : 0f;
        var fwd = cam.transform.forward;
        // onde o raio da camera cruza o chao; se ela nao aponta pra baixo
        // (primeira pessoa), o foco e ela mesma
        if (fwd.y < -0.05f) Foco = cam.transform.position + fwd * ((cam.transform.position.y - chao) / -fwd.y);
        else Foco = cam.transform.position;
    }

    void Update()
    {
        if (Cinematica) return;

        if (Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame)
        {
            mode = mode == CameraMode.TopDown ? CameraMode.FirstPerson : CameraMode.TopDown;
            Apply();
        }
    }

    void LateUpdate()
    {
        if (!Cinematica && mode == CameraMode.TopDown && target != null && topDownCamera != null)
        {
            Vector3 wanted = target.position + new Vector3(0f, height, -back);
            topDownCamera.transform.position = Vector3.Lerp(
                topDownCamera.transform.position, wanted, 1f - Mathf.Exp(-followLerp * Time.deltaTime));
            topDownCamera.transform.rotation = Quaternion.Euler(pitch, 0f, 0f);
        }
        // medido DEPOIS de mover, e tambem durante a cutscene: quem segue o
        // quadro precisa do quadro deste frame, nao do anterior
        Medir();
    }

    void Apply()
    {
        bool td = mode == CameraMode.TopDown;

        if (topDownCamera != null) topDownCamera.enabled = td;
        if (firstPersonCamera != null) firstPersonCamera.enabled = !td;

        // Em primeira pessoa a camera fica dentro da malha do jogador.
        // Virou lista porque o soldado fardado tem varias malhas (corpo, colete,
        // cabelo, olhos): com um Renderer so, o resto continuava na frente da lente.
        if (playerBody != null)
            foreach (var r in playerBody)
                if (r != null) r.enabled = td;

        Cursor.lockState = td ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = td;
    }
}
