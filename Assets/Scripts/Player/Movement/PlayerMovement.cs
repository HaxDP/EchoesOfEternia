using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
	[Header("Movement")]
	[SerializeField] private float moveSpeed = 5f;
	[SerializeField] private float gravity = -9.81f;
	[SerializeField] private float jumpHeight = 1.5f;
	[SerializeField] private float groundedSnapVelocity = -2f;
	[SerializeField] private float maxFallSpeed = -30f;

	private CharacterController characterController;
	private Vector3 velocity;

	private void Awake()
	{
		characterController = GetComponent<CharacterController>();
	}

	private void Update()
	{
		HandleMovementAndJump();
	}

	private void HandleMovementAndJump()
	{
		var moveX = 0f;
		if (Input.GetKey(KeyCode.A))
		{
			moveX -= 1f;
		}
		if (Input.GetKey(KeyCode.D))
		{
			moveX += 1f;
		}

		var moveZ = Input.GetKey(KeyCode.W) ? 1f : 0f;

		var flatRight = transform.right;
		flatRight.y = 0f;
		flatRight.Normalize();

		var flatForward = transform.forward;
		flatForward.y = 0f;
		flatForward.Normalize();

		var isGrounded = characterController.isGrounded;

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

		var horizontal = (flatRight * moveX + flatForward * moveZ) * moveSpeed;
		var motion = new Vector3(horizontal.x, velocity.y, horizontal.z);
		var flags = characterController.Move(motion * Time.deltaTime);

		if ((flags & CollisionFlags.Below) != 0 && velocity.y < 0f)
		{
			velocity.y = groundedSnapVelocity;
		}
	}

	private bool IsJumpPressed()
	{
		return Input.GetKeyDown(KeyCode.Space) || Input.GetButtonDown("Jump");
	}
}
