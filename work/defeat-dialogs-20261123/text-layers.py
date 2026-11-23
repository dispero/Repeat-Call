from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

folder = Path(__file__).resolve().parent
font = ImageFont.truetype("C:/Windows/Fonts/trebucbd.ttf", 57)
messages = {
    "approach": (
        "Бандит увидел ваше приближение",
        "и застрелил вашего отца",
    ),
    "accomplices": (
        "Бандит понял, что вы убили обоих его",
        "напарников, и застрелил вашего отца",
    ),
}

for key, lines in messages.items():
    mask = Image.new("L", (1448, 160))
    draw = ImageDraw.Draw(mask)
    for line, baseline in zip(lines, (59, 125)):
        assert draw.textlength(line, font=font) <= 1250
        draw.text((724, baseline), line, font=font, fill=255, anchor="ms")
    text_layer = Image.new("RGBA", mask.size, (249, 242, 221, 0))
    text_layer.putalpha(mask)
    text_layer.save(folder / f"text-{key}.png")
