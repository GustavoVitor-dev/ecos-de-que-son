using UnityEngine;
using UnityEngine.InputSystem;

/// A lanterna e o segundo custo do jogo.
/// Ruido te denuncia pelo ouvido, luz te denuncia pelos olhos.
/// Mesma troca, sentido diferente: voce ve mais, e e visto de mais longe.
[RequireComponent(typeof(Light))]
public class Flashlight : MonoBehaviour
{
    public bool startsOn = true;
    public Key toggleKey = Key.F;

    public bool IsOn { get; private set; }

    Light spot;

    void Awake()
    {
        spot = GetComponent<Light>();
        Set(startsOn);
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;
        if (kb[toggleKey].wasPressedThisFrame) Set(!IsOn);
    }

    void Set(bool on)
    {
        IsOn = on;
        if (spot != null) spot.enabled = on;
    }
}
