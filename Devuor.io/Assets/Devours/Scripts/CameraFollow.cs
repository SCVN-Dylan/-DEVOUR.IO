using UnityEngine;

/// <summary>
/// Camera goc nghieng bam theo nhan vat, kieu hole.io.
/// </summary>
[ExecuteAlways]
public class CameraFollow : MonoBehaviour
{
    [Header("Target")]
    public Transform target;

    [Header("Framing")]
    [Tooltip("Do cao cua camera so voi nhan vat")]
    public float height = 18f;

    [Tooltip("Khoang lui ve phia sau nhan vat")]
    public float distance = 14f;

    [Tooltip("Goc nhin xuong (do)")]
    public float pitch = 52f;

    [Header("Damping")]
    [Tooltip("Thoi gian bam theo, 0 = bam cung")]
    public float smoothTime = 0.18f;

    Vector3 _velocity;

    void LateUpdate()
    {
        if (target == null) return;

        Vector3 desired = target.position + new Vector3(0f, height, -distance);

        if (Application.isPlaying && smoothTime > 0f)
            transform.position = Vector3.SmoothDamp(
                transform.position, desired, ref _velocity, smoothTime);
        else
            transform.position = desired;

        transform.rotation = Quaternion.Euler(pitch, 0f, 0f);
    }
}
