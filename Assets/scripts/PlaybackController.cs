using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

/// <summary>
/// Global timebase controller for the digital-twin playback.
/// Holds play/pause state, speed factor, scrub logic, and updates the HUD clock.
/// Other scripts read the public static fields but never modify them directly.
/// </summary>
public class PlaybackController : MonoBehaviour
{
    /* ───────────── Public static simulation state (read-only for others) ───────────── */

    /// <summary>Playback speed multiplier. 1 = real-time.</summary>
    public static float speed        = 1f;

    /// <summary>True when user pressed Pause.</summary>
    public static bool  paused       = false;

    /// <summary>True while the user is dragging the time-scrubber.</summary>
    public static bool  scrubbing    = false;

    /// <summary>The globally visible simulation clock (seconds since midnight).</summary>
    public static float simTime      = 0f;

    /// <summary>Lower/upper bounds of the CSV day, set by <see cref="SetTimeBounds"/>.</summary>
    public static float tMin = 0f, tMax = 86_400f;

    /* -------------------------------------------------------------------- */
    static PlaybackController inst;   // singleton (scene has exactly one)

    /* ───────────── Inspector-wired UI elements ───────────── */

    [Header("UI")]
    public Slider  speedSlider;       // 0.1 – 10
    public Slider  timeScrubber;      // 0 – 1 (percent of tMin..tMax)
    public Button  playButton;
    public Button  pauseButton;
    public TMP_Text clockLabel;       // HUD clock

    [Header("Simulation Day  (yyyy-MM-dd)")]
    [Tooltip("Calendar date that matches the CSV DT-Time column (UTC or local)")]
    public string simulationDay = "2023-05-01";

    DateTime simDay;                  // midnight of simulationDay

    /* ─────────────────────────────── lifecycle ───────────────────────────── */

    void Awake()  => inst = this;

    void Start()
    {
        simDay = DateTime.Parse(simulationDay);

        /* speed slider */
        speedSlider.onValueChanged.AddListener(v => speed = v);

        /* play / pause */
        playButton .onClick.AddListener(()=> paused = false);
        pauseButton.onClick.AddListener(()=> paused = true);

        /* scrub bar setup */
        timeScrubber.minValue = 0f;
        timeScrubber.maxValue = 1f;
        timeScrubber.wholeNumbers = false;

        timeScrubber.onValueChanged.AddListener(v =>
        {
            scrubbing = true;
            paused    = true;
            simTime   = Mathf.Lerp(tMin, tMax, v);
        });

        /* resume automatically when pointer released */
        var trg = timeScrubber.gameObject.AddComponent<
                  UnityEngine.EventSystems.EventTrigger>();
        var e   = new UnityEngine.EventSystems.EventTrigger.Entry
        {
            eventID = UnityEngine.EventSystems.EventTriggerType.PointerUp
        };
        e.callback.AddListener(_ => { scrubbing = false; paused = false; });
        trg.triggers.Add(e);
    }

    void Update()
    {
        /* advance clock when playing normally */
        if (!paused && !scrubbing)
            simTime = Mathf.Clamp(simTime + Time.deltaTime * speed, tMin, tMax);

        /* keep slider in sync (unless user is dragging) */
        if (!scrubbing && timeScrubber)
        {
            float pct = Mathf.InverseLerp(tMin, tMax, simTime);
            timeScrubber.SetValueWithoutNotify(pct);
        }

        /* HUD clock text */
        if (clockLabel)
            clockLabel.text =
                simDay.AddSeconds(simTime).ToString("yyyy-MM-dd  HH:mm:ss");
    }

    /// <summary>
    /// Called exactly once by <see cref="FlightLoader"/> after it parses the CSV,
    /// so this controller knows the playable window.
    /// </summary>
    public static void SetTimeBounds(float minSec, float maxSec)
    {
        tMin = minSec;
        tMax = maxSec;
        simTime = tMin;
        if (inst) inst.timeScrubber.SetValueWithoutNotify(0f);
    }
}
