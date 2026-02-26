import json
import os

# --- Configuration ---
SOURCE_DATABASE_FILE = "final_unified_database.json"
SORTED_DATABASE_FILE = "final_unified_database_sorted.json"

def sort_database_entries():
    source_file_path = os.path.join(SOURCE_DATABASE_FILE)
    output_file_path = os.path.join(SORTED_DATABASE_FILE)

    if not os.path.exists(source_file_path):
        print(f"Error: {SOURCE_DATABASE_FILE} not found at {source_file_path}")
        return

    with open(source_file_path, 'r', encoding='utf-8') as f:
        data = json.load(f)

    print(f"Loaded {len(data)} entries from {SOURCE_DATABASE_FILE}.")

    # Sort entries: Primary key is 'Race', secondary key is 'Level'
    # Ensure 'Race' exists for all entries for consistent sorting
    for entry in data:
        if 'Race' not in entry:
            # Fallback for old JSON structure, assuming Arcana is now Race
            entry['Race'] = entry.get('Arcana', 'Unclassified_Race') # Add a default if neither exist

    sorted_data = sorted(data, key=lambda x: (x.get('Race', 'Unknown_Race'), x.get('Level', 0)))

    with open(output_file_path, 'w', encoding='utf-8') as f:
        json.dump(sorted_data, f, indent=2)

    print(f"Successfully sorted entries from {SOURCE_DATABASE_FILE} and saved to {SORTED_DATABASE_FILE}.")

if __name__ == "__main__":
    sort_database_entries()