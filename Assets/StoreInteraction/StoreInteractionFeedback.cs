using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

[RequireComponent(typeof(XRBaseInteractable))]
public sealed class StoreInteractionFeedback : MonoBehaviour
{
    [SerializeField] private Color hoverColor = new Color(0.2f, 0.85f, 1f, 1f);
    [SerializeField] private string pickupSound = "pickup";
    private XRBaseInteractable interactable;
    private Renderer[] renderers;
    private MaterialPropertyBlock[] original;
    private MaterialPropertyBlock block;
    private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    private void Awake()
    {
        interactable = GetComponent<XRBaseInteractable>();
        renderers = GetComponentsInChildren<Renderer>();
        block = new MaterialPropertyBlock();
    }

    private void OnEnable()
    {
        interactable.firstHoverEntered.AddListener(OnHoverEntered);
        interactable.lastHoverExited.AddListener(OnHoverExited);
        interactable.selectEntered.AddListener(OnSelected);
    }

    private void OnDisable()
    {
        interactable.firstHoverEntered.RemoveListener(OnHoverEntered);
        interactable.lastHoverExited.RemoveListener(OnHoverExited);
        interactable.selectEntered.RemoveListener(OnSelected);
        Restore();
    }

    private void OnHoverEntered(HoverEnterEventArgs args)
    {
        if (original != null) return;
        original = new MaterialPropertyBlock[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null) continue;
            original[i] = new MaterialPropertyBlock();
            renderers[i].GetPropertyBlock(original[i]);
            renderers[i].GetPropertyBlock(block);
            block.SetColor(BaseColor, hoverColor);
            block.SetColor(ColorId, hoverColor);
            renderers[i].SetPropertyBlock(block);
        }
    }

    private void OnHoverExited(HoverExitEventArgs args) => Restore();

    private void OnSelected(SelectEnterEventArgs args)
    {
        if (!string.IsNullOrEmpty(pickupSound) && AudioManager.Instance != null)
            AudioManager.Instance.PlayAtPosition(pickupSound, transform.position);
    }

    private void Restore()
    {
        if (original == null) return;
        for (int i = 0; i < renderers.Length; i++)
            if (renderers[i] != null) renderers[i].SetPropertyBlock(original[i]);
        original = null;
    }
}
