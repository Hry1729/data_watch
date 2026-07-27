from pathlib import Path
from PIL import Image, ImageChops

root = Path(r"D:\python\device_alarm\data_watch\outputs\多协议工业设备数据统一采集与实时监控\mermaid_figures")
for path in sorted(root.glob("fig_*.png")):
    image = Image.open(path).convert("RGB")
    background = Image.new("RGB", image.size, "white")
    difference = ImageChops.difference(image, background).convert("L")
    difference = difference.point(lambda value: 255 if value > 8 else 0)
    bbox = difference.getbbox()
    if bbox is None:
        continue
    padding = 24
    left = max(0, bbox[0] - padding)
    top = max(0, bbox[1] - padding)
    right = min(image.width, bbox[2] + padding)
    bottom = min(image.height, bbox[3] + padding)
    cropped = image.crop((left, top, right, bottom))
    cropped.save(path, optimize=True)
    print(path.name, image.size, "->", cropped.size)
