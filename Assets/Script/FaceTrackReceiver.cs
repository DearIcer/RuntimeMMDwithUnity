using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Receives face tracking data from the OpenVtuber Node server over a plain
/// WebSocket (ws://host:6790, JSON payload) and applies it to an MMD-style
/// FBX character: head euler angles, eye gaze bones, mouth/blink blendshapes.
///
/// Payload format:
/// {"euler":[pitch,-yaw,-roll](deg), "eye":[theta,pha](rad),
///  "mouth":0..1, "blink":[left,right](smaller = more closed),
///  "body":[swayX,swayY](normalized head translation),
///  "pose":{"shoulderL":[x,y,score], "shoulderR":..., "elbowL":...,
///          "elbowR":..., "wristL":..., "wristR":..., "hipL":...,
///          "hipR":...} (x/y normalized image coords, y down, COCO
///          left/right from the person's own perspective; omitted when
///          no valid pose is detected)}
/// </summary>
public class FaceTrackReceiver : MonoBehaviour
{
    [Header("Connection")]
    public string serverUrl = "ws://127.0.0.1:6790";
    public float reconnectDelay = 3f;

    [Header("Bones (auto-detected when left empty)")]
    public Transform headBone;
    public Transform leftEyeBone;
    public Transform rightEyeBone;
    public Transform upperBodyBone;

    [Header("Blendshapes")]
    public SkinnedMeshRenderer faceMesh;
    public string mouthShapeName = "あ";
    public string blinkLeftShapeName = "まばたき左";
    public string blinkRightShapeName = "まばたき右";
    public string blinkBothShapeName = "まばたき";

    [Header("Head mapping")]
    public bool invertPitch = false;
    public bool invertYaw = false;
    public bool invertRoll = false;
    public float headScale = 1f;
    [Tooltip("Clamp head rotation per axis (deg); the solvePnP intrinsics " +
             "are approximate and tend to exaggerate large angles")]
    public float maxHeadAngle = 35f;

    [Header("Head calibration")]
    [Tooltip("Average the first frames of tracking as the neutral head pose " +
             "and subtract it from all following frames; press the calibrate " +
             "key to re-capture while holding a neutral pose")]
    public bool calibrateOnStart = true;
    public int calibrationFrames = 30;
    public KeyCode calibrateKey = KeyCode.C;

    [Header("Eye mapping")]
    public float eyeScale = 30f;   // rad -> deg gain
    public bool invertEyeX = false;
    public bool invertEyeY = false;
    public float maxEyeAngle = 25f;

    [Header("Blink mapping")]
    public float blinkClosedThreshold = 0.08f;
    public float blinkOpenThreshold = 0.22f;

    [Header("Body sway (from head translation)")]
    public float bodyRollGain = 60f;   // deg of body roll per unit of normalized sway
    public bool invertBodyRoll = false;
    public float bodyPitchGain = 0f;   // forward/back lean, off by default
    public bool invertBodyPitch = false;

    [Header("Arm bones (auto-detected when left empty)")]
    public Transform upperArmL;
    public Transform upperArmR;
    public Transform elbowL;
    public Transform elbowR;

    [Header("Arm mapping (from pose keypoints)")]
    [Tooltip("Image-space angle (deg) of an arm hanging straight down; the " +
             "arm bones rotate by the limb's offset from this reference")]
    public float neutralArmDownAngle = 90f;
    public float armGain = 1f;
    public bool invertArmL = false;
    public bool invertArmR = false;
    [Tooltip("Keypoints below this confidence are ignored (bone holds pose)")]
    public float poseScoreThreshold = 0.3f;

    [Header("Smoothing")]
    [Tooltip("Higher = snappier, lower = smoother")]
    public float rotationSmoothing = 15f;
    public float blendshapeSmoothing = 20f;

    // ---- runtime state ----
    private readonly object _lock = new object();
    private string _latestJson;
    private string _status = "not started";

    private FaceData _data;
    private bool _hasData;

    private Quaternion _headBase, _leftEyeBase, _rightEyeBase, _bodyBase;
    private Quaternion _armLBase, _armRBase, _elbowLBase, _elbowRBase;
    private float _mouthWeight, _blinkLWeight, _blinkRWeight;

    private Vector3 _headNeutral, _headCalibSum;
    private int _headCalibCount;
    private bool _headCalibrated;

    private int _mouthIndex = -1, _blinkLIndex = -1, _blinkRIndex = -1;

    private CancellationTokenSource _cts;

    [Serializable]
    private class FaceData
    {
        public float[] euler;
        public float[] eye;
        public float mouth;
        public float[] blink;
        public float[] body;
        public PoseData pose;
    }

    // half-body keypoints; each is [x, y, score], x/y normalized to the
    // camera frame (y down). Left/right is the person's own perspective:
    // when facing the camera, shoulderL appears on the image's right side,
    // same screen side as the character's 左腕, giving a mirror mapping.
    [Serializable]
    private class PoseData
    {
        public float[] shoulderL;
        public float[] shoulderR;
        public float[] elbowL;
        public float[] elbowR;
        public float[] wristL;
        public float[] wristR;
        public float[] hipL;
        public float[] hipR;
    }

    private static readonly string[] HeadCandidates = { "頭", "Head", "head", "HEAD" };
    private static readonly string[] LeftEyeCandidates = { "目.L", "Ŀ.L", "左目", "Eye_L", "Eye.L", "LeftEye" };
    private static readonly string[] RightEyeCandidates = { "目.R", "Ŀ.R", "右目", "Eye_R", "Eye.R", "RightEye" };
    private static readonly string[] UpperBodyCandidates = { "上半身", "UpperChest", "UpperBody", "Spine2", "Spine1", "Spine" };
    private static readonly string[] UpperArmLCandidates = { "左腕", "UpperArm_L", "UpperArm.L", "LeftUpperArm", "LeftArm", "Shoulder_L" };
    private static readonly string[] UpperArmRCandidates = { "右腕", "UpperArm_R", "UpperArm.R", "RightUpperArm", "RightArm", "Shoulder_R" };
    private static readonly string[] ElbowLCandidates = { "左ひじ", "LowerArm_L", "LowerArm.L", "LeftLowerArm", "LeftForeArm", "Elbow_L" };
    private static readonly string[] ElbowRCandidates = { "右ひじ", "LowerArm_R", "LowerArm.R", "RightLowerArm", "RightForeArm", "Elbow_R" };

    private void Awake()
    {
        AutoBind();
    }

    private void Start()
    {
        _cts = new CancellationTokenSource();
        _ = ReceiveLoop(_cts.Token);
    }

    private void OnDestroy()
    {
        _cts?.Cancel();
    }

    // ------------------------------------------------------------------
    // Binding
    // ------------------------------------------------------------------

    private void AutoBind()
    {
        if (headBone == null) headBone = FindChildByNames(HeadCandidates);
        if (leftEyeBone == null) leftEyeBone = FindChildByNames(LeftEyeCandidates);
        if (rightEyeBone == null) rightEyeBone = FindChildByNames(RightEyeCandidates);
        if (upperBodyBone == null) upperBodyBone = FindChildByNames(UpperBodyCandidates);
        if (upperArmL == null) upperArmL = FindChildByNames(UpperArmLCandidates);
        if (upperArmR == null) upperArmR = FindChildByNames(UpperArmRCandidates);
        if (elbowL == null) elbowL = FindChildByNames(ElbowLCandidates);
        if (elbowR == null) elbowR = FindChildByNames(ElbowRCandidates);

        if (faceMesh == null)
        {
            // pick the skinned mesh with the most blendshapes (the face)
            SkinnedMeshRenderer best = null;
            foreach (var smr in GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (smr.sharedMesh == null) continue;
                if (best == null || smr.sharedMesh.blendShapeCount > best.sharedMesh.blendShapeCount)
                    best = smr;
            }
            faceMesh = best;
        }

        if (headBone != null) _headBase = headBone.localRotation;
        if (leftEyeBone != null) _leftEyeBase = leftEyeBone.localRotation;
        if (rightEyeBone != null) _rightEyeBase = rightEyeBone.localRotation;
        if (upperBodyBone != null) _bodyBase = upperBodyBone.localRotation;
        if (upperArmL != null) _armLBase = upperArmL.localRotation;
        if (upperArmR != null) _armRBase = upperArmR.localRotation;
        if (elbowL != null) _elbowLBase = elbowL.localRotation;
        if (elbowR != null) _elbowRBase = elbowR.localRotation;

        if (faceMesh != null)
        {
            _mouthIndex = FindBlendShape(mouthShapeName);
            _blinkLIndex = FindBlendShape(blinkLeftShapeName);
            _blinkRIndex = FindBlendShape(blinkRightShapeName);
            if (_blinkLIndex < 0 && _blinkRIndex < 0)
            {
                int both = FindBlendShape(blinkBothShapeName);
                _blinkLIndex = both;
                _blinkRIndex = both;
            }
        }

        Debug.Log($"[FaceTrack] head={Name(headBone)} eyeL={Name(leftEyeBone)} eyeR={Name(rightEyeBone)} " +
                  $"body={Name(upperBodyBone)} mesh={Name(faceMesh)} mouthIdx={_mouthIndex} blinkL={_blinkLIndex} blinkR={_blinkRIndex}\n" +
                  $"[FaceTrack] armL={Name(upperArmL)} armR={Name(upperArmR)} elbowL={Name(elbowL)} elbowR={Name(elbowR)}");
    }

    private static string Name(Component c) => c == null ? "<missing>" : c.name;

    private Transform FindChildByNames(IEnumerable<string> candidates)
    {
        var all = GetComponentsInChildren<Transform>(true);
        foreach (var name in candidates)
        {
            foreach (var t in all)
            {
                if (t.name == name) return t;
            }
        }
        return null;
    }

    private int FindBlendShape(string shapeName)
    {
        if (faceMesh == null || string.IsNullOrEmpty(shapeName)) return -1;
        var mesh = faceMesh.sharedMesh;
        for (int i = 0; i < mesh.blendShapeCount; i++)
        {
            if (mesh.GetBlendShapeName(i) == shapeName) return i;
        }
        return -1;
    }

    // ------------------------------------------------------------------
    // Network (background thread)
    // ------------------------------------------------------------------

    private async Task ReceiveLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                SetStatus("connecting...");
                using (var ws = new ClientWebSocket())
                {
                    await ws.ConnectAsync(new Uri(serverUrl), ct);
                    SetStatus("connected");
                    var buffer = new byte[8192];
                    while (ws.State == WebSocketState.Open && !ct.IsCancellationRequested)
                    {
                        var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
                        if (result.MessageType == WebSocketMessageType.Close) break;
                        if (result.MessageType != WebSocketMessageType.Text) continue;
                        string json = Encoding.UTF8.GetString(buffer, 0, result.Count);
                        lock (_lock) { _latestJson = json; }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception e)
            {
                SetStatus("error: " + e.Message);
            }

            SetStatus("retrying in " + reconnectDelay + "s");
            try { await Task.Delay(TimeSpan.FromSeconds(reconnectDelay), ct); }
            catch (OperationCanceledException) { break; }
        }
        SetStatus("stopped");
    }

    private void SetStatus(string s)
    {
        lock (_lock) { _status = s; }
    }

    // ------------------------------------------------------------------
    // Apply (main thread)
    // ------------------------------------------------------------------

    private void LateUpdate()
    {
        string json;
        lock (_lock)
        {
            json = _latestJson;
            _latestJson = null;
        }

        if (json != null)
        {
            try
            {
                _data = JsonUtility.FromJson<FaceData>(json);
                _hasData = _data != null;
            }
            catch (Exception)
            {
                _hasData = false;
            }
        }

        if (!_hasData) return;

        float t = 1f - Mathf.Exp(-rotationSmoothing * Time.deltaTime);
        float tb = 1f - Mathf.Exp(-blendshapeSmoothing * Time.deltaTime);

        // evaluate the body sway first so the head rotation below can
        // subtract it and avoid double-counting the same lean
        float bodyRollApplied = 0f, bodyLeanApplied = 0f;
        if (upperBodyBone != null && _data.body != null && _data.body.Length >= 2)
        {
            bodyRollApplied = Mathf.Clamp(_data.body[0] * bodyRollGain, -45f, 45f) * (invertBodyRoll ? -1f : 1f);
            bodyLeanApplied = Mathf.Clamp(_data.body[1] * bodyPitchGain, -30f, 30f) * (invertBodyPitch ? -1f : 1f);
        }

        if (headBone != null && _data.euler != null && _data.euler.Length >= 3)
        {
            if (Input.GetKeyDown(calibrateKey))
            {
                _headCalibrated = false;
                _headCalibCount = 0;
                _headCalibSum = Vector3.zero;
            }

            Vector3 e = new Vector3(_data.euler[0], _data.euler[1], _data.euler[2]);
            if (!_headCalibrated)
            {
                if (calibrateOnStart)
                {
                    _headCalibSum += e;
                    if (++_headCalibCount >= calibrationFrames)
                    {
                        _headNeutral = _headCalibSum / _headCalibCount;
                        _headCalibrated = true;
                        Debug.Log($"[FaceTrack] head neutral calibrated to {_headNeutral}");
                    }
                }
                else
                {
                    _headNeutral = Vector3.zero;
                    _headCalibrated = true;
                }
            }

            float p = Mathf.Clamp((e.x - _headNeutral.x) * headScale * (invertPitch ? -1f : 1f) - bodyLeanApplied, -maxHeadAngle, maxHeadAngle);
            float y = Mathf.Clamp((e.y - _headNeutral.y) * headScale * (invertYaw ? -1f : 1f), -maxHeadAngle, maxHeadAngle);
            float r = Mathf.Clamp((e.z - _headNeutral.z) * headScale * (invertRoll ? -1f : 1f) - bodyRollApplied, -maxHeadAngle, maxHeadAngle);
            Quaternion target = _headBase * Quaternion.Euler(p, y, r);
            headBone.localRotation = Quaternion.Slerp(headBone.localRotation, target, t);
        }

        if (upperBodyBone != null && _data.body != null && _data.body.Length >= 2)
        {
            Quaternion target = _bodyBase * Quaternion.Euler(bodyLeanApplied, 0f, bodyRollApplied);
            upperBodyBone.localRotation = Quaternion.Slerp(upperBodyBone.localRotation, target, t);
        }

        // half-body pose keypoints (independent from the head sway above)
        ApplyPose(t);

        if (_data.eye != null && _data.eye.Length >= 2)
        {
            float ex = Mathf.Clamp(_data.eye[1] * eyeScale * (invertEyeX ? -1f : 1f), -maxEyeAngle, maxEyeAngle);
            float ey = Mathf.Clamp(_data.eye[0] * eyeScale * (invertEyeY ? -1f : 1f), -maxEyeAngle, maxEyeAngle);
            if (leftEyeBone != null)
                leftEyeBone.localRotation = Quaternion.Slerp(leftEyeBone.localRotation, _leftEyeBase * Quaternion.Euler(ex, ey, 0f), t);
            if (rightEyeBone != null)
                rightEyeBone.localRotation = Quaternion.Slerp(rightEyeBone.localRotation, _rightEyeBase * Quaternion.Euler(ex, ey, 0f), t);
        }

        if (faceMesh != null)
        {
            float mouthTarget = Mathf.Clamp01(_data.mouth) * 100f;
            _mouthWeight = Mathf.Lerp(_mouthWeight, mouthTarget, tb);
            if (_mouthIndex >= 0) faceMesh.SetBlendShapeWeight(_mouthIndex, _mouthWeight);

            if (_data.blink != null && _data.blink.Length >= 2)
            {
                float bl = BlinkWeight(_data.blink[0]);
                float br = BlinkWeight(_data.blink[1]);
                _blinkLWeight = Mathf.Lerp(_blinkLWeight, bl, tb);
                _blinkRWeight = Mathf.Lerp(_blinkRWeight, br, tb);
                if (_blinkLIndex >= 0) faceMesh.SetBlendShapeWeight(_blinkLIndex, _blinkLWeight);
                if (_blinkRIndex >= 0) faceMesh.SetBlendShapeWeight(_blinkRIndex, _blinkRWeight);
            }
        }
    }

    private float BlinkWeight(float openness)
    {
        return 100f * (1f - Mathf.InverseLerp(blinkClosedThreshold, blinkOpenThreshold, openness));
    }

    // ------------------------------------------------------------------
    // Half-body pose -> arm bones
    // ------------------------------------------------------------------

    private void ApplyPose(float t)
    {
        var pose = _data.pose;
        if (pose == null) return;

        float gainL = armGain * (invertArmL ? -1f : 1f);
        float gainR = armGain * (invertArmR ? -1f : 1f);

        // upper arm: image-space direction shoulder->elbow, applied as an
        // offset from the "arm hanging down" reference angle
        ApplyLimbAngle(upperArmL, _armLBase, pose.shoulderL, pose.elbowL, neutralArmDownAngle, gainL, t);
        ApplyLimbAngle(upperArmR, _armRBase, pose.shoulderR, pose.elbowR, neutralArmDownAngle, gainR, t);

        // elbow: relative bend between upper arm and forearm directions
        if (Valid(pose.shoulderL) && Valid(pose.elbowL))
            ApplyLimbAngle(elbowL, _elbowLBase, pose.elbowL, pose.wristL,
                           SegmentAngleDeg(pose.shoulderL, pose.elbowL), gainL, t);
        if (Valid(pose.shoulderR) && Valid(pose.elbowR))
            ApplyLimbAngle(elbowR, _elbowRBase, pose.elbowR, pose.wristR,
                           SegmentAngleDeg(pose.shoulderR, pose.elbowR), gainR, t);
    }

    private bool Valid(float[] p)
    {
        return p != null && p.Length >= 3 && p[2] >= poseScoreThreshold;
    }

    private static float SegmentAngleDeg(float[] a, float[] b)
    {
        // image coords: x right, y down; straight down = +90 deg
        return Mathf.Atan2(b[1] - a[1], b[0] - a[0]) * Mathf.Rad2Deg;
    }

    private void ApplyLimbAngle(Transform bone, Quaternion baseRot,
                                float[] from, float[] to,
                                float neutralDeg, float gain, float t)
    {
        if (bone == null || !Valid(from) || !Valid(to)) return;
        float offset = Mathf.DeltaAngle(neutralDeg, SegmentAngleDeg(from, to));
        Quaternion target = baseRot * Quaternion.Euler(0f, 0f, offset * gain);
        bone.localRotation = Quaternion.Slerp(bone.localRotation, target, t);
    }

    // ------------------------------------------------------------------
    // Debug overlay
    // ------------------------------------------------------------------

    private void OnGUI()
    {
        string status;
        lock (_lock) { status = _status; }

        string text = "[FaceTrack] " + status;
        if (_hasData && _data != null)
        {
            string euler = _data.euler != null && _data.euler.Length >= 3
                ? string.Format("({0:F1}, {1:F1}, {2:F1})", _data.euler[0], _data.euler[1], _data.euler[2])
                : "-";
            string blink = _data.blink != null && _data.blink.Length >= 2
                ? string.Format("({0:F2}, {1:F2})", _data.blink[0], _data.blink[1])
                : "-";
            string body = _data.body != null && _data.body.Length >= 2
                ? string.Format("({0:F3}, {1:F3})", _data.body[0], _data.body[1])
                : "-";
            string pose = _data.pose != null && _data.pose.wristL != null && _data.pose.wristR != null
                ? string.Format("wristL({0:F2},{1:F2}) wristR({2:F2},{3:F2})",
                                _data.pose.wristL[0], _data.pose.wristL[1],
                                _data.pose.wristR[0], _data.pose.wristR[1])
                : "no pose";
            text += string.Format("\neuler {0}\nmouth {1:F2}\nblink {2}\nbody {3}\npose {4}", euler, _data.mouth, blink, body, pose);
        }

        GUI.Box(new Rect(10, 10, 260, 126), "");
        GUI.Label(new Rect(18, 14, 250, 120), text);
    }
}
