# coding: utf-8
"""Preview face tracking status without connecting to the MMD server."""

import sys
from pathlib import Path

import cv2
import numpy as np


ROOT = Path(__file__).resolve().parent
sys.path.insert(0, str(ROOT))

from service import (  # noqa: E402
    CoordinateAlignmentModel,
    HeadPoseEstimator,
    UltraLightFaceDetecion,
)


def get_status(landmarks):
    eye_starts = landmarks[[35, 89]]
    eye_ends = landmarks[[39, 93]]
    eye_lengths = (eye_ends - eye_starts)[:, 0]

    left_eye = 0.0
    right_eye = 0.0
    if eye_lengths[0] > 1e-6:
        left_eye = (landmarks[33, 1] - landmarks[40, 1]) / eye_lengths[0]
    if eye_lengths[1] > 1e-6:
        right_eye = (landmarks[87, 1] - landmarks[94, 1]) / eye_lengths[1]

    mouth_height = landmarks[53, 1] - landmarks[71, 1]
    mouth = 0.0
    if abs(mouth_height) > 1e-6:
        mouth = (landmarks[60, 1] - landmarks[62, 1]) / mouth_height

    return mouth, left_eye, right_eye


def draw_status_panel(frame, face_index, mouth, left_eye, right_eye, euler):
    mouth_state = "OPEN" if mouth > 0.45 else ("HALF" if mouth > 0.2 else "CLOSED")
    left_state = "OPEN" if left_eye > 0.1 else "CLOSED"
    right_state = "OPEN" if right_eye > 0.1 else "CLOSED"

    lines = [
        "FACE %d" % (face_index + 1),
        "MOUTH %.2f %s" % (mouth, mouth_state),
        "L_EYE %.2f %s" % (left_eye, left_state),
        "R_EYE %.2f %s" % (right_eye, right_state),
        "PITCH %.1f YAW %.1f ROLL %.1f" % (euler[0], euler[1], euler[2]),
    ]

    origin_x = 10 + face_index * 330
    origin_y = 30
    for line in lines:
        cv2.putText(
            frame,
            line,
            (origin_x, origin_y),
            cv2.FONT_HERSHEY_SIMPLEX,
            0.55,
            (0, 255, 255),
            1,
            cv2.LINE_AA,
        )
        origin_y += 24


def main(video_path):
    cap = cv2.VideoCapture(video_path)
    if not cap.isOpened():
        raise RuntimeError("Cannot open video: %s" % video_path)

    fd = UltraLightFaceDetecion("weights/RFB-320.tflite", conf_threshold=0.88)
    fa = CoordinateAlignmentModel("weights/coor_2d106.tflite")
    hp = HeadPoseEstimator(
        "weights/head_pose_object_points.npy",
        cap.get(cv2.CAP_PROP_FRAME_WIDTH),
        cap.get(cv2.CAP_PROP_FRAME_HEIGHT),
    )

    print("Press q to quit, space to pause.")
    paused = False

    while True:
        if not paused:
            ret, frame = cap.read()
            if not ret:
                break

            boxes, _ = fd.inference(frame)
            landmarks_list = list(fa.get_landmarks(frame, boxes))

            for det in boxes.astype(np.int32):
                cv2.rectangle(
                    frame,
                    (det[0], det[1]),
                    (det[2], det[3]),
                    (0, 255, 0),
                    2,
                )

            for face_index, landmarks in enumerate(landmarks_list):
                for point in np.round(landmarks).astype(np.int32):
                    cv2.circle(frame, tuple(point), 1, (0, 255, 255), 1, cv2.LINE_AA)

                for eye_indices in fa.eye_bound:
                    eye_points = landmarks[eye_indices].astype(np.int32)
                    cv2.polylines(frame, [eye_points], True, (255, 0, 255), 1)

                mouth, left_eye, right_eye = get_status(landmarks)
                euler = hp.get_head_pose(landmarks).flatten()
                face_center = np.mean(landmarks, axis=0)
                hp.draw_axis(frame, euler, face_center, size=60, thickness=2)
                draw_status_panel(
                    frame,
                    face_index,
                    mouth,
                    left_eye,
                    right_eye,
                    euler,
                )

        cv2.imshow("face status preview", frame)
        key = cv2.waitKey(30) & 0xFF
        if key == ord("q"):
            break
        if key == ord(" "):
            paused = not paused

    cap.release()
    cv2.destroyAllWindows()


if __name__ == "__main__":
    if len(sys.argv) != 2:
        print("Usage: python face_status_preview.py <video-path>")
        sys.exit(1)
    main(sys.argv[1])
