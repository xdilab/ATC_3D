using System.IO;
using System.Threading;
using System.Collections.Generic;
using UnityEngine;

public class CaptureService : MonoBehaviour
{
    [Header("Which camera to capture")]
    public Camera sourceCamera;          // assign a rig/Base camera, or leave null to follow MainCamera
    public bool followMainCamera = true; // if true, tracks Camera.main at runtime (CameraDirector can tag the active rig)

    [Header("Capture resolution (even numbers)")]
    public int width = 1280;
    public int height = 720;
    public int targetFps = 30;

    [Header("Pre-roll buffer")]
    public bool enablePreRoll = true;
    public float preRollSec = 5f;                 // amount to include BEFORE capture starts

    [Header("Encode to MP4 (ffmpeg)")]
    public bool encodeOnStop = true;
    public string ffmpegPath = "ffmpeg";          // set full path if not on PATH
    public string outputName = "incident.mp4";
    [Range(0,51)] public int crf = 23;
    public string preset = "veryfast";
    public bool deleteFramesAfterEncode = true;

    // Internal state
    bool capturing = false;
    bool preDumped = false;
    string currentId;
    float nextTime;
    int frameIdx;

    RenderTexture rt;
    Texture2D scratch;

    // Circular buffer for pre-roll (encoded PNG bytes)
    Queue<byte[]> preBuf = new Queue<byte[]>();
    int preBufMaxFrames => Mathf.Max(1, Mathf.CeilToInt(preRollSec * targetFps));

    void OnEnable()
    {
        // enforce even dims
        if ((width & 1) == 1)  width++;
        if ((height & 1) == 1) height++;
        Application.targetFrameRate = Mathf.Max(Application.targetFrameRate, targetFps);

        // allocate buffers
        rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
        rt.wrapMode = TextureWrapMode.Clamp;
        rt.filterMode = FilterMode.Bilinear;
        rt.Create();

        scratch = new Texture2D(width, height, TextureFormat.RGB24, false, false);

        nextTime = Time.time;
    }

    void OnDisable()
    {
        if (rt) { rt.Release(); Destroy(rt); }
        if (scratch) Destroy(scratch);
        preBuf.Clear();
    }

    public void StartCapture(string incidentId)
    {
        if (capturing && currentId == incidentId) return;
        currentId = incidentId;
        frameIdx = 0;
        capturing = true;
        preDumped = false; // will dump pre-roll frames on the first tick

        string dir = Dir(currentId);
        Directory.CreateDirectory(dir);

        // Use an adjusted "startSimSec" that accounts for pre-roll frames
        float startSim = Time.time - (enablePreRoll ? Mathf.Min(preBuf.Count, preBufMaxFrames) / (float)targetFps : 0f);
        File.WriteAllText(Path.Combine(dir, "manifest.json"),
            $"{{\"incidentId\":\"{incidentId}\",\"fps\":{targetFps},\"startSimSec\":{startSim:F3}}}");

        Debug.Log($"[CaptureService] START {incidentId} → {dir}");
    }

    public void StopCapture(string incidentId)
    {
        if (!capturing || currentId != incidentId) return;
        capturing = false;
        Debug.Log($"[CaptureService] STOP {incidentId}");

        if (encodeOnStop)
        {
            string dir = Dir(incidentId);
            ThreadPool.QueueUserWorkItem(_ => EncodeToMp4(dir, targetFps, outputName, deleteFramesAfterEncode));
        }
    }

    void LateUpdate()
    {
        // tick at targetFps
        if (Time.time < nextTime) return;
        nextTime += 1f / Mathf.Max(1, targetFps);

        // pick camera
        if (followMainCamera || !sourceCamera) sourceCamera = Camera.main;
        if (!sourceCamera) return;

        // Render this camera into our RT
        var prevTarget = sourceCamera.targetTexture;
        sourceCamera.targetTexture = rt;
        sourceCamera.Render();
        sourceCamera.targetTexture = prevTarget;

        // Readback
        var prevActive = RenderTexture.active;
        RenderTexture.active = rt;
        scratch.ReadPixels(new Rect(0, 0, width, height), 0, 0, false);
        scratch.Apply(false);
        RenderTexture.active = prevActive;

        // Encode current frame
        byte[] png = scratch.EncodeToPNG();

        if (!capturing)
        {
            // Maintain circular pre-roll buffer in memory
            if (enablePreRoll)
            {
                preBuf.Enqueue(png);
                while (preBuf.Count > preBufMaxFrames) preBuf.Dequeue();
            }
            return; // not recording yet
        }

        // First capture tick: dump pre-roll frames to disk
        if (!preDumped)
        {
            string dir = Dir(currentId);
            int preCount = enablePreRoll ? Mathf.Min(preBuf.Count, preBufMaxFrames) : 0;

            // Write oldest → newest so playback is chronological
            int idxStart = 0;
            if (preCount > 0)
            {
                var tmp = preBuf.ToArray();
                int start = tmp.Length - preCount;
                for (int i = start; i < tmp.Length; i++)
                {
                    File.WriteAllBytes(Path.Combine(dir, $"frames_{idxStart:D4}.png"), tmp[i]);
                    idxStart++;
                }
            }
            frameIdx = idxStart;
            preDumped = true;
        }

        // Write current frame
        string path = Path.Combine(Dir(currentId), $"frames_{frameIdx:D4}.png");
        File.WriteAllBytes(path, png);
        frameIdx++;
    }

    /// <summary>
    /// Immediately stops whatever incident is active, kicking off encode.
    /// </summary>
    public void ForceStopActive()
    {
        if (capturing && !string.IsNullOrEmpty(currentId))
        {
            Debug.Log($"[CaptureService] FORCE STOP {currentId}");
            StopCapture(currentId);
        }
    }

    void Update()
    {
        // Press Space to force-stop the current capture
        if (Input.GetKeyDown(KeyCode.Space))
        {
            ForceStopActive();
        }
    }

    static string Dir(string id) => Path.Combine(Application.persistentDataPath, "Captures", id);

    void EncodeToMp4(string dir, int fps, string outfile, bool cleanup)
    {
        string inputPattern = Path.Combine(dir, "frames_%04d.png");
        string outputPath   = Path.Combine(dir, outfile);

        // Ensure H.264-legal even dimensions (kept for safety)
        string vf = "scale=ceil(iw/2)*2:ceil(ih/2)*2";

        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = ffmpegPath,
            Arguments =
                $"-y -framerate {fps} -i \"{inputPattern}\" " +
                $"-vf \"{vf}\" -c:v libx264 -pix_fmt yuv420p " +
                $"-preset {preset} -crf {crf} \"{outputPath}\"",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true
        };

        try
        {
            using (var p = System.Diagnostics.Process.Start(psi))
            {
                string stderr = p.StandardError.ReadToEnd();  // ffmpeg logs on stderr
                p.WaitForExit();
                Debug.Log($"[CaptureService] ffmpeg exit {p.ExitCode} → {outputPath}\n{stderr}");

                if (p.ExitCode == 0 && cleanup)
                {
                    foreach (var f in Directory.GetFiles(dir, "frames_*.png")) File.Delete(f);
                    Debug.Log("[CaptureService] Deleted PNG frames after encoding.");
                }
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[CaptureService] Failed to run ffmpeg. Set 'ffmpegPath' or install ffmpeg.\n{ex.Message}");
        }
    }
}
