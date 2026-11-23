from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
import numpy as np

folder = Path(__file__).resolve().parent
template = Image.open(folder / "template.png").convert("RGB")
sample = template.crop((205, 885, 1245, 980)).convert("L").point(lambda x: 255 if x > 130 else 0)
sample = sample.crop(sample.getbbox())
phrase = "Нажмите R, чтобы повторить"
fonts_folder = Path("C:/Windows/Fonts")
candidate_names = ("arialbd.ttf", "segoeuib.ttf", "seguisb.ttf", "trebucbd.ttf", "verdanab.ttf", "tahomabd.ttf", "calibrib.ttf", "bahnschrift.ttf", "gothicb.ttf", "aptos-bold.ttf")
values = []
for name in candidate_names:
    path = fonts_folder / name
    if not path.is_file():
        continue
    font = ImageFont.truetype(path, 59)
    layer = Image.new("L", (1200, 100))
    ImageDraw.Draw(layer).text((1, 1), phrase, font=font, fill=255)
    layer = layer.crop(layer.getbbox()).resize(sample.size, Image.Resampling.BILINEAR)
    score = (np.asarray(layer) > 128) == (np.asarray(sample) > 128)
    values.append((float(score.mean()), name, font.getbbox(phrase), font.getlength(phrase)))
print("Reference", sample.size)
print(*sorted(values, reverse=True), sep="\n")
