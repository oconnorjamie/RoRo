import traitlets
import cv2
import numpy as np
import pyzed.sl as sl
import threading
from traitlets.config.configurable import SingletonConfigurable
import ipywidgets.widgets as widgets
from IPython.display import display
import matplotlib.pyplot as plt

class Camera(SingletonConfigurable):
    color_value = traitlets.Any()  # Monitor the color_value variable

    def __init__(self):
        super(Camera, self).__init__()

        self.zed = sl.Camera()
        init_params = sl.InitParameters()
        init_params.camera_resolution = sl.RESOLUTION.HD1080
        init_params.depth_mode = sl.DEPTH_MODE.ULTRA
        init_params.coordinate_units = sl.UNIT.MILLIMETER

        status = self.zed.open(init_params)
        if status != sl.ERROR_CODE.SUCCESS:
            print("Camera Open : " + repr(status) + ". Exit program.")
            self.zed.close()
            exit(1)

        self.runtime = sl.RuntimeParameters()
        self.thread_runnning_flag = False

        camera_info = self.zed.get_camera_information()
        self.width = camera_info.camera_configuration.resolution.width
        self.height = camera_info.camera_configuration.resolution.height
        self.image = sl.Mat(self.width, self.height, sl.MAT_TYPE.U8_C4, sl.MEM.CPU)
        self.depth = sl.Mat(self.width, self.height, sl.MAT_TYPE.F32_C1, sl.MEM.CPU)
        self.point_cloud = sl.Mat(self.width, self.height, sl.MAT_TYPE.F32_C4, sl.MEM.CPU)

    def _capture_frames(self):
        while self.thread_runnning_flag == True:
            if self.zed.grab(self.runtime) == sl.ERROR_CODE.SUCCESS:
                self.zed.retrieve_image(self.image, sl.VIEW.LEFT)
                self.zed.retrieve_measure(self.depth, sl.MEASURE.DEPTH)

                self.color_value = self.image.get_data()
                self.color_value = cv2.cvtColor(self.color_value, cv2.COLOR_BGRA2BGR)
                self.depth_image = np.asanyarray(self.depth.get_data())

    def start(self):
        if self.thread_runnning_flag == False:
            self.thread_runnning_flag = True
            self.thread = threading.Thread(target=self._capture_frames)
            self.thread.start()

    def stop(self):
        if self.thread_runnning_flag == True:
            self.thread_runnning_flag = False
            self.thread.join()

def bgr8_to_jpeg(value):
    return bytes(cv2.imencode('.jpg', value)[1])

camera = Camera()
camera.start()

display_color = widgets.Image(format='jpeg', width='30%')
display_depth = widgets.Image(format='jpeg', width='30%')
layout = widgets.Layout(width='100%')
sidebyside = widgets.HBox([display_color, display_depth], layout=layout)
display(sidebyside)

def expand_yellow_rope_from_frame(img, resize_dim=128):
    h, w = img.shape[:2]
    scaling_factor = resize_dim / max(h, w)
    new_size = (int(w * scaling_factor), int(h * scaling_factor))
    resized = cv2.resize(img, new_size, interpolation=cv2.INTER_LANCZOS4)

    hsv = cv2.cvtColor(resized, cv2.COLOR_BGR2HSV)
    lower_yellow = np.array([2, 2, 2])
    upper_yellow = np.array([255, 255, 255])
    yellow_mask = cv2.inRange(hsv, lower_yellow, upper_yellow)

    kernel = np.ones((3, 3), np.uint8)
    dilated_mask = cv2.dilate(yellow_mask, kernel, iterations=1)

    output = np.zeros_like(resized)
    output[dilated_mask > 0] = (0, 255, 255)
    return output

def connect_yellow_lines_from_frame(img):
    expanded_img = expand_yellow_rope_from_frame(img)

    hsv = cv2.cvtColor(expanded_img, cv2.COLOR_BGR2HSV)
    yellow_mask = cv2.inRange(hsv, (20, 100, 100), (40, 255, 255))

    kernel = cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (5, 5))
    morphed = cv2.morphologyEx(yellow_mask, cv2.MORPH_CLOSE, kernel, iterations=2)

    h, w = morphed.shape
    flash_mask = np.zeros((h + 2, w + 2), np.uint8)

    for x in range(w):
        if morphed[h - 1, x]:
            cv2.floodFill(
                morphed.copy(),
                flash_mask,
                (x, h - 1),
                255,
                flags=4 | cv2.FLOODFILL_MASK_ONLY
            )

    final_mask = flash_mask[1:-1, 1:-1]

    output = np.zeros((h, w, 3), dtype=np.uint8)
    output[final_mask > 0] = (0, 255, 255)

    final = cv2.resize(output, (64, 64), interpolation=cv2.INTER_AREA)
    final[np.any(final != [0, 0, 0], axis=-1)] = [255, 255, 255]
    final = cv2.cvtColor(final, cv2.COLOR_BGR2GRAY)

    return final

def func(change):
    scale = 0.1
    resized_image = cv2.resize(change['new'], None, fx=scale, fy=scale, interpolation=cv2.INTER_AREA)

    yellow_path_mask = connect_yellow_lines_from_frame(resized_image)

    display_color.value = bgr8_to_jpeg(yellow_path_mask)

    depth_colormap = cv2.applyColorMap(cv2.convertScaleAbs(camera.depth_image, alpha=0.03), cv2.COLORMAP_JET)
    resized_depth_colormap = cv2.resize(depth_colormap, None, fx=scale, fy=scale, interpolation=cv2.INTER_AREA)
    display_depth.value = bgr8_to_jpeg(resized_depth_colormap)

camera.observe(func, names=['color_value'])
