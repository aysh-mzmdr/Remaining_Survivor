using System.Collections;
using UnityEngine;

// Simple detect-and-attack AI. Mirrors KnightMove's "raycast down to find ground Y" approach
// (masked to the Ground layer) instead of using a Rigidbody2D/gravity, so enemies stay glued to
// tilemap platforms with the same zero-physics-setup style as the player.
[RequireComponent(typeof(Animator))]
public class EnemySkeleton : MonoBehaviour
{
    public float detectionRadius = 3.5f;
    public float loseTargetRadius = 5.5f;
    public float attackRange = 1.2f;
    public float moveSpeed = 1.8f;
    public float attackCooldown = 1.8f;
    public float attackWindupDelay = 0.3f;
    public int attackDamage = 6;
    public LayerMask playerLayer;
    public LayerMask groundLayer;
    public float footHalfWidth = 0.25f;

    const float GroundSkin = 0.1f;

    Animator animator;
    Transform player;
    float cooldownTimer;
    bool attacking;

    void Awake()
    {
        animator = GetComponent<Animator>();
    }

    void Update()
    {
        if (cooldownTimer > 0f) cooldownTimer -= Time.deltaTime;

        if (player == null)
        {
            AcquireTarget();
            if (player == null)
            {
                animator.SetBool("IsWalking", false);
                return;
            }
        }

        float dist = Vector2.Distance(transform.position, player.position);
        if (dist > loseTargetRadius)
        {
            player = null;
            animator.SetBool("IsWalking", false);
            return;
        }

        FaceTarget();
        if (attacking) return;

        if (dist <= attackRange)
        {
            animator.SetBool("IsWalking", false);
            if (cooldownTimer <= 0f) StartCoroutine(Attack());
        }
        else
        {
            animator.SetBool("IsWalking", true);
            MoveTowardsTarget();
        }
    }

    void AcquireTarget()
    {
        Collider2D hit = Physics2D.OverlapCircle(transform.position, detectionRadius, playerLayer);
        if (hit != null) player = hit.transform;
    }

    void FaceTarget()
    {
        float dir = player.position.x - transform.position.x;
        if (Mathf.Abs(dir) < 0.01f) return;
        Vector3 scale = transform.localScale;
        scale.x = Mathf.Abs(scale.x) * (dir < 0f ? -1f : 1f);
        transform.localScale = scale;
    }

    void MoveTowardsTarget()
    {
        float dir = Mathf.Sign(player.position.x - transform.position.x);
        Vector3 pos = transform.position;
        pos.x += dir * moveSpeed * Time.deltaTime;

        if (TryGetGroundY(pos.x, pos.y, out float groundY))
        {
            pos.y = groundY;
            transform.position = pos;
        }
    }

    IEnumerator Attack()
    {
        attacking = true;
        cooldownTimer = attackCooldown;
        animator.SetTrigger("Attack");

        yield return new WaitForSeconds(attackWindupDelay);

        if (player != null)
        {
            float facing = Mathf.Sign(transform.localScale.x);
            Vector2 hitPoint = (Vector2)transform.position + new Vector2(facing * attackRange, 0.5f);
            Collider2D hit = Physics2D.OverlapCircle(hitPoint, 0.6f, playerLayer);
            if (hit != null)
            {
                PlayerHealth health = hit.GetComponentInParent<PlayerHealth>();
                if (health != null) health.TakeDamage(attackDamage, transform.position);
            }
        }

        yield return new WaitForSeconds(0.3f);
        attacking = false;
    }

    bool TryGetGroundY(float x, float feetY, out float groundY)
    {
        bool found = false;
        groundY = float.NegativeInfinity;
        for (int i = -1; i <= 1; i++)
        {
            Vector2 origin = new Vector2(x + i * footHalfWidth, feetY + GroundSkin);
            RaycastHit2D hit = Physics2D.Raycast(origin, Vector2.down, GroundSkin + 0.3f, groundLayer);
            if (hit.collider != null && hit.point.y > groundY)
            {
                groundY = hit.point.y;
                found = true;
            }
        }
        return found;
    }
}
