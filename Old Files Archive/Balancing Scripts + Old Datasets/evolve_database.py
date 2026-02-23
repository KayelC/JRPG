import json
import os
from collections import OrderedDict

# Configuration
INPUT_FILE = 'smtiii_demons.json'
OUTPUT_FILE = 'smtiii_demons_reordered.json'


def reorder_demon_affinities(demon):
    """
    Creates a new affinity dictionary with 'Earth' inserted
    specifically after 'Wind' and before 'Light'.
    """
    if "Affinities" not in demon:
        return demon

    old_affs = demon["Affinities"]
    new_affs = OrderedDict()

    # Define the default value for the new column
    default_earth_val = "Normal"

    # We iterate through the existing keys and rebuild the dictionary
    # to enforce the specific column order.
    for key, value in old_affs.items():
        # Add the current key
        new_affs[key] = value

        # Logic: If we just added 'Wind', immediately insert 'Earth'
        if key == "Wind":
            # If Earth already exists (from a previous run), we skip
            # to avoid duplicates, otherwise we inject the new column.
            if "Earth" not in old_affs:
                new_affs["Earth"] = default_earth_val

    # Safety check: if 'Wind' was missing for some reason,
    # ensure Earth is still added before Light or at least exists.
    if "Earth" not in new_affs:
        # Re-verify if we need to insert it before Light if Wind was missing
        final_affs = OrderedDict()
        inserted = False
        for k, v in new_affs.items():
            if k == "Light" and not inserted:
                final_affs["Earth"] = default_earth_val
                inserted = True
            final_affs[k] = v

        # If still not found (no Wind and no Light), append to end
        if "Earth" not in final_affs:
            final_affs["Earth"] = default_earth_val

        demon["Affinities"] = final_affs
    else:
        demon["Affinities"] = new_affs

    return demon


def main():
    print("--- JRPG ENGINE: DATABASE RESTRUCTURING ---")

    if not os.path.exists(INPUT_FILE):
        print(f"[ERROR] Source file '{INPUT_FILE}' not found.")
        return

    try:
        with open(INPUT_FILE, 'r', encoding='utf-8') as f:
            data = json.load(f)
    except Exception as e:
        print(f"[ERROR] Failed to parse JSON: {e}")
        return

    print(f"[SYSTEM] Restructuring {len(data)} demons...")

    # Process every demon in the database
    evolved_data = [reorder_demon_affinities(demon) for demon in data]

    try:
        with open(OUTPUT_FILE, 'w', encoding='utf-8') as f:
            # indent=2 and ensure_ascii=False keeps the JSON human-readable for manual editing
            json.dump(evolved_data, f, indent=2, ensure_ascii=False)
    except Exception as e:
        print(f"[ERROR] Failed to save restructured database: {e}")
        return

    print("--- RESTRUCTURE COMPLETE ---")
    print(f"[SUCCESS] File saved as: {OUTPUT_FILE}")
    print("[INFO] The 'Earth' column is now seated between 'Wind' and 'Light'.")
    print("[INFO] All manual skills, levels, and stats have been preserved.")


if __name__ == "__main__":
    main()