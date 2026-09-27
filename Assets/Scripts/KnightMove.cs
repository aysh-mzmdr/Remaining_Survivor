using System;
using UnityEngine;
using Spine;
using Spine.Unity;

// Keyboard controls for the Spine Knight. Every animation is played through KnightControl.
//
//   A / D or Left / Right   walk        (hold Left Shift to run)
//   Space                   jump
//   J / K                   attack 1 / attack 2
//   1 / 2 / 3               skill 1 / 2 / 3
//   H                       get hit
//   T                       stun
//   X                       die         (R revives)
//
// The ground needs a Collider2D. If there is no ground below, the Knight falls and holds the
// mid-air jump pose; the landing part of the jump animation only plays once ground is close.
[RequireComponent(typeof(SkeletonAnimation), typeof(KnightControl))]
public class KnightMove : MonoBehaviour
{
    public float walkSpeed = 3f;
    public float runSpeed = 6f;

    [Header("Jump")]
    // Peak height above the take-off point, in world units. Landing is only checked once the
    // Knight starts falling again (see UpdateVertical), so this must clear the tallest step in
    // the level (2 tilemap cells) with some margin.
    public float jumpHeight = 2.3f;
    // Heavier on the way down than on the way up, so the jump reads as weighty rather than floaty.
    public float riseGravity = 32f;
    public float fallGravity = 48f;

    // World X limits so the Knight stays on the ground.
    public float minX = -11.5f;
    public float maxX = 11.5f;

    [Header("Ground detection")]
    // Restrict ground raycasts to this layer so they can't hit the Knight's own collider or an
    // enemy's collider instead of terrain.
    public LayerMask groundLayer = ~0;
    public float footHalfWidth = 0.25f;
    // Below this Y the Knight has fallen out of the world and goes back to where it started.
    public float respawnBelowY = -15f;

    [Header("Wall / ceiling detection")]
    // The Knight's body, in world units from the feet. Walking, knockback and jumps stop at solid
    // Ground tiles so the Knight can never end up inside them (it would then "stand" on the
    // tile's inside bottom edge, since the composite collider is outlines only). bodyHeight stays
    // under 1 so the Knight fits through the level's 1-cell-tall gaps.
    public float bodyHalfWidth = 0.25f;
    public float bodyHeight = 0.8f;

    [Header("Jump animation timing (seconds into the 'jump' animation)")]
    // The crouch ends and the feet leave the ground.
    public float takeoffTime = 0.2f;
    // The feet are back on the ground and the landing starts.
    public float landingStartTime = 0.65f;
    // The landing has settled; control returns here instead of waiting for the animation to finish.
    public float landingEndTime = 1.0f;
    // The landing only plays once ground is at most this far below the feet.
    public float landingLookahead = 0.6f;

    const float GroundSkin = 0.1f;
    // The lowest wall ray sits this far above the feet: clear of the ground being stood on, and
    // under GroundSkin so a ledge corner caught mid-fall is snapped onto rather than clipped into.
    const float StepClearance = 0.05f;
    // Walls stop the body this far short of touching, so vertical rays at the body's edge never
    // run exactly along a wall face (which reads as a ceiling/ground hit).
    const float WallSkin = 0.02f;

    enum State { Free, Action, Air, Dead }
    enum Locomotion { None, Idle, Walk, Run }

    SkeletonAnimation skeletonAnimation;
    KnightControl knightControl;

    State state = State.Free;
    Locomotion locomotion = Locomotion.None;
    TrackEntry actionEntry;
    Vector3 spawnPosition;
    bool launched;      // feet have left the ground (false during the crouch before a jump)
    bool landing;       // the landing part of the jump animation is playing
    bool airborne;
    float verticalSpeed;

    void Awake()
    {
        skeletonAnimation = GetComponent<SkeletonAnimation>();
        knightControl = GetComponent<KnightControl>();
        spawnPosition = transform.position;
    }

    void Update()
    {
        if (state == State.Dead)
        {
            if (Input.GetKeyDown(KeyCode.R)) Revive();
        }
        else
        {
            HandleInterrupts();
            if (state == State.Free) HandleActions();

            if (state == State.Free || (state == State.Air && launched))
            {
                float input = Input.GetAxisRaw("Horizontal");
                bool running = Input.GetKey(KeyCode.LeftShift);
                Move(input, running);

                if (state == State.Free) UpdateLocomotion(input, running);
            }
        }

        UpdateVertical();

        if (state == State.Action && landing && actionEntry.TrackTime >= landingEndTime)
        {
            state = State.Free;
            landing = false;
        }
    }

    // Hit, stun and death can cut in on anything (including a jump).
    void HandleInterrupts()
    {
        if (Input.GetKeyDown(KeyCode.X))
        {
            PlayAction(knightControl.death, false);
            state = State.Dead;
        }
        else if (Input.GetKeyDown(KeyCode.H)) PlayAction(knightControl.getHit, true);
        else if (Input.GetKeyDown(KeyCode.T)) PlayAction(knightControl.stun, true);
    }

    // Raised once whenever an attack action starts, so PlayerCombat can time its hit detection
    // off the same key presses without duplicating HandleActions' private key-reading logic.
    public event Action OnAttackStarted;

    public bool IsDead => state == State.Dead;

    // Reuses the existing get-hit reaction; the natural hook for PlayerHealth.TakeDamage.
    // knockbackDir/knockbackDistance nudge the Knight away from whatever hit it.
    public bool TakeHit(float knockbackDir = 0f, float knockbackDistance = 0f)
    {
        if (state == State.Dead) return false;

        if (knockbackDistance > 0f)
        {
            Vector3 pos = transform.position;
            pos.x = Mathf.Clamp(MoveXUntilWall(pos, knockbackDir * knockbackDistance), minX, maxX);
            transform.position = pos;
        }

        PlayAction(knightControl.getHit, true);
        return true;
    }

    // Reuses the existing death reaction (identical to the manual X-key path in HandleInterrupts).
    public void Die()
    {
        if (state == State.Dead) return;
        PlayAction(knightControl.death, false);
        state = State.Dead;
    }

    void HandleActions()
    {
        if (Input.GetKeyDown(KeyCode.Space)) StartJump();
        else if (Input.GetKeyDown(KeyCode.J)) { OnAttackStarted?.Invoke(); PlayAction(knightControl.attack_1, true); }
        else if (Input.GetKeyDown(KeyCode.K)) { OnAttackStarted?.Invoke(); PlayAction(knightControl.attack_2, true); }
        else if (Input.GetKeyDown(KeyCode.Alpha1)) PlayAction(knightControl.skill_1, true);
        else if (Input.GetKeyDown(KeyCode.Alpha2)) PlayAction(knightControl.skill_2, true);
        else if (Input.GetKeyDown(KeyCode.Alpha3)) PlayAction(knightControl.skill_3, true);
    }

    void Move(float input, bool running)
    {
        if (Mathf.Approximately(input, 0f)) return;

        Vector3 pos = transform.position;
        float speed = running ? runSpeed : walkSpeed;
        pos.x = Mathf.Clamp(MoveXUntilWall(pos, input * speed * Time.deltaTime), minX, maxX);
        transform.position = pos;

        // Art faces right, so mirror the skeleton when moving left.
        skeletonAnimation.Skeleton.ScaleX = input < 0f ? -1f : 1f;
    }

    // X after moving dx from pos, stopping short of any wall (a few rays up the body).
    float MoveXUntilWall(Vector3 pos, float dx)
    {
        if (Mathf.Approximately(dx, 0f)) return pos.x;

        float dir = Mathf.Sign(dx);
        float allowed = Mathf.Abs(dx);
        for (int i = 0; i < 3; i++)
        {
            float y = pos.y + Mathf.Lerp(StepClearance, bodyHeight, i * 0.5f);
            RaycastHit2D hit = Physics2D.Raycast(new Vector2(pos.x, y), new Vector2(dir, 0f), allowed + bodyHalfWidth + WallSkin, groundLayer);
            if (hit.collider != null) allowed = Mathf.Max(0f, hit.distance - bodyHalfWidth - WallSkin);
        }
        return pos.x + dir * allowed;
    }

    // KnightControl restarts the animation on every call, so only call it when the state changes.
    void UpdateLocomotion(float input, bool running)
    {
        Locomotion wanted = Mathf.Approximately(input, 0f)
            ? Locomotion.Idle
            : (running ? Locomotion.Run : Locomotion.Walk);
        if (wanted == locomotion) return;

        locomotion = wanted;
        switch (wanted)
        {
            case Locomotion.Idle: knightControl.idle(); break;
            case Locomotion.Walk: knightControl.walking(); break;
            case Locomotion.Run: knightControl.running(); break;
        }
    }

    // KnightControl always loops, so play the animation once and hand control back when it finishes.
    void PlayAction(System.Action play, bool returnToFree)
    {
        state = State.Action;
        locomotion = Locomotion.None;
        landing = false;

        play();
        TrackEntry entry = knightControl.spineAnimationState.GetCurrent(0);
        entry.Loop = false;
        actionEntry = entry;
        if (returnToFree) entry.Complete += OnActionComplete;
    }

    void OnActionComplete(TrackEntry entry)
    {
        if (state == State.Action && actionEntry == entry) state = State.Free;
    }

    // Crouch first; the feet leave the ground once the animation reaches takeoffTime.
    void StartJump()
    {
        launched = false;
        PlayJumpAnimation(0f);
    }

    // Walked off an edge: same jump animation, starting from the mid-air pose.
    void StartFall()
    {
        launched = true;
        airborne = true;
        verticalSpeed = 0f;
        PlayJumpAnimation(landingStartTime);
    }

    void PlayJumpAnimation(float startTime)
    {
        state = State.Air;
        locomotion = Locomotion.None;
        landing = false;

        knightControl.jump();
        TrackEntry entry = knightControl.spineAnimationState.GetCurrent(0);
        entry.Loop = false;
        entry.TrackTime = startTime;
        entry.Complete += OnActionComplete;
        actionEntry = entry;
    }

    // Take-off, gravity, ground contact and the landing part of the jump animation.
    void UpdateVertical()
    {
        Vector3 pos = transform.position;

        if (state == State.Air && !launched)
        {
            if (actionEntry.TrackTime < takeoffTime) return;

            launched = true;
            airborne = true;
            verticalSpeed = Mathf.Sqrt(2f * riseGravity * jumpHeight);
        }

        if (!airborne)
        {
            float ignored;
            if (!TryGetGround(pos.x, pos.y, 0.05f, out ignored))
            {
                if (state == State.Free) StartFall();
                else { airborne = true; verticalSpeed = 0f; }
            }
            return;
        }

        verticalSpeed -= (verticalSpeed > 0f ? riseGravity : fallGravity) * Time.deltaTime;
        float newY = pos.y + verticalSpeed * Time.deltaTime;

        // Bump the head on solid ground above instead of rising into it.
        float ceilingY;
        if (verticalSpeed > 0f && TryGetCeiling(pos.x, pos.y + bodyHeight, newY - pos.y, out ceilingY))
        {
            newY = ceilingY - bodyHeight;
            verticalSpeed = 0f;
        }

        float groundY;
        if (verticalSpeed <= 0f && TryGetGround(pos.x, pos.y, pos.y - newY, out groundY))
        {
            pos.y = groundY;
            airborne = false;
            verticalSpeed = 0f;
            OnLanded();
        }
        else
        {
            pos.y = newY;
        }
        transform.position = pos;

        UpdateLandingHold(pos);

        if (pos.y < respawnBelowY) Respawn();
    }

    // In the air the jump animation waits at the pose before the landing until ground is close.
    void UpdateLandingHold(Vector3 pos)
    {
        if (state != State.Air || actionEntry == null) return;
        if (actionEntry.TrackTime < landingStartTime) return;

        float ignored;
        bool groundNear = TryGetGround(pos.x, pos.y, landingLookahead, out ignored);
        actionEntry.TimeScale = groundNear ? 1f : 0f;
        if (!groundNear) actionEntry.TrackTime = landingStartTime;
    }

    // Let the landing part of the jump animation play out, then go back to Free.
    void OnLanded()
    {
        if (state != State.Air) return;

        state = State.Action;
        landing = true;
        actionEntry.TimeScale = 1f;
    }

    // Highest ground surface under the feet within `distance`, using a few rays across the feet.
    bool TryGetGround(float x, float feetY, float distance, out float groundY)
    {
        bool found = false;
        groundY = float.NegativeInfinity;

        for (int i = -1; i <= 1; i++)
        {
            Vector2 origin = new Vector2(x + i * footHalfWidth, feetY + GroundSkin);
            RaycastHit2D hit = Physics2D.Raycast(origin, Vector2.down, GroundSkin + distance, groundLayer);
            if (hit.collider != null && hit.point.y > groundY)
            {
                groundY = hit.point.y;
                found = true;
            }
        }
        return found;
    }

    // Lowest ground surface above the head within `distance`, using a few rays across the head.
    bool TryGetCeiling(float x, float headY, float distance, out float ceilingY)
    {
        bool found = false;
        ceilingY = float.PositiveInfinity;

        for (int i = -1; i <= 1; i++)
        {
            Vector2 origin = new Vector2(x + i * bodyHalfWidth, headY);
            RaycastHit2D hit = Physics2D.Raycast(origin, Vector2.up, distance, groundLayer);
            if (hit.collider != null && hit.point.y < ceilingY)
            {
                ceilingY = hit.point.y;
                found = true;
            }
        }
        return found;
    }

    void Respawn()
    {
        transform.position = spawnPosition;
        airborne = false;
        verticalSpeed = 0f;
        state = State.Free;
        locomotion = Locomotion.None;
        landing = false;
    }

    void Revive()
    {
        state = State.Free;
        locomotion = Locomotion.None;
    }
}
