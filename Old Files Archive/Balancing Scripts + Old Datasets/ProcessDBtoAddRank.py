import json
import os
from itertools import groupby

# --- Configuration ---
SORTED_DATABASE_FILE = "final_unified_database_sorted.json"
PROCESSED_DATABASE_FILE = "final_unified_database_processed.json"


def add_rank_to_entries():
    source_file_path = os.path.join(SORTED_DATABASE_FILE)
    output_file_path = os.path.join(PROCESSED_DATABASE_FILE)

    if not os.path.exists(source_file_path):
        print(f"Error: {SORTED_DATABASE_FILE} not found at {source_file_path}")
        print("Please run sort_db.py first.")
        return

    with open(source_file_path, 'r', encoding='utf-8') as f:
        data = json.load(f)

    print(f"Loaded {len(data)} entries from {SORTED_DATABASE_FILE}.")

    processed_data = []

    # Ensure 'Race' is present for grouping before sorting
    for entry in data:
        if 'Race' not in entry:
            entry['Race'] = entry.get('Arcana', 'Unclassified_Race')

            # Re-sort just to be absolutely certain of grouping, even if Script 1 wasn't run
    data.sort(key=lambda x: (x.get('Race', 'Unknown_Race'), x.get('Level', 0)))

    for race, group in groupby(data, lambda x: x['Race']):
        race_entries = list(group)

        for i, entry in enumerate(race_entries):
            # Create a new dictionary to ensure specific key order
            new_entry = {}
            new_entry['Id'] = entry.get('Id')
            new_entry['Name'] = entry.get('Name')
            new_entry['Race'] = entry.get('Race')
            new_entry['Rank'] = i + 1  # Assign rank here
            new_entry['Level'] = entry.get('Level')

            # Copy remaining properties from the original entry
            for key, value in entry.items():
                if key not in new_entry:  # Don't overwrite already ordered keys
                    new_entry[key] = value

            processed_data.append(new_entry)

    with open(output_file_path, 'w', encoding='utf-8') as f:
        json.dump(processed_data, f, indent=2)

    print(f"Successfully added 'Rank' property to entries with specified order and saved to {PROCESSED_DATABASE_FILE}.")


if __name__ == "__main__":
    add_rank_to_entries()