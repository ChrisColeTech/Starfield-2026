#!/usr/bin/env python3
"""
Fetch Pokemon game data from PokeAPI and write directly into gamedata.db.

Downloads moves, learnsets, and evolution chains in batches of 100
with a cooldown between batches to avoid throttling.

Usage:
    python fetch-gamedata.py                  # fetch all
    python fetch-gamedata.py --only moves     # fetch only moves
    python fetch-gamedata.py --only learnsets
    python fetch-gamedata.py --only evolutions

Writes directly to: src/Starfield.Assets/Data/gamedata.db
"""

import argparse
import sqlite3
import sys
import time
from pathlib import Path

import requests
from tqdm import tqdm

API_BASE = "https://pokeapi.co/api/v2"
SCRIPT_DIR = Path(__file__).parent
DB_PATH = SCRIPT_DIR.parent / "src" / "Starfield.Assets" / "Data" / "gamedata.db"

# Gen 7 (Ultra Sun/Ultra Moon)
MAX_SPECIES_ID = 807
MAX_MOVE_ID = 728
VERSION_GROUP = "ultra-sun-ultra-moon"
FALLBACK_VERSION_GROUP = "sun-moon"

BATCH_SIZE = 100
BATCH_COOLDOWN = 6  # seconds between batches

TYPE_MAP = {
    "normal": "Normal", "fire": "Fire", "water": "Water", "grass": "Grass",
    "electric": "Electric", "ice": "Ice", "fighting": "Fighting", "poison": "Poison",
    "ground": "Ground", "flying": "Flying", "psychic": "Psychic", "bug": "Bug",
    "rock": "Rock", "ghost": "Ghost", "dragon": "Dragon", "dark": "Dark",
    "steel": "Steel", "fairy": "Fairy",
}

CATEGORY_MAP = {
    "physical": "Physical",
    "special": "Special",
    "status": "Status",
}


# --- Helpers ---

def fetch_json(session: requests.Session, url: str) -> dict | None:
    try:
        resp = session.get(url, timeout=30)
        if resp.status_code == 404:
            return None
        resp.raise_for_status()
        return resp.json()
    except requests.RequestException as e:
        tqdm.write(f"    ERROR: {e}")
        return None


def create_session() -> requests.Session:
    session = requests.Session()
    session.headers["User-Agent"] = "Starfield-DataFetcher/2.0"
    return session


def batch_cooldown(bar: tqdm):
    """Show a countdown in the progress bar during cooldown."""
    for remaining in range(BATCH_COOLDOWN, 0, -1):
        bar.set_postfix_str(f"cooldown {remaining}s")
        time.sleep(1)
    bar.set_postfix_str("")


# --- Schema ---

def ensure_schema(conn: sqlite3.Connection):
    conn.executescript("""
        CREATE TABLE IF NOT EXISTS moves (
            id          INTEGER PRIMARY KEY,
            name        TEXT NOT NULL,
            type        TEXT NOT NULL,
            category    TEXT NOT NULL,
            power       INTEGER NOT NULL DEFAULT 0,
            accuracy    INTEGER NOT NULL DEFAULT 0,
            pp          INTEGER NOT NULL DEFAULT 0,
            priority    INTEGER NOT NULL DEFAULT 0
        );

        CREATE TABLE IF NOT EXISTS learnsets (
            species_id  INTEGER NOT NULL,
            move_id     INTEGER NOT NULL,
            method      TEXT NOT NULL,
            level       INTEGER NOT NULL DEFAULT 0,
            PRIMARY KEY (species_id, move_id, method)
        );

        CREATE TABLE IF NOT EXISTS evolutions (
            from_species_id INTEGER NOT NULL,
            to_species_id   INTEGER NOT NULL,
            trigger         TEXT NOT NULL,
            min_level       INTEGER,
            item            TEXT,
            held_item       TEXT,
            known_move      TEXT,
            known_move_type TEXT,
            min_happiness   INTEGER,
            time_of_day     TEXT,
            gender          INTEGER,
            PRIMARY KEY (from_species_id, to_species_id, trigger)
        );
    """)


def get_existing_ids(conn: sqlite3.Connection, table: str, id_col: str = "id") -> set[int]:
    rows = conn.execute(f"SELECT DISTINCT {id_col} FROM {table}").fetchall()
    return {r[0] for r in rows}


# --- Moves ---

def fetch_and_insert_moves(conn: sqlite3.Connection, session: requests.Session):
    existing = get_existing_ids(conn, "moves")
    ids = [i for i in range(1, MAX_MOVE_ID + 1) if i not in existing]

    if not ids:
        print(f"  All {len(existing)} moves already in DB, skipping.")
        return

    print(f"  {len(existing)} existing, {len(ids)} to fetch")
    inserted = 0
    errors = 0

    bar = tqdm(total=len(ids), desc="  Moves", unit="move", ncols=80)

    for batch_start in range(0, len(ids), BATCH_SIZE):
        batch = ids[batch_start:batch_start + BATCH_SIZE]

        rows = []
        for move_id in batch:
            data = fetch_json(session, f"{API_BASE}/move/{move_id}")
            if data is None:
                errors += 1
            else:
                rows.append((
                    data["id"],
                    data["name"].replace("-", " ").title(),
                    TYPE_MAP.get(data["type"]["name"], "Normal"),
                    CATEGORY_MAP.get(data["damage_class"]["name"], "Physical"),
                    data["power"] or 0,
                    data["accuracy"] or 0,
                    data["pp"] or 0,
                    data.get("priority", 0),
                ))
            bar.update(1)

        if rows:
            conn.executemany(
                "INSERT OR IGNORE INTO moves (id, name, type, category, power, accuracy, pp, priority) VALUES (?,?,?,?,?,?,?,?)",
                rows
            )
            conn.commit()
            inserted += len(rows)

        if batch_start + BATCH_SIZE < len(ids):
            batch_cooldown(bar)

    bar.close()
    print(f"  Done: {inserted} inserted, {errors} errors")


# --- Learnsets ---

def fetch_and_insert_learnsets(conn: sqlite3.Connection, session: requests.Session):
    existing = get_existing_ids(conn, "learnsets", "species_id")
    ids = [i for i in range(1, MAX_SPECIES_ID + 1) if i not in existing]

    if not ids:
        print(f"  All {len(existing)} species learnsets already in DB, skipping.")
        return

    print(f"  {len(existing)} existing, {len(ids)} species to fetch")
    inserted = 0
    errors = 0

    bar = tqdm(total=len(ids), desc="  Learnsets", unit="spc", ncols=80)

    for batch_start in range(0, len(ids), BATCH_SIZE):
        batch = ids[batch_start:batch_start + BATCH_SIZE]

        rows = []
        for species_id in batch:
            data = fetch_json(session, f"{API_BASE}/pokemon/{species_id}")
            if data is None:
                errors += 1
                bar.update(1)
                continue

            for move_entry in data.get("moves", []):
                move_url = move_entry["move"]["url"]
                move_id = int(move_url.rstrip("/").split("/")[-1])

                # Try USUM first, fall back to SM
                found = False
                for vg in (VERSION_GROUP, FALLBACK_VERSION_GROUP):
                    for vgd in move_entry.get("version_group_details", []):
                        if vgd["version_group"]["name"] != vg:
                            continue
                        method = vgd["move_learn_method"]["name"]
                        level = vgd["level_learned_at"]
                        rows.append((species_id, move_id, method, level))
                        found = True
                    if found:
                        break

            bar.update(1)

        if rows:
            conn.executemany(
                "INSERT OR IGNORE INTO learnsets (species_id, move_id, method, level) VALUES (?,?,?,?)",
                rows
            )
            conn.commit()
            inserted += len(rows)
            bar.set_postfix_str(f"{inserted} entries")

        if batch_start + BATCH_SIZE < len(ids):
            batch_cooldown(bar)

    bar.close()
    print(f"  Done: {inserted} entries inserted, {errors} errors")


# --- Evolutions ---

def flatten_chain(chain: dict) -> list[tuple]:
    """Recursively flatten an evolution chain into rows."""
    results = []
    from_url = chain["species"]["url"]
    from_id = int(from_url.rstrip("/").split("/")[-1])

    for evo in chain.get("evolves_to", []):
        to_url = evo["species"]["url"]
        to_id = int(to_url.rstrip("/").split("/")[-1])

        for detail in evo.get("evolution_details", []):
            trigger = detail["trigger"]["name"]
            results.append((
                from_id,
                to_id,
                trigger,
                detail.get("min_level"),
                detail["item"]["name"] if detail.get("item") else None,
                detail["held_item"]["name"] if detail.get("held_item") else None,
                detail["known_move"]["name"] if detail.get("known_move") else None,
                detail["known_move_type"]["name"] if detail.get("known_move_type") else None,
                detail.get("min_happiness"),
                detail.get("time_of_day") or None,
                detail.get("gender"),
            ))

        results.extend(flatten_chain(evo))

    return results


def fetch_and_insert_evolutions(conn: sqlite3.Connection, session: requests.Session):
    existing_pairs = conn.execute("SELECT DISTINCT from_species_id, to_species_id FROM evolutions").fetchall()

    if len(existing_pairs) > 0:
        print(f"  {len(existing_pairs)} evolution pairs already in DB, skipping.")
        print(f"  (DROP TABLE evolutions to refetch)")
        return

    max_chain = 500
    ids = list(range(1, max_chain + 1))

    print(f"  Fetching up to {max_chain} evolution chains")
    inserted = 0

    bar = tqdm(total=len(ids), desc="  Evolutions", unit="chain", ncols=80)

    for batch_start in range(0, len(ids), BATCH_SIZE):
        batch = ids[batch_start:batch_start + BATCH_SIZE]

        rows = []
        for chain_id in batch:
            data = fetch_json(session, f"{API_BASE}/evolution-chain/{chain_id}")
            if data is not None:
                chain_rows = flatten_chain(data["chain"])
                rows.extend(chain_rows)
            bar.update(1)

        if rows:
            conn.executemany(
                """INSERT OR IGNORE INTO evolutions
                   (from_species_id, to_species_id, trigger, min_level, item, held_item,
                    known_move, known_move_type, min_happiness, time_of_day, gender)
                   VALUES (?,?,?,?,?,?,?,?,?,?,?)""",
                rows
            )
            conn.commit()
            inserted += len(rows)
            bar.set_postfix_str(f"{inserted} records")

        if batch_start + BATCH_SIZE < len(ids):
            batch_cooldown(bar)

    bar.close()
    print(f"  Done: {inserted} evolution records inserted")


# --- Main ---

def main():
    parser = argparse.ArgumentParser(description="Fetch game data from PokeAPI into gamedata.db")
    parser.add_argument("--only", type=str, choices=["moves", "learnsets", "evolutions"],
                        help="Fetch only one data type")
    args = parser.parse_args()

    if not DB_PATH.exists():
        print(f"ERROR: {DB_PATH} not found. Run seed-gamedata.mjs first.")
        sys.exit(1)

    conn = sqlite3.connect(str(DB_PATH))
    ensure_schema(conn)

    session = create_session()
    targets = [args.only] if args.only else ["moves", "learnsets", "evolutions"]

    print(f"PokeAPI -> {DB_PATH}")
    print(f"  Batch: {BATCH_SIZE}, cooldown: {BATCH_COOLDOWN}s")
    print()

    start = time.time()

    if "moves" in targets:
        print(f"=== Moves (1-{MAX_MOVE_ID}) ===")
        fetch_and_insert_moves(conn, session)
        print()

    if "learnsets" in targets:
        print(f"=== Learnsets (species 1-{MAX_SPECIES_ID}) ===")
        fetch_and_insert_learnsets(conn, session)
        print()

    if "evolutions" in targets:
        print("=== Evolution Chains ===")
        fetch_and_insert_evolutions(conn, session)
        print()

    conn.close()

    elapsed = time.time() - start
    minutes = int(elapsed // 60)
    seconds = int(elapsed % 60)
    print(f"Total time: {minutes}m {seconds}s")


if __name__ == "__main__":
    main()
