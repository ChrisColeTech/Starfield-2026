# Sprite Counting Tool

A Python utility to count the number of individual sprites in a sprite sheet image. It detects sprites by identifying connected components of non-transparent pixels.

## Prerequisites

- Python 3.x
- A virtual environment (recommended)

## Setup

1.  **Navigate to the tools directory:**
    ```powershell
    cd tools
    ```

2.  **Create a virtual environment (if not already created):**
    ```powershell
    python -m venv .venv
    ```

3.  **Activate the virtual environment:**
    - On Windows (PowerShell):
      ```powershell
      .\.venv\Scripts\Activate.ps1
      ```
    - On Windows (Command Prompt):
      ```cmd
      .venv\Scripts\activate.bat
      ```
    - On macOS/Linux:
      ```bash
      source .venv/bin/activate
      ```

4.  **Install dependencies:**
    ```powershell
    pip install Pillow
    ```

## Usage

Run the script by passing the path to the sprite sheet image you want to analyze.

```powershell
python count_sprites.py <path_to_image>
```

### Example

To count sprites in the `player_jump.png` file located in the project's content directory:

```powershell
python count_sprites.py "..\src\Starfield\Content\Sprites\player_jump.png"
```

## How It Works

The script loads the image and converts it to RGBA format. It then scans the image pixel by pixel. When it finds a non-transparent pixel that hasn't been visited yet, it increments the sprite count and uses a flood-fill algorithm (BFS) to mark all connected non-transparent pixels as part of the same sprite. This ensures that even complex shapes are counted as a single sprite.
