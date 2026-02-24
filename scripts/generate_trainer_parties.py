import json
import os
import random

ASSETS_ROOT = r"D:\Projects\Starfield-2026\src\Starfield2026.Assets"
CHARACTERS_ROOT = os.path.join(ASSETS_ROOT, "Models", "Characters")
POKEMON_ROOT = os.path.join(ASSETS_ROOT, "Models", "Pokemon")
OUTPUT_PATH = os.path.join(ASSETS_ROOT, "trainer_parties.json")

POKEMON_SOURCES = ["scarlet", "sun-moon"]
PARTY_SIZE = 6

def get_all_trainers_by_generation():
    trainers_by_gen = {}
    
    for gen in os.listdir(CHARACTERS_ROOT):
        gen_path = os.path.join(CHARACTERS_ROOT, gen)
        if not os.path.isdir(gen_path):
            continue
        
        trainers = set()
        
        def scan_for_trainers(path):
            if not os.path.isdir(path):
                return
            for item in os.listdir(path):
                item_path = os.path.join(path, item)
                if not os.path.isdir(item_path):
                    continue
                
                if item.startswith("tr") and item != "trainers":
                    trainers.add(item)
                else:
                    scan_for_trainers(item_path)
        
        scan_for_trainers(gen_path)
        if trainers:
            trainers_by_gen[gen] = sorted(trainers)
    
    return trainers_by_gen

def get_all_pokemon():
    pokemon = []
    for source in POKEMON_SOURCES:
        source_path = os.path.join(POKEMON_ROOT, source)
        if not os.path.isdir(source_path):
            print(f"Warning: {source_path} not found")
            continue
        for folder in os.listdir(source_path):
            if folder.startswith("pm"):
                rel_path = f"{source}/{folder}"
                pokemon.append(rel_path)
    return pokemon

def generate_random_party(pokemon_list):
    return random.sample(pokemon_list, min(PARTY_SIZE, len(pokemon_list)))

def main():
    random.seed(42)
    
    print("Scanning trainers by generation...")
    trainers_by_gen = get_all_trainers_by_generation()
    
    total_trainers = sum(len(t) for t in trainers_by_gen.values())
    print(f"Found {total_trainers} trainers across {len(trainers_by_gen)} generations:")
    for gen, trainers in sorted(trainers_by_gen.items()):
        print(f"  {gen}: {len(trainers)} trainers")
    
    print("\nScanning Pokemon...")
    pokemon = get_all_pokemon()
    print(f"Found {len(pokemon)} Pokemon from {POKEMON_SOURCES}")
    
    print("\nGenerating parties...")
    parties = {}
    for gen, trainers in sorted(trainers_by_gen.items()):
        for trainer in trainers:
            key = f"{gen}/{trainer}"
            party = generate_random_party(pokemon)
            parties[key] = party
    
    print(f"Writing to {OUTPUT_PATH}...")
    with open(OUTPUT_PATH, 'w', encoding='utf-8') as f:
        json.dump(parties, f, indent=2)
    
    print(f"Done! Generated parties for {len(parties)} trainers")
    
    print("\nSample output (first 5 entries):")
    for i, (key, party) in enumerate(parties.items()):
        if i >= 5:
            break
        print(f"  {key}: {party[:2]}...")

if __name__ == "__main__":
    main()
