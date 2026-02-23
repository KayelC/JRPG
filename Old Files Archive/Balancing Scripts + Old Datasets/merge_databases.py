import json

def merge_databases(main_db_path, persona_db_path, output_path):
    try:
        # Load SMT3 (Main) and Persona (Secondary)
        with open(main_db_path, 'r', encoding='utf-8') as f:
            smt_data = json.load(f)
        with open(persona_db_path, 'r', encoding='utf-8') as f:
            persona_data = json.load(f)

        # 1. Create a set of IDs that exist in the Main Database
        # We lowercase them to ensure 'Nekomata' and 'nekomata' match
        smt_ids = {d['Id'].lower() for d in smt_data}

        # 2. Filter Persona data: Keep only those NOT in SMT3
        unique_personas = []
        skipped_count = 0
        for p in persona_data:
            if p['Id'].lower() in smt_ids:
                print(f"Skipping duplicate Persona: {p['Name']} (Using SMT3 version)")
                skipped_count += 1
            else:
                unique_personas.append(p)

        # 3. Combine the datasets
        merged_list = smt_data + unique_personas

        # 4. Strict Sort by Race, then by Level
        # This ensures the protocol is followed exactly
        merged_list.sort(key=lambda x: (x.get('Race', 'Unknown'), x.get('Level', 0)))

        # 5. Save the final merged database
        with open(output_path, 'w', encoding='utf-8') as f:
            json.dump(merged_list, f, indent=4)

        print("-" * 30)
        print(f"Merge Complete!")
        print(f"Total entries in Main DB: {len(smt_data)}")
        print(f"Unique Personas added: {len(unique_personas)}")
        print(f"Duplicates removed: {skipped_count}")
        print(f"Final database size: {len(merged_list)}")
        print(f"Result saved to: {output_path}")

    except Exception as e:
        print(f"An error occurred during merging: {e}")

if __name__ == "__main__":
    merge_databases(
        "smt3_balanced_database.json",
        "final_balanced_persona_data.json",
        "final_unified_database.json"
    )