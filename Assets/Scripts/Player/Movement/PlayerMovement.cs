using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float gravity = -20f;
    [SerializeField] private float jumpHeight = 1.8f;
    [SerializeField] private float groundedSnapVelocity = -2f;
    [SerializeField] private float maxFallSpeed = -30f;

    [Header("Visual / Animation")]
	[SerializeField] private SpriteRenderer spriteRenderer;
	[SerializeField] private Animator animator;

    private CharacterController characterController;
    private Vector3 velocity;
    private float moveX;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();

        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }
		if (spriteRenderer == null)
		{
    		spriteRenderer = GetComponentInChildren<SpriteRenderer>();
		}
    }

    private void Update()
    {
        ReadInput();
        MovePlayer();
        FlipVisual();
        UpdateAnimator();
    }

    private void ReadInput()
    {
        moveX = 0f;

        if (Input.GetKey(KeyCode.A))
        {
            moveX -= 1f;
        }

        if (Input.GetKey(KeyCode.D))
        {
            moveX += 1f;
        }
    }

    private void MovePlayer()
    {
        bool isGrounded = characterController.isGrounded;

        if (isGrounded && velocity.y < 0f)
        {
            velocity.y = groundedSnapVelocity;
        }

        if (IsJumpPressed() && isGrounded)
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }

        velocity.y += gravity * Time.deltaTime;

        if (velocity.y < maxFallSpeed)
        {
            velocity.y = maxFallSpeed;
        }

        Vector3 horizontalMove = new Vector3(moveX * moveSpeed, 0f, 0f);
        Vector3 verticalMove = new Vector3(0f, velocity.y, 0f);

        CollisionFlags flags = characterController.Move((horizontalMove + verticalMove) * Time.deltaTime);

        if ((flags & CollisionFlags.Below) != 0 && velocity.y < 0f)
        {
            velocity.y = groundedSnapVelocity;
        }
    }

    private void FlipVisual()
	{
    	if (spriteRenderer == null || Mathf.Abs(moveX) < 0.01f)
    	{
        	return;
    	}
    	spriteRenderer.flipX = moveX < 0f;
	}

    private void UpdateAnimator()
    {
        if (animator == null)
        {
            return;
        }

        animator.SetFloat("Speed", Mathf.Abs(moveX));
        animator.SetBool("IsGrounded", characterController.isGrounded);
        animator.SetFloat("YVelocity", velocity.y);
    }

    private bool IsJumpPressed()
    {
        return Input.GetKeyDown(KeyCode.Space) || Input.GetButtonDown("Jump");
    }
}