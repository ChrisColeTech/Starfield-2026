"""
Fetch items from PokeAPI and seed the gamedata.db items table.
Uses batched requests to avoid overwhelming the API.
"""
import sqlite3
import urllib.request
import json
import time

DB_PATH = r"D:\Projects\Starfield-2026\src\Starfield2026.Assets\Data\gamedata.db"
API_BASE = "https://pokeapi.co/api/v2"

# Map PokeAPI category names to our game categories
CATEGORY_MAP = {
    "standard-balls": "Pokeball",
    "special-balls": "Pokeball",
    "apricorn-balls": "Pokeball",
    "healing": "Medicine",
    "status-cures": "Medicine",
    "revival": "Medicine",
    "pp-recovery": "Medicine",
    "vitamins": "Medicine",
    "stat-boosts": "Battle",
    "flutes": "Battle",
    "medicine": "Medicine",
    "berries": "Berry",
    "mail": "Mail",
    "evolution": "EvolutionStone",
    "held-items": "HeldItem",
    "choice": "HeldItem",
    "effort-training": "HeldItem",
    "bad-held-items": "HeldItem",
    "training": "HeldItem",
    "plates": "HeldItem",
    "species-specific": "HeldItem",
    "type-enhancement": "HeldItem",
    "type-protection": "HeldItem",
    "loot": "Valuable",
    "collectibles": "Valuable",
    "all-machines": "TM",
    "all-mail": "Mail",
    "plot-advancement": "KeyItem",
    "gameplay": "KeyItem",
    "event-items": "KeyItem",
    "in-a-+pinch": "Berry",
    "picky-healing": "Berry",
    "type-protection-berry": "Berry",
    "baking-only": "Berry",
}

def get_category(api_category_name):
    """Map a PokeAPI category to our game category."""
    for key, val in CATEGORY_MAP.items():
        if key in api_category_name:
            return val
    return "Valuable"

def fetch_json(url):
    """Fetch JSON from a URL."""
    req = urllib.request.Request(url, headers={"User-Agent": "Starfield2026-ItemSeeder/1.0"})
    with urllib.request.urlopen(req, timeout=15) as resp:
        return json.loads(resp.read().decode())

def main():
    conn = sqlite3.connect(DB_PATH)
    cur = conn.cursor()

    # Create items table
    cur.execute("""
    CREATE TABLE IF NOT EXISTS items (
        id INTEGER PRIMARY KEY,
        name TEXT NOT NULL,
        sprite TEXT NOT NULL DEFAULT '',
        category TEXT NOT NULL,
        buy_price INTEGER NOT NULL DEFAULT 0,
        sell_price INTEGER NOT NULL DEFAULT 0,
        usable_in_battle INTEGER NOT NULL DEFAULT 0,
        usable_overworld INTEGER NOT NULL DEFAULT 0,
        effect TEXT
    )
    """)
    conn.commit()

    # Fetch item list from PokeAPI (Gen 1-3 items, IDs 1-350ish)
    print("Fetching item list from PokeAPI...")
    batch_size = 100
    offset = 0
    all_items = []

    while offset < 400:
        url = f"{API_BASE}/item?offset={offset}&limit={batch_size}"
        print(f"  Batch: offset={offset}, limit={batch_size}")
        data = fetch_json(url)
        results = data.get("results", [])
        if not results:
            break
        all_items.extend(results)
        offset += batch_size
        time.sleep(0.3)

    print(f"Found {len(all_items)} items. Fetching details...")

    inserted = 0
    for i, item_stub in enumerate(all_items):
        try:
            item = fetch_json(item_stub["url"])
        except Exception as e:
            print(f"  SKIP {item_stub['name']}: {e}")
            continue

        item_id = item["id"]
        name = item["name"].replace("-", " ").title()

        # Get category from first category attribute
        cat_name = "Valuable"
        if item.get("category") and item["category"].get("name"):
            cat_name = get_category(item["category"]["name"])

        cost = item.get("cost", 0)
        buy_price = cost
        sell_price = cost // 2

        # Check usability from attributes
        usable_battle = 0
        usable_overworld = 0
        for attr in item.get("attributes", []):
            aname = attr.get("name", "")
            if "usable-in-battle" in aname:
                usable_battle = 1
            if "usable-overworld" in aname:
                usable_overworld = 1

        # Effect: grab short effect text
        effect = None
        for ee in item.get("effect_entries", []):
            if ee.get("language", {}).get("name") == "en":
                effect = ee.get("short_effect", "")[:100]
                break

        cur.execute("""
            INSERT OR REPLACE INTO items (id, name, sprite, category, buy_price, sell_price, usable_in_battle, usable_overworld, effect)
            VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?)
        """, (item_id, name, "", cat_name, buy_price, sell_price, usable_battle, usable_overworld, effect))
        inserted += 1

        if (i + 1) % 20 == 0:
            conn.commit()
            print(f"  Processed {i+1}/{len(all_items)}...")
            time.sleep(0.5)
        else:
            time.sleep(0.2)

    conn.commit()
    print(f"\nDone! Inserted {inserted} items into gamedata.db")

    # Show sample
    cur.execute("SELECT id, name, category FROM items ORDER BY id LIMIT 20")
    for row in cur.fetchall():
        print(f"  {row[0]:>3}: {row[1]:<20} ({row[2]})")

    conn.close()

if __name__ == "__main__":
    main()
