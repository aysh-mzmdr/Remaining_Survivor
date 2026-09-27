using System.Collections;
using UnityEngine;
using Spine.Unity;

[RequireComponent(typeof(KnightMove))]
public class PlayerHealth : MonoBehaviour
{
    public int maxHealth = 100;
    // Must comfortably outlast the "Get Hit" animation, otherwise a swarm of enemies can land a
    // hit again before the current hit-reaction finishes, which re-triggers TakeHit() and
    // overwrites it with a fresh one before it ever completes - permanently stuck in the hit
    // reaction (KnightMove.HandleActions only runs in State.Free), unable to attack or move.
    public float invulnerabilityDuration = 1.0f;
    public float knockbackDistance = 0.5f;
    public Color hitFlashColor = new Color(1f, 0.35f, 0.35f);
    public float hitFlashDuration = 0.12f;
    public PlayerHealthBarUI healthBarUI;

    int currentHealth;
    float invulnTimer;
    KnightMove knightMove;
    SkeletonAnimation skeletonAnimation;

    void Awake()
    {
        knightMove = GetComponent<KnightMove>();
        skeletonAnimation = GetComponent<SkeletonAnimation>();
        currentHealth = maxHealth;
    }

    void Start()
    {
        if (healthBarUI != null) healthBarUI.SetHealth(currentHealth, maxHealth);
    }

    void Update()
    {
        if (invulnTimer > 0f) invulnTimer -= Time.deltaTime;
    }

    public void TakeDamage(int amount)
    {
        TakeDamage(amount, transform.position);
    }

    // sourcePosition lets the knockback push the Knight away from whatever dealt the damage.
    public void TakeDamage(int amount, Vector2 sourcePosition)
    {
        if (knightMove.IsDead || invulnTimer > 0f) return;

        currentHealth = Mathf.Max(0, currentHealth - amount);
        if (healthBarUI != null) healthBarUI.SetHealth(currentHealth, maxHealth);

        if (currentHealth <= 0)
        {
            knightMove.Die();
        }
        else
        {
            float dir = Mathf.Sign(transform.position.x - sourcePosition.x);
            knightMove.TakeHit(dir, knockbackDistance);
            invulnTimer = invulnerabilityDuration;
            StartCoroutine(FlashHit());
        }
    }

    IEnumerator FlashHit()
    {
        var skeleton = skeletonAnimation.Skeleton;
        skeleton.R = hitFlashColor.r;
        skeleton.G = hitFlashColor.g;
        skeleton.B = hitFlashColor.b;
        yield return new WaitForSeconds(hitFlashDuration);
        skeleton.R = 1f;
        skeleton.G = 1f;
        skeleton.B = 1f;
    }
}
