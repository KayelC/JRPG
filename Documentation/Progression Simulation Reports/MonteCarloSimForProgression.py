import math
import random
import statistics

# --- CONFIGURATION ---
EXP_DIVISOR = 50
MACCA_MULTIPLIER = 0.25
SIMULATION_RUNS = 1000
MAX_LEVEL = 99
OUTPUT_FILENAME = "simulation_report.txt"


# --- FORMULAS (Matching your C# Logic) ---

def get_exp_required(level):
    return int(1.5 * math.pow(level, 3))


def calculate_exp_yield(enemy_level, enemy_stats):
    base_yield = (1.5 * math.pow(enemy_level, 3)) / EXP_DIVISOR
    expected_stats = (enemy_level * 3) + 15
    stat_mult = 1.0 + max(0, (enemy_stats - expected_stats) / 100.0)
    final_exp = base_yield * stat_mult
    # THE FIX: Ensure a minimum of 1 EXP to prevent infinite loops
    return max(1, int(final_exp))


def calculate_macca_yield(enemy_level, enemy_luck):
    base_macca = MACCA_MULTIPLIER * math.pow(enemy_level, 2)
    luck_bonus = enemy_luck * 10
    variance = random.uniform(0.9, 1.1)
    return int((base_macca + luck_bonus) * variance)


# --- ENCOUNTER GENERATION ---

def generate_encounter(player_level):
    party_size = random.choices([1, 2, 3, 4], weights=[10, 20, 35, 35], k=1)[0]
    enemies = []
    for _ in range(party_size):
        lvl = max(1, player_level + random.randint(-2, 2))
        stats = int(((lvl * 3) + 15) * random.uniform(0.9, 1.1))
        luck = max(1, int(stats * 0.1))
        enemies.append((lvl, stats, luck))
    return enemies


# --- SIMULATION LOOP ---

def run_simulation():
    level = 1
    current_exp = 0
    total_macca = 0
    battles_per_level = {}
    macca_at_level = {}

    while level < MAX_LEVEL:
        req = get_exp_required(level)
        battles_this_level = 0
        while current_exp < req:
            group = generate_encounter(level)
            battle_exp = sum(calculate_exp_yield(lvl, stats) for lvl, stats, _ in group)
            battle_macca = sum(calculate_macca_yield(lvl, luk) for lvl, _, luk in group)
            current_exp += battle_exp
            total_macca += battle_macca
            battles_this_level += 1
        current_exp -= req
        battles_per_level[level] = battles_this_level
        macca_at_level[level] = total_macca
        level += 1
    return battles_per_level, macca_at_level


# --- AGGREGATION & REPORTING ---

print(f"Running {SIMULATION_RUNS} simulations...")
all_battles = []
all_macca = []

for i in range(SIMULATION_RUNS):
    if (i + 1) % 100 == 0:
        print(f"  ...completed simulation {i + 1} / {SIMULATION_RUNS}")
    b, m = run_simulation()
    all_battles.append(b)
    all_macca.append(m)

print(f"Simulation complete. Saving report to '{OUTPUT_FILENAME}'...")

with open(OUTPUT_FILENAME, 'w') as f:
    f.write("MONTE CARLO SIMULATION REPORT\n")
    f.write("=============================\n\n")
    f.write(f"Configuration:\n")
    f.write(f" - Simulation Runs: {SIMULATION_RUNS}\n")
    f.write(f" - EXP Divisor: {EXP_DIVISOR}\n")
    f.write(f" - Macca Multiplier: {MACCA_MULTIPLIER}\n\n")
    f.write("=== PACING ANALYSIS (Battles per Level) ===\n")
    f.write(f"{'Level':<10} | {'Avg Battles':<15}\n")
    f.write("-" * 30 + "\n")
    checkpoints = [1, 5, 10, 20, 30, 40, 50, 60, 70, 80, 90, 98]
    for lvl in checkpoints:
        b_list = [run[lvl] for run in all_battles if lvl in run]
        avg_b = statistics.mean(b_list) if b_list else 0
        f.write(f"Lv {lvl:<7} | {avg_b:<15.1f}\n")
    f.write("\n=== ECONOMY ANALYSIS (Accumulated Wealth) ===\n")
    f.write(f"{'Level':<10} | {'Total Macca':<15}\n")
    f.write("-" * 30 + "\n")
    for lvl in checkpoints:
        m_list = [run[lvl] for run in all_macca if lvl in run]
        avg_m = statistics.mean(m_list) if m_list else 0
        f.write(f"Lv {lvl:<7} | {int(avg_m):<15,}\n")
    f.write("-" * 30 + "\n")

print("Done.")