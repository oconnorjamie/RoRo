import cv2
import numpy as np
import matplotlib.pyplot as plt

def expand_yellow_rope(img_path, output_path='smooth_traced_path.png', resize_dim=128):
    # Load and resize image
    img = cv2.imread(img_path)
    if img is None:
        raise FileNotFoundError(f"Could not load image: {img_path}")

    h, w = img.shape[:2]
    scaling_factor = resize_dim / max(h, w)
    new_size = (int(w * scaling_factor), int(h * scaling_factor))
    resized = cv2.resize(img, new_size, interpolation=cv2.INTER_LANCZOS4)

    # Detect yellow pixels in HSV
    hsv = cv2.cvtColor(resized, cv2.COLOR_BGR2HSV)
    lower_yellow = np.array([2, 2, 2])
    upper_yellow = np.array([255, 255, 255])
    yellow_mask = cv2.inRange(hsv, lower_yellow, upper_yellow)

    # Dilate the yellow mask
    kernel = np.ones((3, 3), np.uint8)
    dilated_mask = cv2.dilate(yellow_mask, kernel, iterations=1)

    # Apply yellow color to mask area
    output = np.zeros_like(resized)
    output[dilated_mask > 0] = (0, 255, 255)

    # Save intermediate result
    cv2.imwrite(output_path, output)
    return output

def connect_yellow_lines(input_path, output_path):
    expanded_img = expand_yellow_rope(input_path)
    
    # Create yellow mask from expanded image
    hsv = cv2.cvtColor(expanded_img, cv2.COLOR_BGR2HSV)
    yellow_mask = cv2.inRange(hsv, (20, 100, 100), (40, 255, 255))

    # Morphological close to connect gaps
    kernel = cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (5,5))
    morphed = cv2.morphologyEx(yellow_mask, cv2.MORPH_CLOSE, kernel, iterations=2)
    
    # Flood fill from bottom row
    h, w = morphed.shape
    flash_mask = np.zeros((h+2, w+2), np.uint8)

    for x in range(w):
        if morphed[h-1, x]:
            cv2.floodFill(
                morphed.copy(),
                flash_mask, 
                (x, h-1), 
                255,
                flags=4 | cv2.FLOODFILL_MASK_ONLY
            )

    # Remove border padding
    final_mask = flash_mask[1:-1, 1:-1]

    # Create final yellow path on black background
    output = np.zeros((h, w, 3), dtype=np.uint8)
    output[final_mask > 0] = (0, 255, 255)

    # Resize to 64x64
    final = cv2.resize(output, (64, 64), interpolation=cv2.INTER_AREA)

    # Convert all non-black pixels to white
    final[np.any(final != [0, 0, 0], axis=-1)] = [255, 255, 255]

    # Convert to grayscale
    final = cv2.cvtColor(final, cv2.COLOR_BGR2GRAY)


    # Save final result
    cv2.imwrite(output_path, final)
    print(f"Saved final connected path to {output_path}")

if __name__ == "__main__":
    connect_yellow_lines('image.png', 'output6.png')
