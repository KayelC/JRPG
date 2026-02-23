import json


def buff_persona_stats(input_file, output_file):
    try:
        with open(input_file, 'r', encoding='utf-8') as f:
            data = json.load(f)

        for entry in data:
            if "Stats" in entry:
                stats = entry["Stats"]

                # Find the key with the maximum value
                # If there is a tie, max() returns the first one it encounters
                max_stat_key = max(stats, key=stats.get)

                # Increment the highest stat by 1
                stats[max_stat_key] += 1

        # Save the result to a new file
        with open(output_file, 'w', encoding='utf-8') as f:
            json.dump(data, f, indent=4)

        print(f"Success! Refactored data saved to: {output_file}")
        print("Every Persona now follows the Level + 20 formula.")

    except FileNotFoundError:
        print(f"Error: The file '{input_file}' was not found.")
    except json.JSONDecodeError:
        print(f"Error: Failed to decode JSON. Check the file format.")
    except Exception as e:
        print(f"An unexpected error occurred: {e}")


# Run the script
if __name__ == "__main__":
    input_filename = "balanced_sorted_formatted_persona_data.json"
    output_filename = "final_balanced_persona_data.json"
    buff_persona_stats(input_filename, output_filename)