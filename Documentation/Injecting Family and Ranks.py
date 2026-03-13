import json
import os

def migrate_skills(file_path):
    if not os.path.exists(file_path):
        print(f"Error: {file_path} not found.")
        return

    with open(file_path, 'r', encoding='utf-8') as f:
        data = json.load(f)

    for category, skills in data.items():
        for skill in skills:
            # Inject new properties if they don't exist
            if "Family" not in skill:
                skill["Family"] = "-"
            if "Rank" not in skill:
                skill["Rank"] = "-"

    # Write back to a new file to prevent data loss
    output_path = file_path.replace(".json", "_migrated.json")
    with open(output_path, 'w', encoding='utf-8') as f:
        json.dump(data, f, indent=4)

    print(f"Migration complete. New file created at: {output_path}")

# Run the migration
migrate_skills('skills_database.json')