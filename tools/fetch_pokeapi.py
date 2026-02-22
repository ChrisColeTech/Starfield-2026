#!/usr/bin/env python3
"""
Fetch Pokemon species data from PokeAPI and output a JSON file
suitable for loading into SpeciesRegistry.

Usage:
    python fetch_pokeapi.py [--gen 7] [--output ../src/Starfield.Assets/Content/Data/species.json]

Ultra Sun/Moon = Gen 7, Pokemon IDs 1-807.
"""

import argparse
import json
import os
import sys
import time
from pathlib import Path

import requests

API_BASE = "https://pokeapi.co/api/v2"

# PokeAPI growth rate name -> our GrowthRate enum name
GROWTH_RATE_MAP = {
    "slow": "Slow",
    "medium": "MediumFast",      # PokeAPI "medium" = our MediumFast (n^3)
    "medium-slow": "MediumSlow",
    "fast": "Fast",
    "erratic": "Erratic",
    "fluctuating": "Fluctuating",
}

# PokeAPI type name -> our MoveType enum name
TYPE_MAP = {
    "normal": "Normal",
    "fire": "Fire",
    "water": "Water",
    "grass": "Grass",
    "electric": "Electric",
    "ice": "Ice",
    "fighting": "Fighting",
    "poison": "Poison",
    "ground": "Ground",
    "flying": "Flying",
    "psychic": "Psychic",
    "bug": "Bug",
    "rock": "Rock",
    "ghost": "Ghost",
    "dragon": "Dragon",
    "dark": "Dark",
    "steel": "Steel",
    "fairy": "Fairy",
}

# PokeAPI stat name -> our field name
STAT_MAP = {
    "hp": "baseHP",
    "attack": "baseAttack",
    "defense": "baseDefense",
    "special-attack": "baseSpAttack",
    "special-defense": "baseSpDefense",
    "speed": "baseSpeed",
}


def get_gen_pokemon_ids(gen: int) -> list[int]:
    """Get all Pokemon IDs up to and including the given generation."""
    # Cumulative Pokemon count per gen
    gen_max = {1: 151, 2: 251, 3: 386, 4: 493, 5: 649, 6: 721, 7: 807, 8: 905, 9: 1025}
    max_id = gen_max.get(gen)
    if max_id is None:
        print(f"Unknown generation {gen}. Supported: 1-9")
        sys.exit(1)
    return list(range(1, max_id + 1))


def fetch_pokemon(pokemon_id: int, session: requests.Session) -> dict | None:
    """Fetch combined pokemon + species data from PokeAPI."""
    # Pokemon endpoint: stats, types, base_experience
    poke_url = f"{API_BASE}/pokemon/{pokemon_id}"
    species_url = f"{API_BASE}/pokemon-species/{pokemon_id}"

    try:
        poke_resp = session.get(poke_url, timeout=30)
        poke_resp.raise_for_status()
        poke = poke_resp.json()

        species_resp = session.get(species_url, timeout=30)
        species_resp.raise_for_status()
        species = species_resp.json()
    except requests.RequestException as e:
        print(f"  ERROR fetching #{pokemon_id}: {e}")
        return None

    # Extract types
    types = sorted(poke["types"], key=lambda t: t["slot"])
    type1 = TYPE_MAP.get(types[0]["type"]["name"], "Normal")
    type2 = TYPE_MAP.get(types[1]["type"]["name"], type1) if len(types) > 1 else type1

    # Extract stats
    stats = {}
    for s in poke["stats"]:
        field = STAT_MAP.get(s["stat"]["name"])
        if field:
            stats[field] = s["base_stat"]

    # Extract growth rate
    growth_raw = species["growth_rate"]["name"]
    growth = GROWTH_RATE_MAP.get(growth_raw, "MediumFast")

    # Proper-case name
    name = species.get("name", "").replace("-", " ").title()
    # Fix common name quirks from PokeAPI
    name_overrides = {
        "Nidoran F": "Nidoran\u2640",
        "Nidoran M": "Nidoran\u2642",
        "Mr Mime": "Mr. Mime",
        "Farfetchd": "Farfetch'd",
        "Ho Oh": "Ho-Oh",
        "Mime Jr": "Mime Jr.",
        "Porygon Z": "Porygon-Z",
        "Porygon2": "Porygon2",
        "Type Null": "Type: Null",
        "Jangmo O": "Jangmo-o",
        "Hakamo O": "Hakamo-o",
        "Kommo O": "Kommo-o",
        "Tapu Koko": "Tapu Koko",
        "Tapu Lele": "Tapu Lele",
        "Tapu Bulu": "Tapu Bulu",
        "Tapu Fini": "Tapu Fini",
    }
    name = name_overrides.get(name, name)

    return {
        "id": pokemon_id,
        "name": name,
        "type1": type1,
        "type2": type2,
        "baseHP": stats.get("baseHP", 0),
        "baseAttack": stats.get("baseAttack", 0),
        "baseDefense": stats.get("baseDefense", 0),
        "baseSpAttack": stats.get("baseSpAttack", 0),
        "baseSpDefense": stats.get("baseSpDefense", 0),
        "baseSpeed": stats.get("baseSpeed", 0),
        "baseEXPYield": poke.get("base_experience") or 0,
        "growthRate": growth,
        "captureRate": species.get("capture_rate", 45),
        "baseHappiness": species.get("base_happiness") or 70,
        "genderRate": species.get("gender_rate", -1),  # -1 = genderless, 0-8 = female eighths
    }


def main():
    parser = argparse.ArgumentParser(description="Fetch Pokemon data from PokeAPI")
    parser.add_argument("--gen", type=int, default=7, help="Generation to fetch through (default: 7 for USUM)")
    parser.add_argument("--output", type=str, default=None, help="Output JSON path")
    parser.add_argument("--start", type=int, default=1, help="Start ID (for resuming)")
    parser.add_argument("--batch", type=int, default=50, help="Print progress every N pokemon")
    args = parser.parse_args()

    if args.output is None:
        script_dir = Path(__file__).parent.parent
        args.output = str(script_dir / "src" / "Starfield.Assets" / "Content" / "Data" / "species.json")

    pokemon_ids = get_gen_pokemon_ids(args.gen)
    total = len(pokemon_ids)
    print(f"Fetching Gen 1-{args.gen} Pokemon data ({total} species) from PokeAPI...")
    print(f"Output: {args.output}")

    # Load existing progress if resuming
    results = []
    existing_ids = set()
    if args.start > 1 and os.path.exists(args.output):
        with open(args.output, "r", encoding="utf-8") as f:
            results = json.load(f)
            existing_ids = {r["id"] for r in results}
        print(f"Resuming from #{args.start} ({len(existing_ids)} already fetched)")

    session = requests.Session()
    session.headers["User-Agent"] = "Starfield-DataFetcher/1.0"

    remaining = [pid for pid in pokemon_ids if pid not in existing_ids and pid >= args.start]
    fetched = 0
    errors = 0

    for pid in remaining:
        data = fetch_pokemon(pid, session)
        if data:
            results.append(data)
            fetched += 1
        else:
            errors += 1
            # Retry once after a pause
            time.sleep(2)
            data = fetch_pokemon(pid, session)
            if data:
                results.append(data)
                fetched += 1
                errors -= 1

        # Progress
        if fetched % args.batch == 0 or pid == remaining[-1]:
            print(f"  [{fetched + len(existing_ids)}/{total}] #{pid} {data['name'] if data else '???'}")

        # Be nice to the API — PokeAPI asks for reasonable rate limiting
        time.sleep(0.1)

        # Save checkpoint every 100
        if fetched % 100 == 0 and fetched > 0:
            save_results(args.output, results)
            print(f"  Checkpoint saved ({len(results)} total)")

    # Sort by ID and save
    results.sort(key=lambda r: r["id"])
    save_results(args.output, results)

    print(f"\nDone! {fetched} fetched, {errors} errors, {len(results)} total in {args.output}")

    # Print a quick sample
    if results:
        sample = results[0]
        print(f"\nSample entry (#{sample['id']} {sample['name']}):")
        print(json.dumps(sample, indent=2))


def save_results(path: str, results: list[dict]):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "w", encoding="utf-8") as f:
        json.dump(results, f, indent=2, ensure_ascii=False)


if __name__ == "__main__":
    main()
