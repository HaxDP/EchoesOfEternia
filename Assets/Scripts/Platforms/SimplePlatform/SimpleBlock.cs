using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class SimpleBlock : MonoBehaviour
{
	[Header("Block")]
	[SerializeField] private Vector3 blockSize = new Vector3(1f, 1f, 1f);
	[SerializeField] private bool isSolid = true;
	[SerializeField] private bool forceStaticPhysics = true;
	[SerializeField] private Color gizmoColor = new Color(0.2f, 0.8f, 0.3f, 0.35f);

	private BoxCollider blockCollider;
	private Rigidbody blockRigidbody;

	private void Awake()
	{
		blockCollider = GetComponent<BoxCollider>();
		blockRigidbody = GetComponent<Rigidbody>();
		ApplyBlockSettings();
	}

	private void OnValidate()
	{
		if (blockCollider == null)
		{
			blockCollider = GetComponent<BoxCollider>();
		}

		if (blockRigidbody == null)
		{
			blockRigidbody = GetComponent<Rigidbody>();
		}

		ApplyBlockSettings();
	}

	private void ApplyBlockSettings()
	{
		if (blockCollider == null)
		{
			return;
		}

		blockCollider.size = blockSize;
		blockCollider.isTrigger = !isSolid;

		if (forceStaticPhysics && blockRigidbody != null)
		{
			blockRigidbody.isKinematic = true;
			blockRigidbody.useGravity = false;
			blockRigidbody.constraints = RigidbodyConstraints.FreezeAll;
		}
	}

	private void OnDrawGizmos()
	{
		Gizmos.color = gizmoColor;
		Gizmos.matrix = transform.localToWorldMatrix;
		Gizmos.DrawCube(Vector3.zero, blockSize);
	}
}
