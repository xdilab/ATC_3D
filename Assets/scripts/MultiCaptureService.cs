using System.IO;
using System.Threading;
using System.Collections.Generic;
using UnityEngine;

public class MultiCaptureService : MonoBehaviour
{
    [Header("Capture resolution (even numbers)")]
    public int width = 1280;
    public int height = 720;
    public int targetFps = 30;

    [Header("Pre-roll")]
    public bool enablePreRoll = true;
    public float preRollSec = 5f;

    [Header("Auto-stop if no updates (safety)")]
    public bool enableIdleTimeout = true;
    public float autoStopIfIdleSec = 6f;   // if no StartOrUpdate for this many seconds, encode it

    [Header("Encode to MP4 (ffmpeg)")]
    public bool encodeOnStop = true;
    public string ffmpegPath = "ffmpeg";
    public string outputName = "incident.mp4";
    [Range(0,51)] public int crf = 23;
    public string preset = "veryfast";
    public bool deleteFramesAfterEncode = true;

    class Recorder
    {
        public string id;
        public Camera camera;
        public RenderTexture rt;
        public Texture2D scratch;
        public Queue<byte[]> preBuf = new Queue<byte[]>();
        public int frameIdx = 0;
        public float nextTime = 0f;
        public bool started = false;
        public bool preDumped = false;
        public float stopAt = -1f;
        public string dir;
        public float startSimLogged = 0f;
        public float lastUpdate = 0f;  // NEW: time of last StartOrUpdate
        public bool encodingKicked = false;
    }

    readonly Dictionary<string, Recorder> recs = new();

    void OnEnable()
    {
        if ((width & 1) == 1)  width++;
        if ((height & 1) == 1) height++;
        Application.targetFrameRate = Mathf.Max(Application.targetFrameRate, targetFps);
    }

    void OnDisable()
    {
        foreach (var r in recs.Values)
        {
            if (r.rt) { r.rt.Release(); Object.Destroy(r.rt); }
            if (r.scratch) Object.Destroy(r.scratch);
        }
        recs.Clear();
    }

    public void StartOrUpdate(string incidentId, Camera cam)
    {
        if (string.IsNullOrEmpty(incidentId) || cam == null) return;

        if (!recs.TryGetValue(incidentId, out var rec))
        {
            rec = new Recorder { id = incidentId, camera = cam };
            rec.dir = Path.Combine(Application.persistentDataPath, "Captures", incidentId);
            Directory.CreateDirectory(rec.dir);

            rec.rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            rec.rt.wrapMode = TextureWrapMode.Clamp;
            rec.rt.filterMode = FilterMode.Bilinear;
            rec.rt.Create();

            rec.scratch = new Texture2D(width, height, TextureFormat.RGB24, false, false);
            rec.nextTime = Time.time;
            rec.startSimLogged = Time.time;
            rec.lastUpdate = Time.time; // NEW
            recs[incidentId] = rec;
        }

        rec.camera = cam;
        rec.started = true;
        rec.lastUpdate = Time.time; // NEW: keep-alive
        rec.stopAt = -1f;           // keep running until told to stop
    }

    public void ScheduleStop(string incidentId, float postRollSec)
    {
        if (!recs.TryGetValue(incidentId, out var rec)) return;
        float t = Time.time + Mathf.Max(0f, postRollSec);
        rec.stopAt = Mathf.Max(rec.stopAt, t);
    }

    void LateUpdate()
    {
        float now = Time.time;

        foreach (var kv in recs)
        {
            var r = kv.Value;

            // Idle auto-stop (if no more updates and not already scheduled)
            if (enableIdleTimeout && r.started && r.stopAt < 0f && (now - r.lastUpdate) > autoStopIfIdleSec)
            {
                r.stopAt = now + 0.01f; // encode on next tick
            }

            // tick
            if (now < r.nextTime) continue;
            r.nextTime += 1f / Mathf.Max(1, targetFps);

            if (!r.camera) continue;

            // Render
            var prevTarget = r.camera.targetTexture;
            r.camera.targetTexture = r.rt;
            r.camera.Render();
            r.camera.targetTexture = prevTarget;

            // Readback
            var prevActive = RenderTexture.active;
            RenderTexture.active = r.rt;
            if (r.scratch == null) r.scratch = new Texture2D(width, height, TextureFormat.RGB24, false, false);
            r.scratch.ReadPixels(new Rect(0, 0, width, height), 0, 0, false);
            r.scratch.Apply(false);
            RenderTexture.active = prevActive;

            byte[] png = r.scratch.EncodeToPNG();

            // Pre-roll buffer (until first write)
            if (enablePreRoll && !r.preDumped)
            {
                r.preBuf.Enqueue(png);
                int maxFrames = Mathf.Max(1, Mathf.CeilToInt(preRollSec * targetFps));
                while (r.preBuf.Count > maxFrames) r.preBuf.Dequeue();
            }

            if (!r.started) continue;

            // First write: manifest + dump pre-roll
            if (!r.preDumped)
            {
                int preCount = enablePreRoll ? r.preBuf.Count : 0;
                float preDur = preCount / (float)targetFps;
                r.startSimLogged = Time.time - preDur;

                var man = new Manifest { incidentId = r.id, fps = targetFps, startSimSec = r.startSimLogged };
                File.WriteAllText(Path.Combine(r.dir, "manifest.json"), JsonUtility.ToJson(man));

                int idx = 0;
                if (preCount > 0)
                {
                    foreach (var bytes in r.preBuf)
                        File.WriteAllBytes(Path.Combine(r.dir, $"frames_{idx++:D4}.png"), bytes);
                }
                r.frameIdx = idx;
                r.preDumped = true;
                r.preBuf.Clear();
            }

            // Write current frame
            File.WriteAllBytes(Path.Combine(r.dir, $"frames_{r.frameIdx:D4}.png"), png);
            r.frameIdx++;

            // Stop / encode
            if (r.stopAt > 0f && now >= r.stopAt && !r.encodingKicked)
            {
                r.encodingKicked = true;
                if (encodeOnStop)
                {
                    // Pass instance options to encoder
                    var args = new EncodeArgs {
                        ffmpegPath = this.ffmpegPath,
                        dir = r.dir,
                        fps = this.targetFps,
                        outfile = this.outputName,
                        crf = this.crf,
                        preset = this.preset,
                        cleanup = this.deleteFramesAfterEncode
                    };
                    ThreadPool.QueueUserWorkItem(_ => EncodeToMp4(args));
                }
            }
        }
    }

    [System.Serializable]
    class Manifest { public string incidentId; public int fps; public float startSimSec; }

    class EncodeArgs { public string ffmpegPath, dir, outfile, preset; public int fps, crf; public bool cleanup; }

    static void EncodeToMp4(EncodeArgs a)
    {
        string inputPattern = Path.Combine(a.dir, "frames_%04d.png");
        string outputPath   = Path.Combine(a.dir, a.outfile);
        string vf = "scale=ceil(iw/2)*2:ceil(ih/2)*2";

        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = string.IsNullOrEmpty(a.ffmpegPath) ? "ffmpeg" : a.ffmpegPath,
            Arguments = $"-y -framerate {a.fps} -i \"{inputPattern}\" -vf \"{vf}\" -c:v libx264 -pix_fmt yuv420p -preset {a.preset} -crf {a.crf} \"{outputPath}\"",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true
        };

        try
        {
            using (var p = System.Diagnostics.Process.Start(psi))
            {
                string stderr = p.StandardError.ReadToEnd();
                p.WaitForExit();
                UnityEngine.Debug.Log($"[MultiCaptureService] ffmpeg exit {p.ExitCode} → {outputPath}\n{stderr}");

                if (p.ExitCode == 0 && a.cleanup)
                {
                    foreach (var f in Directory.GetFiles(a.dir, "frames_*.png")) File.Delete(f);
                    UnityEngine.Debug.Log("[MultiCaptureService] Deleted PNG frames after encoding.");
                }
            }
        }
        catch (System.Exception ex)
        {
            UnityEngine.Debug.LogError($"[MultiCaptureService] ffmpeg failed: {ex.Message}");
        }
    }
}
