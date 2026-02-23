import json


def refactor_smt3_stats(input_file, output_file):
    try:
        with open(input_file, 'r', encoding='utf-8') as f:
            data = json.load(f)

        for entry in data:
            if "Stats" in entry and "Level" in entry:
                level = entry["Level"]
                stats = entry["Stats"]

                # 1. Calculate Target and Current Sum
                target_sum = level + 20
                current_sum = sum(stats.values())

                if current_sum == 0:
                    continue  # Skip if stats are missing

                # 2. Calculate Proportional Stats
                new_stats = {}
                for key, value in stats.items():
                    # Calculate ratio and apply to new budget
                    proportional_val = (value / current_sum) * target_sum
                    new_stats[key] = int(round(proportional_val))

                # 3. Handle Rounding Drift
                # Because of rounding, the sum might be slightly off the target
                actual_new_sum = sum(new_stats.values())
                diff = target_sum - actual_new_sum

                if diff != 0:
                    # Find the highest stat to absorb the rounding difference
                    # This prevents low stats from becoming 0 or negative
                    max_stat_key = max(new_stats, key=new_stats.get)
                    new_stats[max_stat_key] += diff

                # 4. Update the entry
                entry["Stats"] = new_stats

        # Save the result
        with open(output_file, 'w', encoding='utf-8') as f:
            json.dump(data, f, indent=4)

        print(f"Refactor complete! Data saved to: {output_file}")
        print("All SMT3 demons now follow the Level + 20 formula.")

    except Exception as e:
        print(f"An error occurred: {e}")


if __name__ == "__main__":
    input_filename = "smtiii_demons.json"
    output_filename = "smt3_balanced_database.json"
    refactor_smt3_stats(input_filename, output_filename)