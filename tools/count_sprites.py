import argparse
from PIL import Image

def count_sprites(image_path):
    """
    Counts the number of sprites in a sprite sheet by finding connected components
    of non-transparent pixels.
    """
    try:
        img = Image.open(image_path).convert("RGBA")
    except Exception as e:
        print(f"Error opening image: {e}")
        return

    width, height = img.size
    pixels = img.load()
    visited = set()
    sprite_count = 0

    def get_neighbors(x, y):
        neighbors = []
        if x > 0: neighbors.append((x - 1, y))
        if x < width - 1: neighbors.append((x + 1, y))
        if y > 0: neighbors.append((x, y - 1))
        if y < height - 1: neighbors.append((x, y + 1))
        return neighbors

    for y in range(height):
        for x in range(width):
            if (x, y) in visited:
                continue

            r, g, b, a = pixels[x, y]
            if a > 0:  # Non-transparent pixel found
                sprite_count += 1
                # BFS/DFS to mark all connected non-transparent pixels
                queue = [(x, y)]
                visited.add((x, y))
                while queue:
                    cx, cy = queue.pop(0)
                    for nx, ny in get_neighbors(cx, cy):
                        if (nx, ny) not in visited:
                            nr, ng, nb, na = pixels[nx, ny]
                            if na > 0:
                                visited.add((nx, ny))
                                queue.append((nx, ny))
    
    print(f"Found {sprite_count} sprites in {image_path}")
    return sprite_count

if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="Count sprites in a sprite sheet.")
    parser.add_argument("image_path", help="Path to the sprite sheet image.")
    args = parser.parse_args()
    count_sprites(args.image_path)
