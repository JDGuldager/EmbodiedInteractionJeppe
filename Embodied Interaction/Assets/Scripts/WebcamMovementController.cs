using UnityEngine;
using TMPro;

public class WebcamMovementController : MonoBehaviour
{
    public enum MovementState
    {
        Still,
        Flowing,
        Energetic,
        Sudden
    }

    [Header("Sudden State Control")]
    public float suddenStateCooldown = 0.8f;
    public float suddenStateDuration = 0.25f;

    private float suddenCooldownTimer;
    private float suddenStateTimer;

    [Header("Scene Targets")]
    public Light sceneLight;
    public Renderer targetObject;
    public ParticleSystem ambientParticles;
    public ParticleSystem burstParticles;
    public AudioSource audioSource;
    public Camera sceneCamera;

    [Header("UI")]
    public TextMeshProUGUI stateText;

    [Header("Webcam Settings")]
    public int sampleStep = 10;
    public float motionSensitivity = 12f;

    [Header("Movement Descriptor Thresholds")]
    public float smoothing = 0.15f;
    public float suddennessThreshold = 0.025f;
    public float stillnessThreshold = 0.04f;
    public float energeticThreshold = 0.35f;

    [Header("Camera Smooth Zoom")]
    public float minZoom = -2.5f;
    public float maxZoom = -5.5f;
    public float zoomSmoothTime = 0.45f;
    public float stillBreathingAmount = 0.08f;
    public float stillBreathingSpeed = 0.8f;

    [Header("Sudden Camera Pulse")]
    public float pulseFovIncrease = 8f;
    public float fovSmoothSpeed = 5f;

    private WebCamTexture webcam;
    private Color32[] currentPixels;
    private Color32[] previousPixels;

    private float movementEnergy;
    private float previousEnergy;
    private float suddenness;
    private float timeEffort;
    private float stillnessTimer;

    private MovementState currentState;

    private Vector3 cameraOriginalPosition;
    private float cameraOriginalFov;
    private float zoomVelocity;

    private float suddenPulseTimer;
    private float burstCooldown;

  
    void Start()
    {
        webcam = new WebCamTexture();
        webcam.Play();

        if (sceneCamera != null)
        {
            cameraOriginalPosition = sceneCamera.transform.localPosition;
            cameraOriginalFov = sceneCamera.fieldOfView;
        }

        if (audioSource != null)
        {
            audioSource.loop = true;
            audioSource.Play();
        }

        if (ambientParticles != null)
            ambientParticles.Play();
    }

    void Update()
    {
        if (webcam == null || !webcam.didUpdateThisFrame)
            return;

        currentPixels = webcam.GetPixels32();

        if (previousPixels == null || previousPixels.Length != currentPixels.Length)
        {
            previousPixels = new Color32[currentPixels.Length];
            currentPixels.CopyTo(previousPixels, 0);
            return;
        }

        float rawMotion = CalculateFrameDifference(currentPixels, previousPixels);

        previousEnergy = movementEnergy;

        float targetEnergy = rawMotion * motionSensitivity * 0.65f;

        movementEnergy = Mathf.Lerp(
            movementEnergy,
            targetEnergy,
            smoothing
        );

        movementEnergy = Mathf.Clamp01(movementEnergy);

        suddenness = Mathf.Abs(movementEnergy - previousEnergy);

        // Scaled version of suddenness for display and LMA-inspired Time effort.
        timeEffort = Mathf.Clamp01(suddenness * 20f);

        UpdateMovementState();
        ApplyEmbodiedMappings();
        ApplyParticles();
        ApplyCameraResponse();
        UpdateUI();

        currentPixels.CopyTo(previousPixels, 0);

        if (burstCooldown > 0f)
            burstCooldown -= Time.deltaTime;

        if (suddenPulseTimer > 0f)
            suddenPulseTimer -= Time.deltaTime;
    }

    float CalculateFrameDifference(Color32[] current, Color32[] previous)
    {
        float differenceSum = 0f;
        int samples = 0;

        for (int i = 0; i < current.Length; i += sampleStep)
        {
            float currentBrightness =
                (current[i].r + current[i].g + current[i].b) / 765f;

            float previousBrightness =
                (previous[i].r + previous[i].g + previous[i].b) / 765f;

            differenceSum += Mathf.Abs(currentBrightness - previousBrightness);
            samples++;
        }

        return differenceSum / samples;
    }

    void UpdateMovementState()
    {
        if (Time.time < 2f)
        {
            currentState = MovementState.Still;
            return;
        }

        if (suddenCooldownTimer > 0f)
            suddenCooldownTimer -= Time.deltaTime;

        if (suddenStateTimer > 0f)
        {
            suddenStateTimer -= Time.deltaTime;
            currentState = MovementState.Sudden;
            return;
        }

        bool isStill = movementEnergy < stillnessThreshold && timeEffort < 0.2f;
        bool isSudden =
            timeEffort > 0.5f &&
            movementEnergy > 0.12f &&
            suddenCooldownTimer <= 0f;

        bool isEnergetic = movementEnergy > energeticThreshold;
        bool isFlowing = movementEnergy >= stillnessThreshold && movementEnergy <= energeticThreshold;

        if (isStill)
            stillnessTimer += Time.deltaTime;
        else
            stillnessTimer = 0f;

        if (isSudden)
        {
            currentState = MovementState.Sudden;
            suddenPulseTimer = 0.25f;

            suddenStateTimer = suddenStateDuration;
            suddenCooldownTimer = suddenStateCooldown;
        }
        else if (stillnessTimer > 0.3f)
        {
            currentState = MovementState.Still;
        }
        else if (isEnergetic)
        {
            currentState = MovementState.Energetic;
        }
        else if (isFlowing)
        {
            currentState = MovementState.Flowing;
        }
    }

    void ApplyEmbodiedMappings()
    {
        float energy = movementEnergy;

        if (sceneLight != null)
        {
            float targetIntensity = Mathf.Lerp(0.35f, 4.5f, energy);

            sceneLight.intensity = Mathf.Lerp(
                sceneLight.intensity,
                targetIntensity,
                Time.deltaTime * 4f
            );
        }

        if (targetObject != null)
        {
            float targetScale = Mathf.Lerp(0.85f, 1.9f, energy);

            if (currentState == MovementState.Still)
            {
                float breathing = Mathf.Sin(Time.time * 1.2f) * 0.04f;
                targetScale += breathing;
            }

            targetObject.transform.localScale = Vector3.Lerp(
                targetObject.transform.localScale,
                Vector3.one * targetScale,
                Time.deltaTime * 5f
            );

            Color stateColor = GetStateColor();

            targetObject.material.color = Color.Lerp(
                targetObject.material.color,
                stateColor,
                Time.deltaTime * 4f
            );

            float rotationSpeed = Mathf.Lerp(8f, 70f, energy);

            targetObject.transform.Rotate(
                0f,
                rotationSpeed * Time.deltaTime,
                rotationSpeed * 0.35f * Time.deltaTime
            );
        }

        if (audioSource != null)
        {
            audioSource.volume = Mathf.Lerp(
                audioSource.volume,
                Mathf.Lerp(0.08f, 0.75f, energy),
                Time.deltaTime * 8f
            );

            audioSource.pitch = Mathf.Lerp(
                audioSource.pitch,
                Mathf.Lerp(0.75f, 1.35f, energy),
                Time.deltaTime * 8f
            );
        }
    }

    void ApplyParticles()
    {
        if (ambientParticles != null)
        {
            var emission = ambientParticles.emission;

            float erraticAmount = Mathf.Clamp01(timeEffort);
            float particleIntensity = Mathf.Clamp01(movementEnergy + erraticAmount);

            emission.rateOverTime = Mathf.Lerp(4f, 120f, particleIntensity);

            var main = ambientParticles.main;
            main.startSpeed = Mathf.Lerp(0.2f, 3.2f, particleIntensity);
            main.startSize = Mathf.Lerp(0.04f, 0.2f, particleIntensity);
            main.startLifetime = Mathf.Lerp(3.5f, 0.8f, erraticAmount);
            main.startColor = GetStateColor();
        }

        if (
            burstParticles != null &&
            currentState == MovementState.Sudden &&
            burstCooldown <= 0f &&
            Time.time > 2f
        )
        {
            int burstAmount = Mathf.RoundToInt(Mathf.Lerp(12, 45, movementEnergy));
            burstParticles.Emit(burstAmount);
            burstCooldown = 0.05f;
        }
    }

    void ApplyCameraResponse()
    {
        if (sceneCamera == null)
            return;

        float targetZ = Mathf.Lerp(minZoom, maxZoom, movementEnergy);

        float smoothZ = Mathf.SmoothDamp(
            sceneCamera.transform.localPosition.z,
            targetZ,
            ref zoomVelocity,
            zoomSmoothTime
        );

        if (currentState == MovementState.Still)
        {
            smoothZ += Mathf.Sin(Time.time * stillBreathingSpeed) * stillBreathingAmount;
        }

        sceneCamera.transform.localPosition = new Vector3(
            cameraOriginalPosition.x,
            cameraOriginalPosition.y,
            smoothZ
        );

        float targetFov = cameraOriginalFov;

        if (suddenPulseTimer > 0f)
            targetFov += pulseFovIncrease;

        sceneCamera.fieldOfView = Mathf.Lerp(
            sceneCamera.fieldOfView,
            targetFov,
            Time.deltaTime * fovSmoothSpeed
        );
    }

    void UpdateUI()
    {
        if (stateText == null)
            return;

        string lmaWeight = movementEnergy > energeticThreshold ? "Strong" : "Light";
        string lmaTime = timeEffort > 0.5f ? "Sudden" : "Sustained";
        string lmaFlow = currentState == MovementState.Flowing ? "Free" : "Bound";

        stateText.text =
            "LMA-inspired state: " + currentState + "\n" +
            "Weight: " + movementEnergy.ToString("F2") + " / " + lmaWeight + "\n" +
            "Time: " + timeEffort.ToString("F2") + " / " + lmaTime + "\n" +
            "Flow: " + lmaFlow;

        stateText.color = GetStateColor();
    }

    Color GetStateColor()
    {
        switch (currentState)
        {
            case MovementState.Still:
                return new Color(0.25f, 0.45f, 1f);

            case MovementState.Flowing:
                return new Color(0.2f, 1f, 0.85f);

            case MovementState.Energetic:
                return new Color(1f, 0.65f, 0.15f);

            case MovementState.Sudden:
                return new Color(1f, 0.15f, 0.08f);

            default:
                return Color.white;
        }
    }


    void OnDestroy()
    {
        if (webcam != null)
            webcam.Stop();
    }
}