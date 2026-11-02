"""Convert icon-source.jpg to a multi-resolution Windows icon (requires Pillow)."""
from pathlib import Path
from PIL import Image, ImageOps

asset_dir = Path(__file__).resolve().parent
with Image.open(asset_dir / "icon-source.jpg") as source:
    picture = ImageOps.contain(ImageOps.exif_transpose(source).convert("RGBA"), (256, 256), Image.Resampling.LANCZOS)
    image = Image.new("RGBA", (256, 256), (0, 0, 0, 0))
    image.alpha_composite(picture, ((256 - picture.width) // 2, (256 - picture.height) // 2))
    image.save(asset_dir / "bl4cktap.ico", sizes=[(16, 16), (24, 24), (32, 32), (48, 48), (64, 64), (128, 128), (256, 256)])
