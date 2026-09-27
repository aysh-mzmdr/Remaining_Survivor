using UnityEngine;

// Smoothly follows the target on both axes, with no level bounds.
public class CameraFollow : MonoBehaviour
{
    public Transform target;
    public float smoothTime = 0.15f;

    // The camera centre sits this far above the target's pivot (the Knight's feet), so more of
    // what's ahead/above is visible than of the ground underfoot.
    public float yOffset = 1f;

    Vector2 velocity;

    void LateUpdate()
    {
        if (target == null) return;

        Vector3 p = transform.position;
        p.x = Mathf.SmoothDamp(p.x, target.position.x, ref velocity.x, smoothTime);
        p.y = Mathf.SmoothDamp(p.y, target.position.y + yOffset, ref velocity.y, smoothTime);
        transform.position = p;
    }
}
