using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

// The root sits on the hinge with world-up as its local Y. A door is selected
// like a grab, but follows only the hand's angular motion, never its position.
[RequireComponent(typeof(Rigidbody))]
public sealed class StoreFridgeDoor : XRBaseInteractable
{
    [SerializeField] private float minAngle = -105f;
    [SerializeField] private float maxAngle;
    [SerializeField] private float rotationSpeed = 240f;
    private Quaternion closedRotation;
    private Rigidbody body;
    private IXRSelectInteractor hand;
    private Vector3 previousDirection;
    private float targetAngle;
    public float Angle { get; private set; }

    protected override void Awake()
    {
        base.Awake();
        body = GetComponent<Rigidbody>();
        closedRotation = transform.localRotation;
    }

    protected override void OnSelectEntered(SelectEnterEventArgs args)
    {
        base.OnSelectEntered(args);
        hand = args.interactorObject;
        previousDirection = HandDirection();
        targetAngle = Angle;
    }

    protected override void OnSelectExited(SelectExitEventArgs args)
    {
        hand = null;
        base.OnSelectExited(args);
    }

    private Vector3 HandDirection()
    {
        return Vector3.ProjectOnPlane(hand.GetAttachTransform(this).position - transform.position, transform.up);
    }

    private void FixedUpdate()
    {
        if (hand != null)
        {
            Vector3 direction = HandDirection();
            if (direction.sqrMagnitude > 0.0025f && previousDirection.sqrMagnitude > 0.0025f)
                targetAngle = Mathf.Clamp(targetAngle + Vector3.SignedAngle(previousDirection, direction, transform.up), minAngle, maxAngle);
            previousDirection = direction;
        }
        Angle = Mathf.MoveTowards(Angle, targetAngle, rotationSpeed * Time.fixedDeltaTime);
        Quaternion local = closedRotation * Quaternion.AngleAxis(Angle, Vector3.up);
        body.MoveRotation(transform.parent == null ? local : transform.parent.rotation * local);
    }
}
