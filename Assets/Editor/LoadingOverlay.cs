using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Shows a fullscreen black overlay with a rotating spinner while a new level loads.
/// Attach to the "LoadingOverlay" Canvas created by SceneSetupUtility.
/// </summary>
public class LoadingOverlay : MonoBehaviour
{
    private CanvasGroup _cg;
    private Image _spinner;
    public float FadeDuration = 0.3f;
    public float SpinSpeed = 180f; // degrees per second

    private void Awake()
    {
        _cg = gameObject.AddComponent<CanvasGroup>();
        _cg.interactable = _cg.blocksRaycasts = false;
        _cg.alpha = 0f;

        // Create a simple spinner UI element (white square for demo)
        var go = new GameObject("Spinner", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(transform, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(80, 80);
        _spinner = go.GetComponent<Image>();
        _spinner.color = Color.white;
    }

    private void Update()
    {
        if (_spinner != null)
            _spinner.rectTransform.Rotate(Vector3.forward, SpinSpeed * Time.unscaledDeltaTime);
    }

    public void Show()
    {
        StopAllCoroutines();
        StartCoroutine(FadeTo(1f));
    }

    public void Hide()
    {
        StopAllCoroutines();
        StartCoroutine(FadeTo(0f));
    }

    private System.Collections.IEnumerator FadeTo(float target)
    {
        float start = _cg.alpha;
        float t = 0f;
        while (t < FadeDuration)
        {
            t += Time.unscaledDeltaTime;
            _cg.alpha = Mathf.Lerp(start, target, t / FadeDuration);
            yield return null;
        }
        _cg.alpha = target;
    }
}

