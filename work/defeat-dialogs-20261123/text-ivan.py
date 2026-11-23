from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

folder = Path(__file__).resolve().parent
font = ImageFont.truetype("C:/Windows/Fonts/trebucbd.ttf", 57)
text = Image.new("RGBA", (1448, 160))
ImageDraw.Draw(text).text((724, 92), "Иван погиб.", font=font, fill=(249,242,221,255), anchor="ms")
text.save(folder / "text-ivan.png")
