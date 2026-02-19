import json
import os
from collections import OrderedDict

# Configuration
INPUT_FILE = 'persona_data.json'  # Your old database file
OUTPUT_FILE = 'formatted_persona_data.json'  # The new formatted version

# The high-fidelity column order as established in previous movements
AFFINITY_ORDER = [
    "Strike", "Slash", "Pierce",
    "Fire", "Ice", "Elec", "Wind", "Earth", "Light", "Dark",
    "Mind", "Nerve", "Curse"
]

# Mapping the old Persona 3 stats to SMT III shorthand
STAT_MAP = {
    "STR": "St",
    "MAG": "Ma",
    "END": "Vi",
    "AGI": "Ag",
    "LUK": "Lu"
}


def transform_to_new_schema(old_demon):
    """
    Upgrades an individual demon entry to the new high-fidelity schema.
    Maintains insertion order to ensure Earth is visualy placed between Wind and Light.
    """
    new_demon = OrderedDict()

    # 1. Metadata Normalization
    new_demon["Id"] = old_demon.get("Id", "unknown_id")
    new_demon["Name"] = old_demon.get("Name", "Unknown Name")

    # Translation: Arcana -> Race
    new_demon["Race"] = old_demon.get("Arcana", old_demon.get("Race", "Unknown"))
    new_demon["Level"] = old_demon.get("Level", 1)

    # 2. Stat Key Normalization
    old_stats = old_demon.get("Stats", {})
    new_stats = OrderedDict()

    # We iterate through the target keys to ensure they appear in St, Ma, Vi, Ag, Lu order
    for old_key, new_key in STAT_MAP.items():
        # Retrieve the old value, fallback to 2 if not found
        new_stats[new_key] = old_stats.get(old_key, old_stats.get(new_key, 2))

    new_demon["Stats"] = new_stats

    # 3. Affinity Expansion and Reordering
    old_affs = old_demon.get("Affinities", {})
    new_affs = OrderedDict()

    # Capture the old Physical value to distribute to the three new subtypes
    old_phys_val = old_affs.get("Phys", old_affs.get("Physical", "Normal"))

    # Force the creation of all 13 columns in the correct visual order
    for element in AFFINITY_ORDER:
        if element in ["Strike", "Slash", "Pierce"]:
            # If the old data didn't have specific Strike/Slash/Pierce, use the old Phys value
            new_affs[element] = old_affs.get(element, old_phys_val)
        else:
            # Check if the element exists in old data, otherwise default to Normal
            new_affs[element] = old_affs.get(element, "Normal")

    new_demon["Affinities"] = new_affs

    # 4. Skill Preservation
    new_demon["BaseSkills"] = old_demon.get("BaseSkills", [])
    new_demon["LearnedSkills"] = old_demon.get("LearnedSkills", {})

    return new_demon


def main():
    print("--- JRPG ENGINE: DATABASE SCHEMA TRANSFORMATION ---")

    if not os.path.exists(INPUT_FILE):
        print(f"[ERROR] Source file '{INPUT_FILE}' not found.")
        return

    try:
        with open(INPUT_FILE, 'r', encoding='utf-8') as f:
            old_data = json.load(f)
    except Exception as e:
        print(f"[ERROR] Failed to parse JSON: {e}")
        return

    print(f"[SYSTEM] Transforming {len(old_data)} entries to the High-Fidelity SMT III schema...")

    # Transform every demon in the list
    formatted_data = [transform_to_new_schema(demon) for demon in old_data]

    # Optional: Sort by Level to make the JSON easier to navigate
    formatted_data.sort(key=lambda x: x["Level"])

    try:
        with open(OUTPUT_FILE, 'w', encoding='utf-8') as f:
            # indent=2 ensures a clean visual layout for manual editing
            json.dump(formatted_data, f, indent=2, ensure_ascii=False)

        print("\n--- TRANSFORMATION SUCCESSFUL ---")
        print(f"[SUCCESS] File saved as: {OUTPUT_FILE}")
        print("[INFO] Arcana has been mapped to Race.")
        print("[INFO] Stats have been mapped to St, Ma, Vi, Ag, Lu.")
        print("[INFO] Earth column is correctly seated between Wind and Light.")
        print("[INFO] All 13 affinity columns are now present for every entry.")
    except Exception as e:
        print(f"[ERROR] Failed to save transformed database: {e}")


if __name__ == "__main__":
    main()