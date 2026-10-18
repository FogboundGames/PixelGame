from PIL import Image, ImageDraw, ImageFilter

def create_container_shadow():
    w, h = 256, 256
    img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    
    # Shadow ellipse / rounded rectangle
    draw = ImageDraw.Draw(img)
    # The container footprint is wider than it is deep
    rect = [32, 48, w - 32, h - 48]
    draw.rounded_rectangle(rect, radius=32, fill=(0, 0, 0, 160))
    
    img = img.filter(ImageFilter.GaussianBlur(18))
    
    out_path = "Assets/UI/CargoContainer_Shadow.png"
    img.save(out_path, "PNG")
    print(f"Saved container shadow to {out_path}")

if __name__ == "__main__":
    create_container_shadow()
