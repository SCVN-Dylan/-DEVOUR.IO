using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Doi input 2D tu joystick/ban phim sang huong the gioi theo truc camera,
/// roi day xuong RbMovement.
///
/// Pipeline: Joystick -> MovePlayerByInput -> RbMovement.SetDir -> rb.velocity
/// </summary>
[RequireComponent(typeof(RbMovement))]
public class PlayerController : MonoBehaviour
{
    [Header("Input")]
    [Tooltip("Joystick ao dieu khien nhan vat")]
    public VirtualJoystick joystick;

    [Tooltip("Cho phep WASD / phim mui ten de test trong Editor")]
    public bool useKeyboard = true;

    [Header("References")]
    [Tooltip("Camera dung de doi input sang huong the gioi. De trong se tu lay Camera.main")]
    public Transform cameraTransform;

    [Header("Animation")]
    [Tooltip("Animator xu ly animation cho player. De trong se tu tim o child")]
    public Animator animator;

    private RbMovement _movement;
    private static readonly int RunSuckingHash = Animator.StringToHash("RunSucking");

    void Awake()
    {
        EnsureReferences();
    }

    private void EnsureReferences()
    {
        if (_movement == null)
            _movement = GetComponent<RbMovement>();

        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;

        if (animator == null)
            animator = GetComponentInChildren<Animator>();
    }

    void Update()
    {
        MovePlayerByInput(ReadInput());
        UpdateAnimation();
    }

    public void UpdateAnimation()
    {
        EnsureReferences();
        if (animator != null && _movement != null)
        {
            animator.SetBool(RunSuckingHash, _movement.IsMoving);
        }
    }

    public void MovePlayerByInput(Vector2 input)
    {
        EnsureReferences();
        if (_movement != null)
        {
            _movement.SetDir(ToWorldDirection(input));
        }
    }

    private Vector2 ReadInput()
    {
        Vector2 input = joystick != null ? joystick.Direction : Vector2.zero;
        if (useKeyboard) input += ReadKeyboard();
        return input;
    }

    private Vector2 ReadKeyboard()
    {
        var kb = Keyboard.current;
        if (kb == null) return Vector2.zero;

        Vector2 v = Vector2.zero;
        if (kb.wKey.isPressed || kb.upArrowKey.isPressed) v.y += 1f;
        if (kb.sKey.isPressed || kb.downArrowKey.isPressed) v.y -= 1f;
        if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) v.x -= 1f;
        if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) v.x += 1f;
        return v;
    }

    /// <summary>
    /// Lay truc camera roi flatten Y thay vi hardcode 45 do, de doi goc camera
    /// khong phai sua code. Normalize cuoi cung de duong cheo khong nhanh hon truc thang.
    /// </summary>
    private Vector3 ToWorldDirection(Vector2 input)
    {
        if (input.sqrMagnitude < 0.0001f) return Vector3.zero;

        Vector3 right = Vector3.right;
        Vector3 forward = Vector3.forward;

        if (cameraTransform != null)
        {
            right = cameraTransform.right;
            right.y = 0f;
            right.Normalize();

            forward = cameraTransform.forward;
            forward.y = 0f;

            // Camera nhin thang xuong thi forward bi triet tieu, lay up lam thay the
            if (forward.sqrMagnitude < 0.0001f) forward = cameraTransform.up;
            forward.y = 0f;
            forward.Normalize();
        }

        Vector3 dir = right * input.x + forward * input.y;
        return dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector3.zero;
    }
}
