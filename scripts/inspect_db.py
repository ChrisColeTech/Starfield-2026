import sqlite3

db_path = r"D:\Projects\Starfield-2026\src\Starfield2026.Assets\Data\gamedata.db"
conn = sqlite3.connect(db_path)
cur = conn.cursor()

# Find the items we need for CreateTestInventory
names = ["Potion", "Super Potion", "Hyper Potion", "Antidote", "Parlyz Heal",
         "Full Heal", "Revive", "Poke Ball", "Great Ball", "Oran Berry",
         "Sitrus Berry", "Pecha Berry"]

for name in names:
    cur.execute("SELECT id, name, category FROM items WHERE LOWER(name) = LOWER(?)", (name,))
    row = cur.fetchone()
    if row:
        print(f"  {row[0]:>3}: {row[1]:<15} ({row[2]})")
    else:
        print(f"  ???: {name:<15} NOT FOUND")

conn.close()
