# coding: utf-8

import numpy as np
import service
import cv2
import sys
import time
import json
import asyncio
import websockets

from threading import Thread, Lock
from queue import Queue


# ======================================================
# Embedded WebSocket server: broadcasts face tracking results directly
# to clients (e.g. Unity). Replaces the old Node.js Socket.IO relay.

class FaceDataServer:
    def __init__(self, host="127.0.0.1", port=6790):
        self.host = host
        self.port = port
        self.clients = set()
        self.loop = None

    def start(self):
        Thread(target=self._run, daemon=True).start()

    def _run(self):
        self.loop = asyncio.new_event_loop()
        asyncio.set_event_loop(self.loop)
        self.loop.run_until_complete(self._serve())
        self.loop.run_forever()

    async def _serve(self):
        async with websockets.serve(self._handler, self.host, self.port):
            print("face data server listening on ws://%s:%d" % (self.host, self.port))
            await asyncio.Future()  # serve forever

    async def _handler(self, ws):
        self.clients.add(ws)
        print("a ws client connected")
        try:
            await ws.wait_closed()
        finally:
            self.clients.discard(ws)
            print("a ws client disconnected")

    def broadcast(self, payload):
        if not self.clients or self.loop is None:
            return
        msg = json.dumps(payload)
        asyncio.run_coroutine_threadsafe(self._send_all(msg), self.loop)

    async def _send_all(self, msg):
        for ws in list(self.clients):
            try:
                await ws.send(msg)
            except Exception:
                self.clients.discard(ws)


server = FaceDataServer()
server.start()

# ======================================================


cap = cv2.VideoCapture(sys.argv[1])

fd = service.UltraLightFaceDetecion("weights/RFB-320.tflite",
                                    conf_threshold=0.98)
fa = service.CoordinateAlignmentModel("weights/coor_2d106.tflite")
hp = service.HeadPoseEstimator("weights/head_pose_object_points.npy",
                               cap.get(3), cap.get(4))
gs = service.IrisLocalizationModel("weights/iris_localization.tflite")
ps = service.MoveNetPoseModel("weights/movenet_singlepose_lightning.tflite")

QUEUE_BUFFER_SIZE = 18

box_queue = Queue(maxsize=QUEUE_BUFFER_SIZE)
landmark_queue = Queue(maxsize=QUEUE_BUFFER_SIZE)
iris_queue = Queue(maxsize=QUEUE_BUFFER_SIZE)
upstream_queue = Queue(maxsize=QUEUE_BUFFER_SIZE)
pose_queue = Queue(maxsize=QUEUE_BUFFER_SIZE)

# latest half-body pose, shared between the pose thread (writer) and the
# iris (broadcast) / draw (preview) threads (readers)
pose_lock = Lock()
pose_state = {'kps': None}  # smoothed (17, 3) keypoints, or None

# ======================================================

def face_detection():
    is_camera = sys.argv[1].isdigit()
    fps = cap.get(cv2.CAP_PROP_FPS)
    frame_interval = 1.0 / fps if fps and fps > 0 else 1.0 / 30.0

    while True:
        start = time.time()
        ret, frame = cap.read()

        if not ret:
            if is_camera:
                break
            # loop the video file
            cap.set(cv2.CAP_PROP_POS_FRAMES, 0)
            continue

        face_boxes, _ = fd.inference(frame)
        box_queue.put((frame, face_boxes))
        pose_queue.put(frame)

        if not is_camera:
            # pace file playback to the video's native frame rate
            elapsed = time.time() - start
            if elapsed < frame_interval:
                time.sleep(frame_interval - elapsed)


def face_alignment():
    while True:
        frame, boxes = box_queue.get()
        landmarks = fa.get_landmarks(frame, boxes)
        landmark_queue.put((frame, landmarks))


def pose_estimation(SMOOTH_ALPHA=0.35):
    """Half-body mocap: run MoveNet on every frame and publish the EMA
    smoothed 17-point skeleton through pose_state. Independent from the
    face pipeline; the existing head-translation sway stays untouched."""
    smoothed = None

    while True:
        frame = pose_queue.get()
        kps = ps.get_keypoints(frame)

        # exponential moving average to suppress per-frame keypoint noise
        if smoothed is None:
            smoothed = kps
        else:
            smoothed = SMOOTH_ALPHA * kps + (1 - SMOOTH_ALPHA) * smoothed

        with pose_lock:
            pose_state['kps'] = smoothed


def iris_localization(YAW_THD=45, SMOOTH_ALPHA=0.35, BASE_ALPHA=0.01):
    smoothed = None  # EMA state over (euler, eye, mouth, blink, sway)
    base_pos = None  # slow EMA of the head position = neutral stance

    while True:
        frame, preds = landmark_queue.get()

        for landmarks in preds:
            # calculate head pose
            euler_angle, head_pos = hp.get_head_pose_and_translation(landmarks)
            euler_angle = euler_angle.flatten()
            pitch, yaw, roll = euler_angle

            # neutral stance baseline drifts slowly; sway = current - baseline,
            # normalized by head depth so it is roughly scale-invariant
            if base_pos is None:
                base_pos = head_pos.copy()
            else:
                base_pos = (1 - BASE_ALPHA) * base_pos + BASE_ALPHA * head_pos
            depth = abs(base_pos[2]) if abs(base_pos[2]) > 1e-6 else 1.0
            sway = (head_pos[:2] - base_pos[:2]) / depth

            eye_starts = landmarks[[35, 89]]
            eye_ends = landmarks[[39, 93]]
            eye_centers = landmarks[[34, 88]]
            eye_lengths = (eye_ends - eye_starts)[:, 0]

            pupils = eye_centers.copy()

            if yaw > -YAW_THD:
                iris_left = gs.get_mesh(frame, eye_lengths[0], eye_centers[0])
                pupils[0] = iris_left[0]

            if yaw < YAW_THD:
                iris_right = gs.get_mesh(frame, eye_lengths[1], eye_centers[1])
                pupils[1] = iris_right[0]

            poi = eye_starts, eye_ends, pupils, eye_centers

            theta, pha, _ = gs.calculate_3d_gaze(poi)
            mouth_open_percent = (
                landmarks[60, 1] - landmarks[62, 1]) / (landmarks[53, 1] - landmarks[71, 1])
            left_eye_status = (
                landmarks[33, 1] - landmarks[40, 1]) / eye_lengths[0]
            right_eye_status = (
                landmarks[87, 1] - landmarks[94, 1]) / eye_lengths[1]

            current = np.array([
                pitch, -yaw, -roll,
                theta.mean(), pha.mean(),
                mouth_open_percent,
                left_eye_status, right_eye_status,
                sway[0], sway[1],
            ], dtype=np.float64)

            # exponential moving average to suppress per-frame landmark noise
            if smoothed is None:
                smoothed = current
            else:
                smoothed = SMOOTH_ALPHA * current + (1 - SMOOTH_ALPHA) * smoothed

            result_string = {'euler': [float(x) for x in smoothed[0:3]],
                             'eye': [float(x) for x in smoothed[3:5]],
                             'mouth': float(smoothed[5]),
                             'blink': [float(x) for x in smoothed[6:8]],
                             'body': [float(x) for x in smoothed[8:10]]}

            # half-body pose keypoints from the pose thread, kept separate
            # from the head-translation sway in 'body' above
            with pose_lock:
                pose_kps = pose_state['kps']
            if pose_kps is not None:
                ub = pose_kps[list(ps.UPPER_BODY)]
                if ub[:, 2].max() >= 0.3:
                    names = ('shoulderL', 'shoulderR', 'elbowL', 'elbowR',
                             'wristL', 'wristR', 'hipL', 'hipR')
                    result_string['pose'] = {
                        name: [float(v) for v in ub[i]]
                        for i, name in enumerate(names)}

            server.broadcast(result_string)
            upstream_queue.put((frame, landmarks, euler_angle))
            break


def draw(color=(125, 255, 0), thickness=2):
    while True:
        frame, landmarks, euler_angle = upstream_queue.get()

        for p in np.round(landmarks).astype(np.int32):
            cv2.circle(frame, tuple(p), 1, color, thickness, cv2.LINE_AA)

        face_center = np.mean(landmarks, axis=0)
        hp.draw_axis(frame, euler_angle, face_center)

        # overlay the half-body skeleton from the pose thread
        with pose_lock:
            pose_kps = pose_state['kps']
        if pose_kps is not None:
            ps.draw_skeleton(pose_kps, frame)

        frame = cv2.resize(frame, (960, 720))

        cv2.imshow('result', frame)
        cv2.waitKey(1)


draw_thread = Thread(target=draw)
draw_thread.start()

pose_thread = Thread(target=pose_estimation)
pose_thread.start()

iris_thread = Thread(target=iris_localization)
iris_thread.start()

alignment_thread = Thread(target=face_alignment)
alignment_thread.start()

face_detection()
cap.release()
cv2.destroyAllWindows()
