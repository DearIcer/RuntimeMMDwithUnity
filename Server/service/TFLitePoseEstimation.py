import cv2
import tensorflow as tf
import numpy as np
from functools import partial


class MoveNetPoseModel():
    """MoveNet SinglePose Lightning TFLite wrapper.

    Detects a single person's 17 COCO keypoints on the full frame.
    The published uint8 lite-model takes raw RGB pixels (0-255) as input.
    Keypoint left/right follows the person's own perspective (COCO
    convention), i.e. a person facing the camera has their LEFT shoulder
    on the right side of the image.
    """

    # COCO keypoint indices
    NOSE = 0
    LEFT_EYE = 1
    RIGHT_EYE = 2
    LEFT_EAR = 3
    RIGHT_EAR = 4
    LEFT_SHOULDER = 5
    RIGHT_SHOULDER = 6
    LEFT_ELBOW = 7
    RIGHT_ELBOW = 8
    LEFT_WRIST = 9
    RIGHT_WRIST = 10
    LEFT_HIP = 11
    RIGHT_HIP = 12
    LEFT_KNEE = 13
    RIGHT_KNEE = 14
    LEFT_ANKLE = 15
    RIGHT_ANKLE = 16

    # shoulders, elbows, wrists, hips (the half-body mocap subset)
    UPPER_BODY = (LEFT_SHOULDER, RIGHT_SHOULDER,
                  LEFT_ELBOW, RIGHT_ELBOW,
                  LEFT_WRIST, RIGHT_WRIST,
                  LEFT_HIP, RIGHT_HIP)

    # bones for drawing, as index pairs
    SKELETON = ((LEFT_SHOULDER, RIGHT_SHOULDER),
                (LEFT_SHOULDER, LEFT_ELBOW),
                (RIGHT_SHOULDER, RIGHT_ELBOW),
                (LEFT_ELBOW, LEFT_WRIST),
                (RIGHT_ELBOW, RIGHT_WRIST),
                (LEFT_SHOULDER, LEFT_HIP),
                (RIGHT_SHOULDER, RIGHT_HIP),
                (LEFT_HIP, RIGHT_HIP))

    def __init__(self, filepath):
        # Load the TFLite model and allocate tensors.
        self._interpreter = tf.lite.Interpreter(model_path=filepath)
        self._interpreter.allocate_tensors()

        # model details
        input_details = self._interpreter.get_input_details()
        output_details = self._interpreter.get_output_details()

        # inference helper
        self._set_input_tensor = partial(self._interpreter.set_tensor,
                                         input_details[0]["index"])
        self._get_output_tensor = partial(self._interpreter.get_tensor,
                                          output_details[0]["index"])

        # e.g. (192, 192) for lightning, both H and W of the input tensor
        self.input_shape = tuple(input_details[0]["shape"][1:3])
        self.input_dtype = input_details[0]["dtype"]

    def _preprocessing(self, img):
        """Preprocess the image to meet the model's input requirement.
        Args:
            img: An image in default BGR format.

        Returns:
            image_input: The resized RGB image ready to be feeded.
        """
        image_rgb = cv2.cvtColor(img, cv2.COLOR_BGR2RGB)
        image_resized = cv2.resize(image_rgb, self.input_shape)
        image_input = image_resized.astype(self.input_dtype)
        return image_input[np.newaxis, :]

    def _inference(self, image_input):
        self._set_input_tensor(image_input)
        self._interpreter.invoke()
        return self._get_output_tensor()

    def _postprocessing(self, output):
        """Convert raw model output to (17, 3) keypoints in (x, y, score)
        order, with x/y normalized to [0, 1] relative to the input frame."""
        kps = output.reshape(-1, 3).copy()
        # MoveNet outputs (y, x, score); swap to (x, y, score)
        kps[:, [0, 1]] = kps[:, [1, 0]]
        return kps

    def get_keypoints(self, frame):
        """Detect pose keypoints from the image given.
        Args:
            frame: An image in default BGR format.

        Returns:
            kps: An array of shape (17, 3), one row per keypoint in
                (x, y, score) order, x/y normalized to [0, 1].
        """
        image_input = self._preprocessing(frame)
        output = self._inference(image_input)
        return self._postprocessing(output)

    @staticmethod
    def draw_skeleton(kps, frame, score_threshold=0.3,
                      color=(255, 125, 0), thickness=2):
        """Draw the upper-body skeleton. kps rows are (x, y, score) with
        normalized x/y; frame is BGR and modified in place."""
        h, w = frame.shape[:2]
        pts = np.round(kps[:, :2] * (w, h)).astype(np.int32)
        valid = kps[:, 2] >= score_threshold

        for a, b in MoveNetPoseModel.SKELETON:
            if valid[a] and valid[b]:
                cv2.line(frame, tuple(pts[a]), tuple(pts[b]),
                         color, thickness, cv2.LINE_AA)

        for p in pts[valid]:
            cv2.circle(frame, tuple(p), 3, (0, 125, 255), -1, cv2.LINE_AA)

        return frame


if __name__ == "__main__":
    import sys

    cap = cv2.VideoCapture(sys.argv[1])
    ps = MoveNetPoseModel("weights/movenet_singlepose_lightning.tflite")

    while True:
        ret, frame = cap.read()

        if not ret:
            break

        kps = ps.get_keypoints(frame)
        ps.draw_skeleton(kps, frame)

        cv2.imshow("pose", frame)
        if cv2.waitKey(0) == ord('q'):
            break

    cap.release()
    cv2.destroyAllWindows()
