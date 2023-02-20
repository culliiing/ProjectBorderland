using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    // Player Keybind
    // Left/Right - Move Character
    // C - Jump
    // X - Dash
    // Z - Grab

    [Header("Components")]
    private Rigidbody2D rb;
    private BoxCollider2D boxCollider;
    private TrailRenderer trailRenderer;

    [Header("Layer Masks")]
    [SerializeField] LayerMask groundLayer;
    [SerializeField] LayerMask wallLayer;
    [SerializeField] LayerMask cornerCorrectLayer;

    [Header("Movement Settings")]
    [SerializeField] float movementAcceleration;
    [SerializeField] float maxMovementSpeed;
    [SerializeField] float movementDeceleration;
    private float horizontalMovementInput;
    private float verticalMovementInput;
    Vector2 movespeed;

    [Header("Jump Settings")]
    [SerializeField] float jumpForce;
    [SerializeField] float airDeceleration;
    [SerializeField] float fallMultiplier;
    [Tooltip("Amount of time holding space to jump higher")][SerializeField] float jumpTime;
    [SerializeField] float maxFallingspeed;
    [SerializeField] float coyoteTime;
    [SerializeField] float jumpBufferTimer;   
    [SerializeField] bool enableDoubleJump;
    private Vector2 defaultGravity = new Vector2(0, -9.8f);
    private float jumpBufferCounter;
    private float coyoteTimeCounter;
    private float jumpTimeCounter;
    private bool isJumping;
    private bool canDoubleJump;

    [Header("Dash Settings")]
    [SerializeField] float dashSpeed;
    [SerializeField] float dashTime;
    private Vector2 dashDirection;
    private float dashMultiplier;
    private bool isDashing;
    private bool canDash;

    [Header("Wall Settings")]
    [SerializeField] float wallJumpTime;   
    [SerializeField] float climbingSpeed;
    private float climbMultiplier;
    private float wallSlidingSpeed;
    private float wallJumpCounter;
    private float wallJumpDirection;
    private bool isWallJumping;
    private bool isWallSliding;
    private bool isHoldingWall;
    private bool isClimbing;

    [Header("Corner Correction Settings")]
    [SerializeField] float _topRaycastLength;
    [SerializeField] Vector3 _edgeRaycastOffset;
    [SerializeField] Vector3 _innerRaycastOffset;
    RaycastHit2D headDetector;
    private bool canCornerCorrect;

    [Header("Stamina")]
    [SerializeField] float maxStamina;
    [SerializeField] float jumpStaminaCost;
    [SerializeField] float dashingStaminaCost;
    [SerializeField] float superDashPoint;
    [SerializeField] float hangWallStaminaCost;
    [SerializeField] bool canRegenStamina;
    private float stamina;
    private bool canGaimStamina = true;


    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        boxCollider = GetComponent<BoxCollider2D>();
        trailRenderer = GetComponent<TrailRenderer>();

        stamina = maxStamina;
    }

    private void FixedUpdate()
    {
        CornerCollision();

        MoveCharacter();

        OnWallMovement();
        PlayerFall();

        if (IsGrounded())
        {
            ApplyGroundDeceleration();
            coyoteTimeCounter = coyoteTime;   
        }
        else
        {
            canGaimStamina = true;
            ApplyAirDeceleration();
            coyoteTimeCounter -= Time.deltaTime;
        }

        if (canCornerCorrect) CornerCorrect(rb.velocity.y);

        StaminaController();
    }


    private void Update()
    {
        horizontalMovementInput = GetInput().x;
        verticalMovementInput = GetInput().y;

        movespeed = new Vector2(rb.velocity.x, rb.velocity.y);

        FlipPlayer();
        WallSlider();

        Jump();
        JumpBuffer();
        WallJump();
        Dash();

        if (stamina <= 0)
        {
            wallSlidingSpeed = 10;
        }
        else
        {
            wallSlidingSpeed = 0;
        }
    }

    #region PLAYER MOVEMENT
    private void MoveCharacter()
    {
        rb.AddForce(new Vector2(horizontalMovementInput, 0f) * movementAcceleration);

        if (Mathf.Abs(rb.velocity.x) > maxMovementSpeed && !isDashing)
        {
            rb.velocity = new Vector2(Mathf.Sign(rb.velocity.x) * maxMovementSpeed, rb.velocity.y);
        }
    }
    private void Jump()
    {
        if (jumpBufferCounter > 0f && coyoteTimeCounter > 0f && stamina > 0)
        {
            canGaimStamina = false;
            stamina -= jumpStaminaCost;
            rb.velocity = new Vector2(rb.velocity.x, jumpForce);
            jumpTimeCounter = jumpTime;
            isJumping = true;
            jumpBufferCounter = 0f;
        }
        else
        {
            // DOUBLE JUMP
            if (Input.GetButtonDown("Jump") && canDoubleJump && !IsGrounded() && coyoteTimeCounter <= 0f && enableDoubleJump && stamina > 0)
            {
                stamina -= jumpStaminaCost;
                rb.velocity = new Vector2(rb.velocity.x, jumpForce);
                jumpTimeCounter = jumpTime;
                isJumping = true;
                canDoubleJump = false;
            }
        }

        // HOLD SPACE TO JUMP HIGHER
        if (Input.GetButton("Jump") && isJumping)
        {
            if (jumpTimeCounter > 0)
            {
                rb.velocity = new Vector2(rb.velocity.x, jumpForce);
                jumpTimeCounter -= Time.deltaTime;
            }
            else
            {
                isJumping = false;
            }
        }

        if (Input.GetButtonUp("Jump"))
        {
            isJumping = false;
            coyoteTimeCounter = 0f; // So that player can not spam Space to do double jump with coyote time;
        }

        if (IsGrounded())
            canDoubleJump = true;

        if (HitHead())
            isJumping = false;
    }
    private void JumpBuffer()
    {
        // Player can still jump even jump input is slightly too early
        if (Input.GetButtonDown("Jump"))
            jumpBufferCounter = jumpBufferTimer;
        else
            jumpBufferCounter -= Time.deltaTime;
    }
    private void WallSlider()
    {
        if (IsOnWall() && !IsGrounded() && Input.GetButton("Grab") && stamina > 0)
        {
            isHoldingWall = true;
            isWallSliding = true;
            canDoubleJump = false;
            stamina -= hangWallStaminaCost * Time.deltaTime;
        }
        else if (IsOnWall() && !IsGrounded())
        {
            isHoldingWall = false;
            isWallSliding = true;
            canDoubleJump = false;
            rb.velocity = new Vector2(rb.velocity.x, Mathf.Clamp(rb.velocity.y, -wallSlidingSpeed, float.MaxValue));
        }
        else
        {
            isHoldingWall = false;
            isWallSliding = false;
        }
    }
    private void WallJump()
    {
        if (isWallSliding)
        {
            isWallJumping = false;
            wallJumpDirection = -transform.localScale.x;
            wallJumpCounter = wallJumpTime;
        }
        else
        {
            wallJumpCounter -= Time.deltaTime;
        }

        if (Input.GetButtonDown("Jump") && wallJumpCounter > 0f && stamina > 0 && !isHoldingWall)
        {
            stamina -= jumpStaminaCost;
            isWallJumping = true;
            rb.velocity = new Vector2(wallJumpDirection * jumpForce * 2.5f, jumpForce * 2.2f);

            if (transform.localScale.x != wallJumpDirection)
            {
                Vector3 localScale = transform.localScale;
                localScale.x *= -1f;
                transform.localScale = localScale;
            }
        }
        else if (Input.GetButtonDown("Jump") && stamina > 0 && isHoldingWall)
        {
            isHoldingWall = false;
            stamina -= jumpStaminaCost;
            isWallJumping = true;
            rb.velocity = new Vector2(0f, jumpForce * 1.8f);

        }
    }
    private void OnWallMovement()
    {
        if (isHoldingWall)
        {
            if (verticalMovementInput > 0)
            {
                climbMultiplier = 1f;
                isClimbing = true;
                stamina -= 2;
            }
            else if (verticalMovementInput < 0)
            {
                climbMultiplier = 1.5f;
                isClimbing = true;
            }
            else
            {
                isClimbing = false;
            }
        }
    }
    private void Dash()
    {
        // Getting Dash Input
        if (Input.GetButtonDown("Dash") && canDash)
        {
            isDashing = true;
            canDoubleJump = true; // Reset Doublejump
            canDash = false;
            trailRenderer.emitting = true;
            dashDirection = GetInput();

            if (dashDirection == Vector2.zero)
            {
                dashDirection = new Vector2(transform.localScale.x, 0f);
            }

            if (stamina > 0 && stamina <= superDashPoint)
            {
                dashMultiplier = 1.6f;
            }
            else if (stamina > 0)
            {
                dashMultiplier = 1f;
            }


            stamina -= dashingStaminaCost;

            StartCoroutine(StopDashing());
        }

        if (isDashing)
        {
            rb.velocity = dashDirection.normalized * dashSpeed * dashMultiplier;
        }

        if (stamina > 0)
        {
            canDash = true;
        }
        else
            canDash = false;
    }
    private IEnumerator StopDashing()
    {
        yield return new WaitForSeconds(dashTime);
        trailRenderer.emitting = false;
        isDashing = false;
    }
    private void StaminaController()
    {
        if (IsGrounded())
        {
            if (canGaimStamina && stamina <= maxStamina && canRegenStamina)
                stamina += 500 * Time.deltaTime;
        }

        if (stamina > maxStamina)
            stamina = maxStamina;
        else if (stamina <= 0)
            stamina = 0;
    }
    #endregion

    #region PHYSICS CHECKS
    private void PlayerFall()
    {
        Physics2D.gravity = defaultGravity;

        if (rb.velocity.y < 0 && !isHoldingWall)
        {
            // Player will have a higher gravitation when falling
            rb.velocity += Vector2.up * Physics2D.gravity.y * fallMultiplier * Time.deltaTime;

            // Player's falling speed will be adjusted
            // so that player won't lose controll to the characteer
            if (rb.velocity.y < maxFallingspeed)
            {
                rb.velocity = new Vector2(rb.velocity.x, maxFallingspeed);
            }
        }
        else if (isHoldingWall && isClimbing)
        {
            Physics2D.gravity = Vector2.zero;
            rb.velocity = new Vector2(rb.velocity.x, verticalMovementInput * climbMultiplier) * climbingSpeed;
        }
        else if (isHoldingWall)
        {
            Physics2D.gravity = Vector2.zero;
            rb.velocity = new Vector2(rb.velocity.x, rb.velocity.y);
        }
    }
    private void ApplyGroundDeceleration()
    {
        //Deceleration for player when ON GROUND
        if(isDashing)
        {
            rb.drag = 0f;
        }
        else if (Mathf.Abs(horizontalMovementInput) < 0.4f)
        {
            rb.drag = movementDeceleration;
        }
        else if (ChangingDirection()) // Makes player turning faster
        {
            rb.drag = movementAcceleration * 1.4f;
        }
        else
        {
            rb.drag = 0;
        }
    }
    private void ApplyAirDeceleration()
    {
        //Deceleration for player when ON AIR
        if (!isHoldingWall)
            rb.drag = airDeceleration;
        else
            rb.drag = airDeceleration * 5;
    }
    private void CornerCorrect(float Yvelocity)
    {
        //Push player to the right
        RaycastHit2D hit = Physics2D.Raycast(transform.position - _innerRaycastOffset + Vector3.up * _topRaycastLength, Vector3.left, _topRaycastLength, cornerCorrectLayer);
        if (hit.collider != null)
        {
            float newPos = Vector3.Distance(new Vector3(hit.point.x, transform.position.y, 0f) + Vector3.up * _topRaycastLength,
                transform.position - _edgeRaycastOffset + Vector3.up * _topRaycastLength);
            transform.position = new Vector3(transform.position.x + newPos, transform.position.y, transform.position.z);
            rb.velocity = new Vector2(rb.velocity.x, Yvelocity);
            return;
        }

        //Push player to the left
        hit = Physics2D.Raycast(transform.position + _innerRaycastOffset + Vector3.up * _topRaycastLength, Vector3.right, _topRaycastLength, cornerCorrectLayer);
        if (hit.collider != null)
        {
            float _newPos = Vector3.Distance(new Vector3(hit.point.x, transform.position.y, 0f) + Vector3.up * _topRaycastLength,
                transform.position + _edgeRaycastOffset + Vector3.up * _topRaycastLength);
            transform.position = new Vector3(transform.position.x - _newPos, transform.position.y, transform.position.z);
            rb.velocity = new Vector2(rb.velocity.x, Yvelocity);
        }
    }
    #endregion

    #region CONDITION CHECKS
    private bool ChangingDirection()
    {
        return (rb.velocity.x > 0f && horizontalMovementInput < 0f) || (rb.velocity.x < 0f && horizontalMovementInput > 0f);
    }
    private bool IsGrounded()
    {
        RaycastHit2D raycastHit = Physics2D.BoxCast(boxCollider.bounds.center, boxCollider.bounds.size, 0f, Vector2.down, 0.1f, groundLayer);
        return raycastHit.collider != null;
    }
    private bool IsOnWall()
    {
        RaycastHit2D raycastHit = Physics2D.BoxCast(boxCollider.bounds.center, boxCollider.bounds.size, 0f, new Vector2(transform.localScale.x, 0), 0.1f, wallLayer);
        return raycastHit.collider != null;
    }
    private bool HitHead()
    {
        headDetector = Physics2D.BoxCast(boxCollider.bounds.center, new Vector2(_innerRaycastOffset.x * 2, transform.localScale.y), 0f, Vector2.up, 0.1f, groundLayer);
        return headDetector.collider != null;

    }
    private void CornerCollision()
    {
        //Corner Collisions
        canCornerCorrect = Physics2D.Raycast(transform.position + _edgeRaycastOffset, Vector2.up, _topRaycastLength, cornerCorrectLayer) &&
                           !Physics2D.Raycast(transform.position + _innerRaycastOffset, Vector2.up, _topRaycastLength, cornerCorrectLayer) ||
                           Physics2D.Raycast(transform.position - _edgeRaycastOffset, Vector2.up, _topRaycastLength, cornerCorrectLayer) &&
                           !Physics2D.Raycast(transform.position - _innerRaycastOffset, Vector2.up, _topRaycastLength, cornerCorrectLayer);
    }
    private void FlipPlayer()
    {
        if (horizontalMovementInput > 0.01f)
            transform.localScale = new Vector2(0.5f, 0.65f);

        else if (horizontalMovementInput < -0.01f)
            transform.localScale = new Vector2(-0.5f, 0.65f);
    }
    #endregion

    private Vector2 GetInput()
    {
        return new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;

        //Gizmos.DrawCube(boxCollider.bounds.center, new Vector2(transform.localScale.x, transform.localScale.y/2));     

        //Gizmos.DrawWireCube(boxCollider.bounds.center, new Vector2(_innerRaycastOffset.x * 2, transform.localScale.y));

        ////Corner Check
        //Gizmos.DrawLine(transform.position + _edgeRaycastOffset, transform.position + _edgeRaycastOffset + Vector3.up * _topRaycastLength);
        //Gizmos.DrawLine(transform.position - _edgeRaycastOffset, transform.position - _edgeRaycastOffset + Vector3.up * _topRaycastLength);
        //Gizmos.DrawLine(transform.position + _innerRaycastOffset, transform.position + _innerRaycastOffset + Vector3.up * _topRaycastLength);
        //Gizmos.DrawLine(transform.position - _innerRaycastOffset, transform.position - _innerRaycastOffset + Vector3.up * _topRaycastLength);

        ////Corner Distance Check
        //Gizmos.DrawLine(transform.position - _innerRaycastOffset + Vector3.up * _topRaycastLength,
        //                transform.position - _innerRaycastOffset + Vector3.up * _topRaycastLength + Vector3.left * _topRaycastLength);
        //Gizmos.DrawLine(transform.position + _innerRaycastOffset + Vector3.up * _topRaycastLength,
        //                transform.position + _innerRaycastOffset + Vector3.up * _topRaycastLength + Vector3.right * _topRaycastLength);
    }
}
