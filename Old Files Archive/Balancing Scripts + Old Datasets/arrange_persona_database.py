import json
import os
from collections import OrderedDict

# Configuration
INPUT_FILE = 'formatted_persona_data.json'  # Your manually edited, race-updated file
OUTPUT_FILE = 'sorted_formatted_persona_data.json'  # The final, organized database


def main():
    print("--- JRPG ENGINE: DATABASE FINALIZATION & SORTING ---")

    if not os.path.exists(INPUT_FILE):
        print(f"[ERROR] Source file '{INPUT_FILE}' not found. Ensure the file is in the same directory.")
        return

    try:
        with open(INPUT_FILE, 'r', encoding='utf-8') as f:
            demon_data = json.load(f)
    except Exception as e:
        print(f"[ERROR] Failed to parse JSON data: {e}")
        return

    print(f"[SYSTEM] Loaded {len(demon_data)} demons for sorting.")

    # High-Fidelity Sorting Logic:
    # 1. Primary Sort Key: Race (alphabetical)
    # 2. Secondary Sort Key: Level (ascending)
    sorted_data = sorted(demon_data, key=lambda x: (x.get("Race", "Unknown"), x.get("Level", 999)))

    try:
        with open(OUTPUT_FILE, 'w', encoding='utf-8') as f:
            # indent=2 and ensure_ascii=False keeps the JSON human-readable for manual editing
            json.dump(sorted_data, f, indent=2, ensure_ascii=False)
    except Exception as e:
        print(f"[ERROR] Failed to save sorted database: {e}")
        return

    print("\n--- SORTING COMPLETE ---")
    print(f"[SUCCESS] Fully sorted and finalized database saved to: {OUTPUT_FILE}")
    print("[INFO] All demons are now grouped by Race and ordered by Level.")


if __name__ == "__main__":
    main()