# AGENTS.md — OpenVtuber

Guidance for AI coding agents working in this repository.

## Project overview

OpenVtuber (虚拟アイドル共享计划) is a real-time face tracking and animation
system. A Python client captures video from a camera or file, runs a
lightweight deep-learning pipeline (face detection → face alignment → head
pose estimation → iris/gaze localization) entirely on TFLite models, plus
a parallel MoveNet pose-estimation channel for half-body motion capture,
and streams facial parameters (head euler angles, eye gaze, mouth openness,
blink status, body sway, upper-body keypoints) as JSON over a WebSocket server (`FaceDataServer`,
`ws://127.0.0.1:6790`) embedded in the same process. The primary consumer
is a Unity project (`FaceTrackReceiver.cs`).

The whole project is a single-process-per-role setup: there is no build
step, packaging, or deployment pipeline.

## Repository layout

- `vtuber_link_start.py` — main Python client entry point. Runs a
  multithreaded pipeline (face detection → alignment → iris/gaze →
  WebSocket broadcast → OpenCV preview) with `queue.Queue` (buffer size 18)
  between stages. Hosts the embedded `FaceDataServer` WebSocket server on
  port 6790 (asyncio loop on a daemon thread; `server.broadcast()` is
  thread-safe). File input is paced to the video FPS and loops at EOF;
  integer arguments (e.g. `0`) are treated as cameras (no pacing/looping).
- `face_status_preview.py` — standalone debug script that runs the
  detection/alignment/head-pose pipeline and draws mouth/eye/head-pose
  status text, without any networking.
- `service/` — the core Python package (re-exported via `service/__init__.py`):
  - `TFLiteFaceDetector.py` — `UltraLightFaceDetecion` (note the typo in the
    class name; keep it, it is part of the public API). Faster-RetinaFace
    style RFB-320 detector: anchor generation, box decoding,
    `tf.image.non_max_suppression`.
  - `TFLiteFaceAlignment.py` — `CoordinateAlignmentModel`, 106-point 2D
    facial landmark regression. Exposes `eye_bound` landmark index lists.
    `get_landmarks()` is a **generator** that yields one landmark array
    (shape `(106, 2)`) per detected face.
  - `SolvePnPHeadPoseEstimation.py` — `HeadPoseEstimator`, wraps
    `cv2.solvePnP` with pre-defined 3D object points
    (`weights/head_pose_object_points.npy`) and converts the result to
    euler angles via `cv2.decomposeProjectionMatrix`.
  - `TFLiteIrisLocalization.py` — `IrisLocalizationModel`, TFLite iris mesh
    model (64×64 input) plus `calculate_3d_gaze` geometry for gaze angles.
  - `TFLitePoseEstimation.py` — `MoveNetPoseModel`, MoveNet SinglePose
    Lightning (192×192 uint8 input, 17 COCO keypoints) for half-body
    motion capture. `get_keypoints()` returns `(17, 3)` rows of
    `(x, y, score)` with x/y normalized to [0, 1]; `UPPER_BODY` lists the
    shoulder/elbow/wrist/hip indices, `SKELETON` the bone pairs for
    drawing. Keypoint left/right follows the person's own perspective.
- `weights/` — TFLite models and data files loaded at runtime:
  `RFB-320.tflite` (detector), `coor_2d106.tflite` (landmarks),
  `iris_localization.tflite` (iris),
  `movenet_singlepose_lightning.tflite` (pose),
  `head_pose_object_points.npy`.
  Paths are hardcoded relative to the project root, so **run Python
  entry points from the project root**.
- `qq_input.mp4` — sample input video for testing the pipeline.

## Technology stack

- Python 3.8+ (pyproject) / README says 3.6+, but 3.8+ is the realistic floor.
- TensorFlow ≥ 2.3 (only `tf.lite.Interpreter` and
  `tf.image.non_max_suppression` are used), OpenCV (`opencv-python`),
  NumPy, `websockets` (embedded broadcast server in `vtuber_link_start.py`).

## Setup and run commands

Python (two dependency managers are present — prefer one consistently):

```bash
pip3 install -r requirements.txt        # plain pip
# or
poetry install                          # poetry
```

Run the face-tracking client (from the **project root** so `weights/`
paths resolve) — this also serves tracking data on `ws://127.0.0.1:6790`
for Unity or other consumers:

```bash
python3 vtuber_link_start.py <your-video-path>    # e.g. qq_input.mp4 or 0 for a camera
python3 face_status_preview.py <your-video-path>  # offline preview, no networking
```

Individual pipeline stages can also be demoed standalone (each has a
`__main__` block): `python3 service/TFLiteFaceAlignment.py <video>`,
`python3 service/SolvePnPHeadPoseEstimation.py <video>`, etc. Note these
demos still use `weights/...` paths relative to the project root even
though the README mentions a `PythonClient` subdirectory — that directory
no longer exists in this layout.

## Build and test commands

There is no compilation/build step, and currently no test suite.

## Code style and conventions

- **Language**: README and code comments are in English (the project title
  has Japanese). Write comments and docstrings in English.
- Python style follows PEP 8 loosely; the codebase predates heavy linting.
  `face_status_preview.py` shows the newer house style: type-friendly,
  %-format strings, small pure functions, a `main()` guarded by
  `if __name__ == "__main__":`.
- Model wrapper classes follow one pattern: load the TFLite interpreter in
  `__init__`, store `functools.partial` handles to `set_tensor`/
  `get_tensor` for hot-path inference, and separate
  `_preprocessing` / `_inference` / `_postprocessing` methods. Follow this
  pattern when adding new models.
- Images are **BGR** everywhere (OpenCV); models expect RGB, so the
  preprocessing does `img[..., ::-1]` and normalizes to [-1, 1] with
  `cv2.normalize(..., alpha=-1, beta=1, norm_type=cv2.NORM_MINMAX)`.
- The 106-landmark index conventions are load-bearing and duplicated
  between `vtuber_link_start.py` and `face_status_preview.py` (eye corners
  `[35, 89]`/`[39, 93]`, centers `[34, 88]`, mouth `[53, 60, 62, 71]`,
  blink `[33, 40]`/`[87, 94]`). If you change the landmark protocol,
  update **all** consumers and keep them consistent with
  `CoordinateAlignmentModel.eye_bound` and the `.npy` object points.
- Naming quirk to preserve: `UltraLightFaceDetecion` (missing 't') is the
  public class name exported from `service/__init__.py`; do not "fix" the
  typo without a coordinated rename.

## Architecture notes

- Data flow: `cv2.VideoCapture` → detector → alignment → (head pose +
  iris) → EMA smoothing (`SMOOTH_ALPHA` in `iris_localization`) → dict
  `{'euler', 'eye', 'mouth', 'blink', 'body', 'pose'?}` →
  `FaceDataServer.broadcast()` → JSON over WebSocket (port 6790) → Unity
  `FaceTrackReceiver`.
- Half-body mocap runs on its own thread (`pose_estimation`): frames are
  duplicated into `pose_queue`, MoveNet keypoints are EMA smoothed and
  published through the lock-protected `pose_state` dict; the broadcast
  in `iris_localization` merges them as a flat `'pose'` object
  (`shoulderL/R`, `elbowL/R`, `wristL/R`, `hipL/R`, each `[x, y, score]`)
  which is omitted when no keypoint clears the 0.3 score threshold.
  Field names must stay in sync with the `PoseData` class in Unity's
  `FaceTrackReceiver.cs` (JsonUtility needs matching field names; it does
  not support jagged arrays or dicts, hence the flat layout).
- The head-translation sway (`'body'` field, `BASE_ALPHA` baseline in
  `iris_localization`) is a separate, older feature — keep it independent
  from the pose channel; do not merge or derive one from the other.
- Only the first detected face is streamed (`break` after the first
  iteration in `iris_localization`).
- Iris inference is skipped when head yaw exceeds ±45° (`YAW_THD`); pupils
  fall back to eye centers.
- The Python client uses threads + bounded queues, not asyncio for the
  pipeline; only the embedded WebSocket server runs an asyncio loop, on its
  own daemon thread.

## Security considerations

- The Python `FaceDataServer` binds port 6790 on localhost only. There is
  no authentication on the channel — the setup assumes a trusted local
  machine.
- Model files in `weights/` and the `.npy` object points are loaded with
  `np.load(..., allow_pickle=True)` — only load trusted files.
