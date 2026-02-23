import json
import os
from collections import OrderedDict
import math

# Configuration
INPUT_FILE = 'sorted_formatted_persona_data.json'  # Your database with Race and P3 stats
OUTPUT_FILE = 'balanced_sorted_formatted_persona_data.json'  # The final, fully balanced database

# --- SMT III Engine Balancing Constants ---
BASE_STAT_BUDGET = 20
POINTS_PER_LEVEL = 1
STAT_CAP = 40


def normalize_demon_stats(demon):
    """
    Transforms a demon's stats from the Persona 3 Reload point pool
    to the Shin Megami Tensei III point pool using Proportional Representation.
    """
    new_demon = OrderedDict()

    # 1. Preserve Metadata (No fallbacks needed)
    new_demon["Id"] = demon["Id"]
    new_demon["Name"] = demon["Name"]
    new_demon["Race"] = demon["Race"]
    new_demon["Level"] = demon["Level"]

    # 2. STAT NORMALIZATION & RE-DISTRIBUTION
    old_stats = demon.get("Stats", {})
    new_stats = OrderedDict()

    # --- Step A: Calculate Proportions from Old Data ---
    total_old_points = sum(old_stats.values())
    proportions = {}
    if total_old_points > 0:
        for key, val in old_stats.items():
            proportions[key] = val / total_old_points
    else:
        for key in old_stats.keys():
            proportions[key] = 1.0 / len(old_stats)

    # --- Step B: Phase 1 - Distribute BASE STATS (The 20 Point Pool) ---
    base_points = {}
    for key in old_stats.keys():
        base_points[key] = math.floor(BASE_STAT_BUDGET * proportions.get(key, 0))

    base_remainder = BASE_STAT_BUDGET - sum(base_points.values())
    sorted_stats = sorted(proportions.items(), key=lambda item: item[1], reverse=True)
    for i in range(base_remainder):
        key_to_increment = sorted_stats[i % len(sorted_stats)][0]
        base_points[key_to_increment] += 1

    # --- Step C: Phase 2 - Distribute LEVELED STATS (The Level-1 Pool) ---
    level = demon.get("Level", 1)
    leveled_points_budget = (level - 1) * POINTS_PER_LEVEL
    leveled_points_dist = {}
    for key in old_stats.keys():
        leveled_points_dist[key] = math.floor(leveled_points_budget * proportions.get(key, 0))

    leveled_remainder = leveled_points_budget - sum(leveled_points_dist.values())
    for i in range(leveled_remainder):
        key_to_increment = sorted_stats[i % len(sorted_stats)][0]
        leveled_points_dist[key_to_increment] += 1

    # --- Step D: Final Combination & Capping ---
    for key in base_points:
        final_stat = base_points[key] + leveled_points_dist.get(key, 0)
        new_stats[key] = min(STAT_CAP, final_stat)

    new_demon["Stats"] = new_stats

    # 3. Preserve Affinities and Skills (No changes needed)
    new_demon["Affinities"] = demon.get("Affinities", {})
    new_demon["BaseSkills"] = demon.get("BaseSkills", [])
    new_demon["LearnedSkills"] = demon.get("LearnedSkills", {})

    return new_demon


def main():
    print("--- JRPG ENGINE: FINAL STAT NORMALIZATION ---")

    if not os.path.exists(INPUT_FILE):
        print(f"[ERROR] Source file '{INPUT_FILE}' not found.")
        return

    with open(INPUT_FILE, 'r', encoding='utf-8') as f:
        data = json.load(f)

    print(f"[SYSTEM] Normalizing stats for {len(data)} demons...")

    transformed_data = [normalize_demon_stats(demon) for demon in data]

    with open(OUTPUT_FILE, 'w', encoding='utf-8') as f:
        json.dump(transformed_data, f, indent=2, ensure_ascii=False)

    print("\n--- NORMALIZATION COMPLETE ---")
    print(f"[SUCCESS] Final database saved as: {OUTPUT_FILE}")


if __name__ == "__main__":
    main()