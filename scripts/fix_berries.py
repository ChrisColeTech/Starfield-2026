import sqlite3

db_path = r"D:\Projects\Starfield-2026\src\Starfield2026.Assets\Data\gamedata.db"
conn = sqlite3.connect(db_path)
cur = conn.cursor()

# Fix berry categories - PokeAPI IDs 125-178 are berries
cur.execute("UPDATE items SET category = 'Berry' WHERE id BETWEEN 125 AND 178")
print(f"Updated {cur.rowcount} items to Berry category")

conn.commit()

# Verify
cur.execute("SELECT id, name, category FROM items WHERE category = 'Berry' ORDER BY id LIMIT 10")
for row in cur.fetchall():
    print(f"  {row[0]:>3}: {row[1]:<20} ({row[2]})")

conn.close()
