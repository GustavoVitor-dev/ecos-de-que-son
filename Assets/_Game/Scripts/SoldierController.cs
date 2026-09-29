using UnityEngine;
using UnityEngine.InputSystem;

/// Movimento com tres marchas, cada uma com um custo de ruido diferente.
/// Essa troca (velocidade x barulho) e o nucleo do jogo. Se ela nao for tensa,
/// nada que a gente construir por cima resolve.
///
/// Nome nao e "PlayerController" de proposito: o pacote Visual Scripting
/// ja traz uma classe com esse nome nos exemplos dele e o nome fica ambiguo.
[RequireComponent(typeof(CharacterController))]
public class SoldierController : MonoBehaviour
{
    [Header("Velocidade (m/s)")]
    public float crouchSpeed = 1.4f;
    public float walkSpeed = 2.9f;
    public float runSpeed = 5.2f;

    [Header("Ruido emitido (raio em metros)")]
    public float crouchNoise = 0f;
    public float walkNoise = 6f;
    public float runNoise = 14f;
    public float noiseInterval = 0.45f;

    [Header("Camera")]
    public CameraRig rig;
    public Transform head;
    public float mouseSensitivity = 0.11f;

    /// Cutscene e transicao tiram o controle sem tirar o jogador do lugar.
    /// Enquanto isto for true ele nao anda, nao agacha e nao faz ruido.
    [System.NonSerialized] public bool Travado;
    /// Piloto automático dirigindo agachado: sem isto, travado = de pé, e os
    /// guardas viam o piloto por cima de cobertura que esconde um jogador.
    [System.NonSerialized] public bool ForcarAgachado;

    public bool IsCrouching { get; private set; }
    public float CurrentNoiseRadius { get; private set; }

    CharacterController cc;
    float pitch;
    float noiseTimer;

    void Awake() => cc = GetComponent<CharacterController>();

    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        if (Travado)
        {
            IsCrouching = ForcarAgachado;
            CurrentNoiseRadius = 0f;
            return;
        }

        IsCrouching = kb.leftCtrlKey.isPressed;
        bool running = kb.leftShiftKey.isPressed && !IsCrouching;

        Vector2 input = new Vector2(
            (kb.dKey.isPressed ? 1f : 0f) - (kb.aKey.isPressed ? 1f : 0f),
            (kb.wKey.isPressed ? 1f : 0f) - (kb.sKey.isPressed ? 1f : 0f));
        if (input.sqrMagnitude > 1f) input.Normalize();

        bool fps = rig != null && rig.mode == CameraMode.FirstPerson;
        if (fps) LookFirstPerson(); else AimTopDown();

        Vector3 dir = fps
            ? transform.right * input.x + transform.forward * input.y
            : new Vector3(input.x, 0f, input.y);

        float speed = IsCrouching ? crouchSpeed : (running ? runSpeed : walkSpeed);
        cc.Move((dir * speed + Vector3.down * 9.81f) * Time.deltaTime);

        EmitNoise(input.sqrMagnitude > 0.01f, running);
    }

    void LookFirstPerson()
    {
        if (Mouse.current == null) return;
        Vector2 d = Mouse.current.delta.ReadValue() * mouseSensitivity;
        transform.Rotate(Vector3.up, d.x, Space.World);
        pitch = Mathf.Clamp(pitch - d.y, -80f, 80f);
        if (head != null) head.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    void AimTopDown()
    {
        Camera cam = rig != null ? rig.topDownCamera : Camera.main;
        if (cam == null || Mouse.current == null) return;

        Ray ray = cam.ScreenPointToRay(Mouse.current.position.ReadValue());
        Plane ground = new Plane(Vector3.up, new Vector3(0f, transform.position.y, 0f));
        if (!ground.Raycast(ray, out float dist)) return;

        Vector3 look = ray.GetPoint(dist) - transform.position;
        look.y = 0f;
        if (look.sqrMagnitude > 0.04f) transform.rotation = Quaternion.LookRotation(look);
    }

    void EmitNoise(bool moving, bool running)
    {
        CurrentNoiseRadius = !moving ? 0f
            : IsCrouching ? crouchNoise
            : running ? runNoise
            : walkNoise;

        if (CurrentNoiseRadius <= 0.01f) { noiseTimer = 0f; return; }

        noiseTimer -= Time.deltaTime;
        if (noiseTimer > 0f) return;

        noiseTimer = noiseInterval * (running ? 0.7f : 1f);
        Noise.Emit(transform.position, CurrentNoiseRadius);
    }
}
