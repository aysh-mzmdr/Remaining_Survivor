using System.Collections;
using UnityEngine;

public class EnemyHealth : MonoBehaviour
{
    public int maxHealth = 30;
    public float despawnDelay = 1.5f;
    public float knockbackDistance = 0.35f;
    public Color hitFlashColor = new Color(1f, 0.4f, 0.4f);
    public float hitFlashDuration = 0.12f;

    int currentHealth;
    bool isDead;
    Animator animator;
    Collider2D col;
    EnemySkeleton ai;
    SpriteRenderer[] renderers;
    Color[] originalColors;

    void Awake()
    {
        currentHealth = maxHealth;
        animator = GetComponent<Animator>();
        col = GetComponent<Collider2D>();
        ai = GetComponent<EnemySkeleton>();

        renderers = GetComponentsInChildren<SpriteRenderer>();
        originalColors = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++) originalColors[i] = renderers[i].color;
    }

    public void TakeDamage(int amount)
    {
        TakeDamage(amount, transform.position);
    }

    // sourcePosition lets the knockback and hit reaction push away from whatever hit it.
    public void TakeDamage(int amount, Vector2 sourcePosition)
    {
        if (isDead) return;

        currentHealth -= amount;
        if (currentHealth <= 0)
        {
            isDead = true;
            if (col != null) col.enabled = false;
            if (ai != null) ai.enabled = false;
            animator.SetTrigger("Dead");
            Invoke(nameof(Despawn), despawnDelay);
        }
        else
        {
            animator.SetTrigger("Hit");
            float dir = Mathf.Sign(transform.position.x - sourcePosition.x);
            transform.position += new Vector3(dir * knockbackDistance, 0f, 0f);
            StartCoroutine(FlashHit());
        }
    }

    IEnumerator FlashHit()
    {
        for (int i = 0; i < renderers.Length; i++) if (renderers[i] != null) renderers[i].color = hitFlashColor;
        yield return new WaitForSeconds(hitFlashDuration);
        for (int i = 0; i < renderers.Length; i++) if (renderers[i] != null) renderers[i].color = originalColors[i];
    }

    void Despawn()
    {
        gameObject.SetActive(false);
    }
}
