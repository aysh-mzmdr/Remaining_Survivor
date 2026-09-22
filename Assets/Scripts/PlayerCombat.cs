using System.Collections;
using UnityEngine;
using Spine.Unity;

// Subscribes to KnightMove.OnAttackStarted instead of re-reading the J/K keys itself, so it
// never duplicates or races KnightMove's own attack timing/cooldown state.
[RequireComponent(typeof(KnightMove), typeof(SkeletonAnimation))]
public class PlayerCombat : MonoBehaviour
{
    public float attackReach = 1.1f;
    public float attackRadius = 0.6f;
    public int attackDamage = 15;
    public float swingDelay = 0.2f;
    public LayerMask enemyLayer;

    KnightMove knightMove;
    SkeletonAnimation skeletonAnimation;

    void Awake()
    {
        knightMove = GetComponent<KnightMove>();
        skeletonAnimation = GetComponent<SkeletonAnimation>();
        knightMove.OnAttackStarted += HandleAttackStarted;
    }

    void OnDestroy()
    {
        knightMove.OnAttackStarted -= HandleAttackStarted;
    }

    void HandleAttackStarted()
    {
        StartCoroutine(SwingAfterDelay());
    }

    IEnumerator SwingAfterDelay()
    {
        yield return new WaitForSeconds(swingDelay);

        float facing = skeletonAnimation.Skeleton.ScaleX < 0f ? -1f : 1f;
        Vector2 hitPoint = (Vector2)transform.position + new Vector2(facing * attackReach, 0.5f);
        Collider2D[] hits = Physics2D.OverlapCircleAll(hitPoint, attackRadius, enemyLayer);
        foreach (Collider2D hit in hits)
        {
            EnemyHealth enemyHealth = hit.GetComponentInParent<EnemyHealth>();
            if (enemyHealth != null) enemyHealth.TakeDamage(attackDamage, transform.position);
        }
    }
}
