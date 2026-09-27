using UnityEngine;

// Smoothly follows the target horizontally and keeps a fixed height. The camera never shows
// anything left of minX or right of maxX (the edges of the view, not the camera centre).
public class CameraFollow : MonoBehaviour
{
    public Transform target;
    public float smoothTime = 0.15f;

    // Height of the camera; the level is horizontal, so it does not follow the target vertically.
    public float fixedY = -1.5f;

    [Header("Level bounds (world X, edges of the view)")]
    public float minX = -9f;
    public float maxX = 121f;

    Camera cam;
    float velocityX;

    void Awake()
    {
        cam = GetComponent<Camera>();
    }

    void LateUpdate()
    {
        if (target == null) return;

        float halfWidth = cam.orthographicSize * cam.aspect;
        float lo = minX + halfWidth;
        float hi = maxX - halfWidth;
        float wantedX = hi < lo ? (minX + maxX) * 0.5f : Mathf.Clamp(target.position.x, lo, hi);

        Vector3 p = transform.position;
        p.x = Mathf.SmoothDamp(p.x, wantedX, ref velocityX, smoothTime);
        p.y = fixedY;
        transform.position = p;
    }
}
