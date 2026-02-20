export interface MapTemplate {
  id: string
  name: string
  file: string
  category: 'core' | 'urban' | 'nature' | 'adventure'
  size: string
}

export const MAP_TEMPLATES: MapTemplate[] = [
  // Core templates
  { id: 'greenleaf_metro', name: 'Greenleaf Metro', file: 'greenleaf_metro.map.json', category: 'core', size: '64x48' },
  { id: 'coastal_route', name: 'Coastal Route', file: 'coastal_route.map.json', category: 'core', size: '25x56' },
  { id: 'evergreen_expanse', name: 'Evergreen Expanse', file: 'evergreen_expanse.map.json', category: 'core', size: '56x44' },
  { id: 'safari_zone', name: 'Safari Zone', file: 'safari_zone.map.json', category: 'core', size: '50x40' },
  { id: 'mt_granite_cave', name: 'Mt. Granite Cave', file: 'mt_granite_cave.map.json', category: 'core', size: '42x34' },
  { id: 'lake_serenity', name: 'Lake Serenity', file: 'lake_serenity.map.json', category: 'core', size: '48x38' },
  { id: 'battle_arena', name: 'Battle Arena', file: 'battle_arena.map.json', category: 'core', size: '36x28' },
  { id: 'department_store', name: 'Department Store', file: 'department_store.map.json', category: 'core', size: '24x18' },

  // Urban / Modern
  { id: 'mega_mall', name: 'Mega Mall', file: 'mega_mall.map.json', category: 'urban', size: '45x35' },
  { id: 'greenleaf_high_school', name: 'High School', file: 'greenleaf_high_school.map.json', category: 'urban', size: '40x30' },
  { id: 'military_compound', name: 'Military Compound', file: 'military_compound.map.json', category: 'urban', size: '70x55' },
  { id: 'neon_downtown', name: 'Neon Downtown', file: 'neon_downtown.map.json', category: 'urban', size: '75x60' },
  { id: 'transit_hub', name: 'Transit Hub', file: 'transit_hub.map.json', category: 'urban', size: '55x40' },
  { id: 'harbor_district', name: 'Harbor District', file: 'harbor_district.map.json', category: 'urban', size: '65x50' },
  { id: 'tech_campus', name: 'Tech Campus', file: 'tech_campus.map.json', category: 'urban', size: '60x45' },
  { id: 'sports_complex', name: 'Sports Complex', file: 'sports_complex.map.json', category: 'urban', size: '50x40' },

  // Nature / Water
  { id: 'open_ocean', name: 'Open Ocean', file: 'open_ocean.map.json', category: 'nature', size: '90x70' },
  { id: 'coral_archipelago', name: 'Coral Archipelago', file: 'coral_archipelago.map.json', category: 'nature', size: '80x65' },
  { id: 'mystic_swamp', name: 'Mystic Swamp', file: 'mystic_swamp.map.json', category: 'nature', size: '65x55' },
  { id: 'volcanic_island', name: 'Volcanic Island', file: 'volcanic_island.map.json', category: 'nature', size: '60x50' },
  { id: 'crystal_caverns', name: 'Crystal Caverns', file: 'crystal_caverns.map.json', category: 'nature', size: '55x45' },
  { id: 'bamboo_forest', name: 'Bamboo Forest', file: 'bamboo_forest.map.json', category: 'nature', size: '60x50' },
  { id: 'frozen_glacier', name: 'Frozen Glacier', file: 'frozen_glacier.map.json', category: 'nature', size: '70x55' },
  { id: 'canyon_river', name: 'Canyon River', file: 'canyon_river.map.json', category: 'nature', size: '30x80' },

  // Adventure / Special
  { id: 'haunted_mansion', name: 'Haunted Mansion', file: 'haunted_mansion.map.json', category: 'adventure', size: '50x40' },
  { id: 'ancient_ruins', name: 'Ancient Ruins', file: 'ancient_ruins.map.json', category: 'adventure', size: '65x55' },
  { id: 'power_plant', name: 'Power Plant', file: 'power_plant.map.json', category: 'adventure', size: '45x35' },
  { id: 'desert_oasis', name: 'Desert Oasis', file: 'desert_oasis.map.json', category: 'adventure', size: '70x55' },
  { id: 'sky_tower', name: 'Sky Tower', file: 'sky_tower.map.json', category: 'adventure', size: '40x55' },
  { id: 'underground_lab', name: 'Underground Lab', file: 'underground_lab.map.json', category: 'adventure', size: '55x45' },
  { id: 'sunken_ship', name: 'Sunken Ship', file: 'sunken_ship.map.json', category: 'adventure', size: '50x40' },
  { id: 'sewer_network', name: 'Sewer Network', file: 'sewer_network.map.json', category: 'adventure', size: '60x50' },
]

export const TEMPLATE_CATEGORIES = [
  { id: 'core', label: 'Core' },
  { id: 'urban', label: 'Urban' },
  { id: 'nature', label: 'Nature' },
  { id: 'adventure', label: 'Adventure' },
] as const

export async function loadTemplate(template: MapTemplate): Promise<string> {
  const resp = await fetch(`/templates/${template.file}`)
  if (!resp.ok) throw new Error(`Failed to load template: ${template.file}`)
  return resp.text()
}
