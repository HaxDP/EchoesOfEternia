using UnityEngine;

public class PortallEnd : MonoBehaviour
{
    [Header("Portal")]
    [SerializeField] private Animator portalAnimator;
    [SerializeField] private string portalEndStateName = "PortallEnd";

    [Header("Player")]
    [SerializeField] private GameObject playerToHide;

    private bool playerInside;
    private bool animationStarted;

    private void Awake()
    {
        if (portalAnimator == null)
        {
            portalAnimator = GetComponent<Animator>();
        }
    }

    private void Update()
    {
        if (!playerInside || animationStarted)
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.E))
        {
            StartEndPortalAnimation();
        }
    }

    private void StartEndPortalAnimation()
    {
        animationStarted = true;

        if (playerToHide != null)
        {
            playerToHide.SetActive(false);
        }

        if (portalAnimator != null)
        {
            portalAnimator.Play(portalEndStateName, 0, 0f);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerInside = true;

            if (playerToHide == null)
            {
                playerToHide = other.gameObject;
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerInside = false;
        }
    }
}