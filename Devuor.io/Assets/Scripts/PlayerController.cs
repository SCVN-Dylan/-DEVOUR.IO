using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Di chuyen nhan vat tren mat dat theo kieu hole.io:
/// - Ban phim: WASD / mui ten
/// - Chuot: giu chuot trai va keo
/// - Mobile: cham va keo
/// Huong di chuyen duoc tinh theo huong camera (camera-relative).
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    [Tooltip("Toc do di chuyen toi da (m/s)")]
    public float moveSpeed = 8f;

    [Tooltip("Do muot khi tang/giam toc (cang lon cang bam duong)")]
    public float acceleration = 20f;

    [Tooltip("Toc do xoay than nhan vat theo huong di chuyen (do/giay)")]
    public float turnSpeed = 720f;

    [Header("Drag Input")]
    [Tooltip("So pixel keo de dat toc do toi da")]
    public float dragRadiusPixels = 120f;

    [Header("Bounds")]
    [Tooltip("Gioi han nua chieu rong cua map (0 = khong gioi han)")]
    public float mapHalfSize = 29f;

    [Header("References")]
    [Tooltip("Camera dung de tinh huong di chuyen. De trong se tu lay Camera.main")]
    public Transform cameraTransform;

    Rigidbody _rb;
    Vector2 _dragOrigin;
    Vector2 _dragCurrent;
    bool _isDragging;
    Vector3 _currentVelocity;

    void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _rb.constraints = RigidbodyConstraints.FreezeRotation;
        _rb.interpolation = RigidbodyInterpolation.Interpolate;

        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;
    }

    void Update()
    {
        ReadDragInput();
    }

    void FixedUpdate()
    {
        Vector2 input = Vector2.ClampMagnitude(ReadKeyboard() + ReadDrag(), 1f);
        Vector3 desired = ToWorldDirection(input) * moveSpeed;

        _currentVelocity = Vector3.MoveTowards(
            _currentVelocity, desired, acceleration * Time.fixedDeltaTime);

        Vector3 next = _rb.position + _currentVelocity * Time.fixedDeltaTime;

        if (mapHalfSize > 0f)
        {
            next.x = Mathf.Clamp(next.x, -mapHalfSize, mapHalfSize);
            next.z = Mathf.Clamp(next.z, -mapHalfSize, mapHalfSize);
        }

        _rb.MovePosition(next);

        if (_currentVelocity.sqrMagnitude > 0.01f)
        {
            Quaternion target = Quaternion.LookRotation(
                new Vector3(_currentVelocity.x, 0f, _currentVelocity.z));
            _rb.MoveRotation(Quaternion.RotateTowards(
                _rb.rotation, target, turnSpeed * Time.fixedDeltaTime));
        }
    }

    Vector2 ReadKeyboard()
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

    void ReadDragInput()
    {
        var touch = Touchscreen.current;
        if (touch != null && touch.primaryTouch.press.isPressed)
        {
            Vector2 pos = touch.primaryTouch.position.ReadValue();
            if (!_isDragging) { _dragOrigin = pos; _isDragging = true; }
            _dragCurrent = pos;
            return;
        }

        var mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.isPressed)
        {
            Vector2 pos = mouse.position.ReadValue();
            if (!_isDragging) { _dragOrigin = pos; _isDragging = true; }
            _dragCurrent = pos;
            return;
        }

        _isDragging = false;
    }

    Vector2 ReadDrag()
    {
        if (!_isDragging || dragRadiusPixels <= 0f) return Vector2.zero;
        return Vector2.ClampMagnitude((_dragCurrent - _dragOrigin) / dragRadiusPixels, 1f);
    }

    Vector3 ToWorldDirection(Vector2 input)
    {
        if (input.sqrMagnitude < 0.0001f) return Vector3.zero;

        Vector3 forward = Vector3.forward;
        Vector3 right = Vector3.right;

        if (cameraTransform != null)
        {
            forward = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
            right = Vector3.Cross(Vector3.up, forward);
        }

        return (forward * input.y + right * input.x);
    }
}
