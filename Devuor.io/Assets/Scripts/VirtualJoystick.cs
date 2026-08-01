using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Joystick ao cho mobile, chay tren EventSystem nen khong phu thuoc
/// backend input (Input System moi hay legacy deu dung duoc).
///
/// Cau truc yeu cau:
///   Joystick   (RectTransform vung cham, Image alpha 0, script nay)
///   |- Background (RectTransform, anchor giua)
///      |- Handle  (RectTransform, anchor giua)
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class VirtualJoystick : MonoBehaviour,
    IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    [Header("References")]
    public RectTransform background;
    public RectTransform handle;

    [Header("Behaviour")]
    [Tooltip("Quang duong handle chay, tinh theo ty le ban kinh Background. 1 = cham vien")]
    [Range(0.1f, 1f)]
    public float handleRange = 1f;

    [Tooltip("Vung chet, 0-1 theo ban kinh Background. Duoi nguong nay coi nhu khong nhap")]
    [Range(0f, 0.9f)]
    public float deadZone = 0.05f;

    [Tooltip("Luon tra ve vector don vi khi ra khoi vung chet: chi co full speed hoac dung. " +
             "Tat de co toc do analog theo do keo, nhung toc do se dao dong theo tay -> kem muot")]
    public bool normalizeOutput = true;

    [Tooltip("Joystick nhay den diem cham dau tien thay vi dung yen mot cho")]
    public bool dynamicOrigin = true;

    [Tooltip("An joystick khi khong cham")]
    public bool hideWhenIdle = false;

    /// <summary>Huong nhap, do dai 0-1.</summary>
    public Vector2 Direction { get; private set; }

    /// <summary>Dang co ngon tay/chuot giu tren joystick hay khong.</summary>
    public bool IsPressed { get; private set; }

    public float Horizontal { get { return Direction.x; } }
    public float Vertical { get { return Direction.y; } }

    RectTransform _area;
    Canvas _canvas;
    CanvasGroup _group;
    Vector2 _homePosition;

    void Awake()
    {
        _area = (RectTransform)transform;
        _canvas = GetComponentInParent<Canvas>();

        if (background != null)
        {
            _homePosition = background.anchoredPosition;
            _group = background.GetComponent<CanvasGroup>();
            if (_group == null) _group = background.gameObject.AddComponent<CanvasGroup>();
        }

        ResetStick();
    }

    void OnDisable()
    {
        IsPressed = false;
        ResetStick();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (background == null || handle == null) return;

        IsPressed = true;

        if (dynamicOrigin)
        {
            // Dat theo world point de khong phu thuoc anchor cua Background
            Vector3 world;
            if (RectTransformUtility.ScreenPointToWorldPointInRectangle(
                    _area, eventData.position, GetCamera(eventData), out world))
                background.position = world;
        }

        if (_group != null) _group.alpha = 1f;

        OnDrag(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!IsPressed || background == null || handle == null) return;

        float scale = (_canvas != null && _canvas.scaleFactor > 0.0001f)
            ? _canvas.scaleFactor : 1f;

        // Tam duoc doc lai moi lan keo (thay vi cache luc nhan xuong) nen van dung
        // ke ca khi Background bi di chuyen boi dynamicOrigin.
        Vector2 center = RectTransformUtility.WorldToScreenPoint(
            GetCamera(eventData), background.position);

        // Chuan hoa theo ban kinh Background: tam dieu khien dung bang joystick nhin thay.
        float radius = Mathf.Min(background.sizeDelta.x, background.sizeDelta.y) * 0.5f;
        if (radius < 1f) radius = 1f;

        Vector2 clamped = Vector2.ClampMagnitude(
            (eventData.position - center) / (radius * scale), 1f);

        handle.anchoredPosition = clamped * radius * handleRange;

        float magnitude = clamped.magnitude;

        if (magnitude <= deadZone)
            Direction = Vector2.zero;
        else if (normalizeOutput)
            // Do keo khong anh huong toc do -> khong bi rung toc do theo tay
            Direction = clamped.normalized;
        else
            // Analog: remap sau vung chet de khong nhay bac tai nguong
            Direction = clamped.normalized *
                        Mathf.Clamp01((magnitude - deadZone) / (1f - deadZone));
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        IsPressed = false;
        ResetStick();
    }

    void ResetStick()
    {
        Direction = Vector2.zero;
        if (handle != null) handle.anchoredPosition = Vector2.zero;
        if (background != null) background.anchoredPosition = _homePosition;
        if (_group != null) _group.alpha = hideWhenIdle ? 0f : 1f;
    }

    Camera GetCamera(PointerEventData eventData)
    {
        if (_canvas == null) return null;
        if (_canvas.renderMode == RenderMode.ScreenSpaceOverlay) return null;
        return _canvas.worldCamera != null ? _canvas.worldCamera : eventData.pressEventCamera;
    }
}
