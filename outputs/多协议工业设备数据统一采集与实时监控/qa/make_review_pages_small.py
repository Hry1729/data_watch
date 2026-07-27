from pathlib import Path
from PIL import Image

root = Path(r"D:\python\device_alarm\data_watch\outputs\多协议工业设备数据统一采集与实时监控\qa\rendered")
for page in sorted(root.glob("page-*.png")):
    image = Image.open(page).convert("L")
    image.thumbnail((560, 730), Image.Resampling.LANCZOS)
    output = root / page.name.replace("page-", "small-").replace(".png", ".jpg")
    image.save(output, quality=24, optimize=True, progressive=True)
    print(output.name, output.stat().st_size)
