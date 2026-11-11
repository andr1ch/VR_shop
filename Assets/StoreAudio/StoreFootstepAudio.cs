using UnityEngine;

public sealed class StoreFootstepAudio : MonoBehaviour
{
    [SerializeField] private Transform listener;
    [SerializeField] private float stepDistance = 0.65f;
    [SerializeField] private float minimumStepInterval = 0.28f;
    [SerializeField] private float teleportThreshold = 1.5f;

    private Vector3 previousPosition;
    private float distanceSinceStep;
    private float lastStepTime;
    private bool alternate;

    private void Start()
    {
        if (listener != null) previousPosition = listener.position;
    }

    private void Update()
    {
        if (listener == null || AudioManager.Instance == null) return;
        Vector3 current = listener.position;
        Vector2 a = new Vector2(previousPosition.x, previousPosition.z);
        Vector2 b = new Vector2(current.x, current.z);
        float delta = Vector2.Distance(a, b);
        previousPosition = current;
        if (delta > teleportThreshold)
        {
            distanceSinceStep = 0f;
            return;
        }
        distanceSinceStep += delta;
        if (distanceSinceStep < stepDistance || Time.time - lastStepTime < minimumStepInterval) return;
        distanceSinceStep = 0f;
        lastStepTime = Time.time;
        AudioManager.Instance.PlayAtPosition(alternate ? "step2" : "step1", current - Vector3.up * 1.3f);
        alternate = !alternate;
    }
}
