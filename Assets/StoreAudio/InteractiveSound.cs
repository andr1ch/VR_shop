using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public sealed class InteractiveSound : MonoBehaviour
{
    [SerializeField] private string hoverSound;
    [SerializeField] private string activateSound;
    [SerializeField] private string releaseSound;
    [SerializeField] private bool useObjectPosition = true;
    private XRBaseInteractable interactable;

    private void Awake() => interactable = GetComponent<XRBaseInteractable>();

    private void OnEnable()
    {
        if (interactable == null) return;
        interactable.hoverEntered.AddListener(OnHoverEntered);
        interactable.selectEntered.AddListener(OnSelectEntered);
        interactable.selectExited.AddListener(OnSelectExited);
    }

    private void OnDisable()
    {
        if (interactable == null) return;
        interactable.hoverEntered.RemoveListener(OnHoverEntered);
        interactable.selectEntered.RemoveListener(OnSelectEntered);
        interactable.selectExited.RemoveListener(OnSelectExited);
    }

    private void OnHoverEntered(HoverEnterEventArgs _) => OnHover();
    private void OnSelectEntered(SelectEnterEventArgs _) => OnActivate();
    private void OnSelectExited(SelectExitEventArgs _) => OnRelease();

    public void OnHover() => PlaySound(hoverSound);
    public void OnActivate() => PlaySound(activateSound);
    public void OnRelease() => PlaySound(releaseSound);

    private void PlaySound(string soundName)
    {
        if (string.IsNullOrEmpty(soundName) || AudioManager.Instance == null) return;
        if (useObjectPosition) AudioManager.Instance.PlayAtPosition(soundName, transform.position);
        else AudioManager.Instance.Play(soundName);
    }
}
