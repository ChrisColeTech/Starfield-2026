#!/usr/bin/env node

// Seed gamedata.db from JSON source files.
// Usage: node seed-gamedata.mjs
//
// Reads:  ../src/Starfield.Assets/Data/species.json
// Writes: ../src/Starfield.Assets/Data/gamedata.db

import { readFileSync, unlinkSync, existsSync } from 'fs'
import { join, dirname } from 'path'
import { fileURLToPath } from 'url'
import Database from 'better-sqlite3'

const __dirname = dirname(fileURLToPath(import.meta.url))
const ASSETS_DATA = join(__dirname, '..', 'src', 'Starfield.Assets', 'Data')

const DB_PATH = join(ASSETS_DATA, 'gamedata.db')
const SPECIES_JSON = join(ASSETS_DATA, 'species.json')

// --- Species ---

function seedSpecies(db) {
  const json = readFileSync(SPECIES_JSON, 'utf-8')
  const species = JSON.parse(json)

  db.exec(`DROP TABLE IF EXISTS species`)
  db.exec(`
    CREATE TABLE species (
      id              INTEGER PRIMARY KEY,
      name            TEXT    NOT NULL,
      hp              INTEGER NOT NULL,
      attack          INTEGER NOT NULL,
      defense         INTEGER NOT NULL,
      sp_attack       INTEGER NOT NULL,
      sp_defense      INTEGER NOT NULL,
      speed           INTEGER NOT NULL,
      type1           TEXT    NOT NULL,
      type2           TEXT,
      base_exp_yield  INTEGER NOT NULL,
      growth_rate     TEXT    NOT NULL,
      catch_rate      INTEGER NOT NULL DEFAULT 45
    )
  `)

  const insert = db.prepare(`
    INSERT INTO species (id, name, hp, attack, defense, sp_attack, sp_defense, speed, type1, type2, base_exp_yield, growth_rate, catch_rate)
    VALUES (@id, @name, @hp, @attack, @defense, @spAttack, @spDefense, @speed, @type1, @type2, @baseExpYield, @growthRate, @catchRate)
  `)

  const insertAll = db.transaction((rows) => {
    for (const row of rows) {
      insert.run(row)
    }
  })

  insertAll(species)
  console.log(`  species: ${species.length} rows`)
  return species.length
}

// --- Main ---

console.log(`Seeding ${DB_PATH}`)
console.log()

// Delete existing DB for a clean rebuild
if (existsSync(DB_PATH)) {
  unlinkSync(DB_PATH)
}

const db = new Database(DB_PATH)
db.pragma('journal_mode = WAL')

let totalRows = 0
totalRows += seedSpecies(db)

// Future tables: seedMoves(db), seedLearnsets(db), seedEvolutions(db), etc.

db.close()
console.log()
console.log(`Done. ${totalRows} total rows in ${DB_PATH}`)
