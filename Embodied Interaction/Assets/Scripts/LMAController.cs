using UnityEngine;
using UnityEngine.Video;
using TMPro;

public class LMAController : MonoBehaviour
{
    public enum MovementClipType
    {
        TaiChi,
        Ballet,
        Breakdance
    }

    [Header("Video")]
    public VideoPlayer videoPlayer;
    public VideoClip taiChiClip;
    public VideoClip balletClip;
    public VideoClip breakdanceClip;

    [Header("Scene Targets")]
    public Light sceneLight;
    public Renderer sphereRenderer;
    public ParticleSystem particles;
    public Camera sceneCamera;

    [Header("UI")]
    public TextMeshProUGUI stateText;

    [Header("Current LMA-Inspired Values")]
    [Range(0f, 1f)] public float weight;     // light to strong
    [Range(0f, 1f)] public float time;       // sustained to sudden
    [Range(0f, 1f)] public float flow;       // bound to free
    [Range(0f, 1f)] public float space;      // indirect to direct

    private Vector3 originalCameraPosition;
    private float particleTimer;

    void Start()
    {
        if (sceneCamera != null)
            originalCameraPosition = sceneCamera.transform.localPosition;

        SelectTaiChi();
    }

    void Update()
    {
        ApplyLMAResponse();
        UpdateUI();
    }

    public void SelectTaiChi()
    {
        SetClip(MovementClipType.TaiChi, taiChiClip);

        // Tai Chi: light, sustained, free, indirect
        weight = 0.25f;
        time = 0.15f;
        flow = 0.85f;
        space = 0.35f;
    }

    public void SelectBallet()
    {
        SetClip(MovementClipType.Ballet, balletClip);

        // Ballet: medium-strong, sustained, bound/controlled, direct
        weight = 0.55f;
        time = 0.35f;
        flow = 0.35f;
        space = 0.85f;
    }

    public void SelectBreakdance()
    {
        SetClip(MovementClipType.Breakdance, breakdanceClip);

        // Breakdance: strong, sudden, alternating/free, direct
        weight = 0.9f;
        time = 0.9f;
        flow = 0.65f;
        space = 0.75f;
    }

    void SetClip(MovementClipType type, VideoClip clip)
    {
        if (videoPlayer == null || clip == null)
            return;

        videoPlayer.clip = clip;
        videoPlayer.isLooping = true;
        videoPlayer.Play();
    }

    void ApplyLMAResponse()
    {
        float intensity = Mathf.Clamp01((weight + time) / 2f);

        if (sceneLight != null)
        {
            sceneLight.intensity = Mathf.Lerp(0.5f, 5f, intensity);
        }

        if (sphereRenderer != null)
        {
            float scale = Mathf.Lerp(0.8f, 2.0f, weight);
            sphereRenderer.transform.localScale = Vector3.Lerp(
                sphereRenderer.transform.localScale,
                Vector3.one * scale,
                Time.deltaTime * 4f
            );

            Color lightSustained = new Color(0.2f, 0.6f, 1f);
            Color controlledDirect = new Color(1f, 0.9f, 0.55f);
            Color strongSudden = new Color(1f, 0.2f, 0.05f);

            Color targetColor = Color.Lerp(lightSustained, strongSudden, intensity);
            targetColor = Color.Lerp(targetColor, controlledDirect, space * (1f - flow));

            sphereRenderer.material.color = Color.Lerp(
                sphereRenderer.material.color,
                targetColor,
                Time.deltaTime * 3f
            );

            sphereRenderer.transform.Rotate(
                flow * 20f * Time.deltaTime,
                space * 40f * Time.deltaTime,
                time * 60f * Time.deltaTime
            );
        }

        if (particles != null)
        {
            particleTimer += Time.deltaTime;

            float interval = Mathf.Lerp(1.5f, 0.15f, time);

            if (particleTimer > interval)
            {
                int amount = Mathf.RoundToInt(Mathf.Lerp(2, 30, weight));
                particles.Emit(amount);
                particleTimer = 0f;
            }
        }

        if (sceneCamera != null)
        {
            Vector3 targetPos = originalCameraPosition + new Vector3(
                Mathf.Lerp(-0.4f, 0.4f, space),
                0f,
                -Mathf.Lerp(0f, 1.5f, intensity)
            );

            sceneCamera.transform.localPosition = Vector3.Lerp(
                sceneCamera.transform.localPosition,
                targetPos,
                Time.deltaTime * 2f
            );

            if (time > 0.75f)
            {
                Vector3 shake = Random.insideUnitSphere * 0.04f * time;
                shake.z = 0f;
                sceneCamera.transform.localPosition += shake;
            }
        }
    }

    void UpdateUI()
    {
        if (stateText == null)
            return;

        stateText.text =
            "LMA-Inspired Effort Profile\n" +
            "Weight: " + weight.ToString("F2") + "\n" +
            "Time: " + time.ToString("F2") + "\n" +
            "Flow: " + flow.ToString("F2") + "\n" +
            "Space: " + space.ToString("F2");
    }
}