from pathlib import Path
from PIL import Image, ImageDraw

root = Path(r"D:\python\device_alarm\data_watch\outputs\多协议工业设备数据统一采集与实时监控\qa\rendered")
pages = sorted(root.glob("page-*.png"))
for sheet_index in range(0, len(pages), 2):
    selected = pages[sheet_index:sheet_index + 2]
    thumbs = []
    for page in selected:
        image = Image.open(page).convert("RGB")
        image.thumbnail((850, 1100), Image.Resampling.LANCZOS)
        thumbs.append((page.name, image.copy()))
    canvas = Image.new("RGB", (1700, 1160), "#d9dde3")
    draw = ImageDraw.Draw(canvas)
    for offset, (name, image) in enumerate(thumbs):
        x = offset * 850 + (850 - image.width) // 2
        y = 45 + (1100 - image.height) // 2
        canvas.paste(image, (x, y))
        draw.text((offset * 850 + 15, 12), name, fill="#111111")
    output = root / f"contact-{sheet_index // 2 + 1:02d}.jpg"
    canvas.save(output, quality=78, optimize=True)
    print(output)
