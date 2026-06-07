# ClassPrestige

<p align="center">
  <img src="https://img.shields.io/badge/TShock-6.1.0-blue?style=for-the-badge" />
  <img src="https://img.shields.io/badge/Terraria-1.4.5.6-green?style=for-the-badge" />
  <img src="https://img.shields.io/badge/.NET-9-purple?style=for-the-badge" />
  <img src="https://img.shields.io/badge/C%23-13-orange?style=for-the-badge" />
  <img src="https://img.shields.io/badge/License-MIT-yellow?style=for-the-badge" />
</p>

<p align="center">
  A class-based RPG progression plugin for TShock servers. Players earn EXP by killing enemies, level up four independent combat classes, achieve prestige ranks, and rebirth for permanent bonuses all without stat power creep.
</p>

---

## Table of Contents

- [Overview](#-overview)
- [Features](#-features)
- [How Progression Works](#-how-progression-works)
- [Leveling System](#-leveling-system)
- [Prestige System](#-prestige-system)
- [Rebirth System](#-rebirth-system)
- [Milestone Rewards](#-milestone-rewards)
- [Anti-Abuse System](#-anti-abuse-system)
- [Commands](#-commands)
- [Permissions](#-permissions)
- [Installation](#-installation)
- [Configuration](#-configuration)
- [Database Support](#-database-support)
- [Performance Notes](#-performance-notes)
- [Troubleshooting](#-troubleshooting)
- [Developer Notes](#-developer-notes)
- [Roadmap](#-roadmap)

---

## ◆ Overview

ClassPrestige transforms Terraria servers into long-term progression environments. Players level four combat classes independently through normal gameplay, earn prestige ranks from cumulative EXP, and eventually rebirth for permanent bonuses that carry across resets.

The plugin is designed for sustained engagement without introducing stat inflation. All progression is cosmetic and bonus based no damage multipliers, no health increases, no game-breaking power.

**Important:** Class levels do NOT affect combat stats. They provide progression tracking, milestone rewards, leaderboard rankings, prestige advancement, and long term goals. No damage, defense, or health modifications are applied.

**Supported Classes:**

| Class | Detection Method |
|-------|-----------------|
| Melee | Melee-type weapons (swords, spears, flails, yoyos) |
| Ranged | Ranged-type weapons (bows, guns, launchers) |
| Magic | Magic-type weapons (staves, spell tomes) |
| Summoner | Summon-type weapons, minion kills, sentry kills |

---

## ◆ Features

**Progression**
- Per-class independent EXP and leveling (Melee, Ranged, Magic, Summoner)
- Prestige ranks with cumulative EXP thresholds
- Rebirth system with permanent bonuses and full progression reset
- Milestone rewards at level thresholds (items, titles, crates)

**Combat Tracking**
- Weapon-based class detection using DamageClass API
- Summoner minion/sentry kill attribution
- Boss damage participation tracking with shared EXP distribution

**Anti-Abuse**
- AFK detection with configurable timeout
- Statue-spawned NPC protection
- Spawn farming diminishing returns
- Event farming EXP reduction

**Infrastructure**
- Leaderboards (Top Levels, Top Prestige, Top Rebirth)
- Auto-save with configurable intervals
- SQLite (default) and MySQL database support
- Async persistence with batch saves
- Hot-reload configuration without restart

---

## ◆ How Progression Works

### Class EXP

Players earn EXP by killing enemies. The weapon held at the time of the kill determines which class receives the EXP. Summoner minion and sentry kills always attribute to the Summoner class regardless of held weapon.

**Base EXP Values:**

| Enemy Type | EXP Awarded |
|-----------|-------------|
| Common Mob | 100 (flat) |
| Rare Mob | 2,500 – 5,000 (random) |
| Boss | 10,000 – 25,000 (random) |

**EXP Pipeline:**

Every kill passes through the full calculation pipeline:

```
Base EXP
  → Apply prestige/rebirth bonus (multiplicative)
  → Apply diminishing returns multiplier (floor)
  → Apply event farming multiplier (floor)
  → Award to class + accumulate prestige EXP
  → Evaluate prestige rank promotion
```

---

## ◆ Leveling System

Each class levels independently from 0 to 100.

**Formula:**

```
Required EXP = max(Level² × 100, 100)
```

**Examples:**

| Current Level | EXP Required | Cumulative EXP |
|--------------|-------------|----------------|
| 0 | 100 | 0 |
| 1 | 100 | 100 |
| 5 | 2,500 | 5,600 |
| 10 | 10,000 | 33,400 |
| 25 | 62,500 | 547,900 |
| 50 | 250,000 | 4,292,400 |
| 100 | 1,000,000 | 33,838,400 |

**Multi-Level-Up:** If a single EXP award exceeds the threshold, the player levels up multiple times in one event. Remaining EXP carries over to the next level.

**Level Cap:** Class levels are capped at 100. EXP continues to accumulate for prestige tracking beyond the cap.

---

## ◆ Prestige System

Prestige ranks are earned through cumulative EXP across all class gains. Every point of EXP awarded to any class also accumulates toward prestige.

**Prestige Ranks:**

| Rank | Cumulative EXP Required | EXP Bonus | Title |
|------|------------------------|-----------|-------|
| I | 500,000 | +2% | (Prestige I) |
| II | 1,500,000 | +4% | (Prestige II) |
| III | 4,000,000 | +6% | (Prestige III) |
| IV | 8,000,000 | +8% | (Prestige IV) |

**Prestige Cycles:** After reaching Prestige IV, every additional 2,500,000 EXP counts as a completed prestige cycle. Cycles are required for rebirth eligibility.

**Bonus Formula:**

```
Bonus = min(prestigeRank × 0.02 + rebirthCount × 0.05, MaxBonusPercent / 100)
```

The default maximum combined bonus is 25%.

---

## ◆ Rebirth System

Rebirth is an end-game reset mechanic. Players who complete enough prestige cycles can rebirth — resetting all class levels, EXP, and prestige progress in exchange for a permanent EXP bonus.

**Requirements:**
- Must be at Prestige IV
- Must have completed the required number of prestige cycles (default: 3)
- Must not have reached the rebirth cap (default: 4)

**Rebirth Progression:**

| Rebirth | Title | Permanent Bonus | Cycles Required |
|---------|-------|----------------|-----------------|
| I | (Reborn I) | +5% | 3 |
| II | (Reborn II) | +10% | 3 |
| III | (Reborn III) | +15% | 3 |
| IV | (Ascended) | +20% | 3 |

**What Resets:**
- All class levels (to 0)
- All class EXP (to 0)
- Prestige rank (to 0)
- Prestige EXP (to 0)
- Prestige cycles (to 0)

**What Persists:**
- Rebirth count
- Unlocked titles
- Unlocked rewards
- Permanent EXP bonus

---

## ◆ Milestone Rewards

When a class reaches a milestone level, configured rewards are automatically granted.

**Default Milestones:**

| Level | Reward Type | Description |
|-------|-------------|-------------|
| 10 | Items | Healing Potions + Gold |
| 25 | Crate | Class Crate I |
| 50 | Title | Exclusive Class Title |
| 75 | Crate | Event Crate + Materials |
| 100 | Crate + Title | Legendary Crate + Cosmetic Title |

**Duplicate Prevention:** Each reward is tracked by a unique key (`{class}_{level}_{index}`). Rewards are never granted twice.

**Full Inventory:** If the player's inventory is full, the reward is queued and delivered on next login or when space becomes available.

Milestones are fully configurable in `config.json`.

---

## ◆ Anti-Abuse System

### AFK Protection

Players who remain stationary for the configured timeout are marked AFK. All kill events are rejected while AFK.

- **Movement threshold:** 2 tiles (32 pixels)
- **Default timeout:** 10 minutes
- **Notification:** One-time message when AFK state activates
- **Recovery:** Move 2+ tiles or perform combat action

### Statue Protection

Kills of NPCs spawned from statues award zero EXP when statue protection is enabled.

### Spawn Farming Reduction

Repeated kills of the same NPC type within a 60-second window apply diminishing returns:

| Kill Count (within 60s) | EXP Multiplier |
|--------------------------|----------------|
| 1st | 100% |
| 2nd | 75% |
| 3rd+ | 50% |

The window resets after 60 seconds of not killing that NPC type.

### Event Farming Reduction

During active wave-based events (Pirate Invasion, Frost Moon, Pumpkin Moon, Martian Madness), EXP is reduced by the configured multiplier (default: 50%).

### Boss Participation

Boss EXP is distributed based on damage contribution. Only players who dealt at least 5% of total damage receive EXP. All eligible participants share a single base EXP roll.

---

## ◆ Commands

### Player Commands

All player commands require the `classprestige.player` permission.

| Command | Description | Example |
|---------|-------------|---------|
| `/level` | Display all class levels and current EXP | `/level` |
| `/classstats` | Detailed per-class stats with EXP to next level | `/classstats` |
| `/prestige` | Prestige rank, cumulative EXP, progress | `/prestige` |
| `/rebirth` | Attempt rebirth or view progress | `/rebirth` |
| `/toplevels` | Top Levels leaderboard | `/toplevels` |
| `/topprestige` | Top Prestige leaderboard | `/topprestige` |
| `/toprebirth` | Top Rebirth leaderboard | `/toprebirth` |
| `/exptoggle` | Toggle EXP gain notifications on/off | `/exptoggle` |
| `/expbonus` | View your EXP bonus breakdown | `/expbonus` |
| `/rebirthinfo` | View rebirth status and requirements | `/rebirthinfo` |
| `/progression` | Learn about progression systems | `/progression` |

**Example Output — /level:**

```
══════════════════════
      LEVELS
══════════════════════
 [i:3507] Melee      Lv. 42
 [i:3019] Ranged     Lv. 18
 [i:3541] Magic      Lv. 55
 [i:3474] Summoner   Lv. 7
```

**Example Output — /prestige:**

```
══════════════════════
      PRESTIGE
══════════════════════
 Rank: Prestige IV
 EXP: 450,000
 Bonus: +8%
 Cycles: 2
```

**Chat Title Display:**

```
(Prestige IV) Mono: hello
(Ascended) Steve: nice work
(Reborn II) Player: hey
```

### Admin Commands

All admin commands require the `classprestige.admin` permission.

| Command | Description | Example |
|---------|-------------|---------|
| `/addexp <player> <class> <amount>` | Add EXP to a player's class | `/addexp Steve Melee 50000` |
| `/setlevel <player> <class> <level>` | Set a player's class level | `/setlevel Steve Magic 100` |
| `/setprestige <player> <rank>` | Set a player's prestige rank | `/setprestige Steve 4` |
| `/resetplayer <player>` | Reset all progression for a player | `/resetplayer Steve` |
| `/reloadlevels` | Reload configuration from disk | `/reloadlevels` |

**Target Resolution:** Admin commands check online players first, then search offline database records. This allows modifying players who are not currently connected.

---

## ◆ Permissions

| Permission | Description | Default Group |
|-----------|-------------|---------------|
| `classprestige.player` | Access to player commands (/level, /classstats, /prestige, /rebirth, /exptoggle, leaderboards) | default (auto-granted) |
| `classprestige.admin` | Access to admin commands (/addexp, /setlevel, /setprestige, /resetplayer, /reloadlevels) | admin |

Add permissions using TShock's group system:

```
/group addperm default classprestige.player
/group addperm admin classprestige.admin
```

---

## ◆ Installation

1. **Stop the TShock server.**

2. **Place the plugin DLL:**

   Copy `ClassPrestige.dll` into the `ServerPlugins/` directory.

3. **Start the server.**

   The plugin will:
   - Create the configuration directory at `{TShock.SavePath}/ClassPrestige/`
   - Generate a default `config.json`
   - Create the SQLite database `classprestige.sqlite`
   - Register all hooks and commands

4. **Verify the plugin loaded:**

   Check the console for:
   ```
   [ClassPrestige] Configuration loaded successfully.
   [ClassPrestige] Auto-save started.
   [ClassPrestige] Leaderboard refresh started.
   [ClassPrestige] Plugin initialized successfully.
   ```

5. **Configure permissions:**

   ClassPrestige automatically grants `classprestige.player` to the default registration group on first startup (configurable via `autoGrantPlayerPermission` in config.json).

   If auto-grant is disabled, add permissions manually:

   ```
   /group addperm default classprestige.player
   /group addperm admin classprestige.admin
   ```

6. **Customize settings** (optional):

   Edit `{TShock.SavePath}/ClassPrestige/config.json` and run `/reloadlevels`.

---

## ◆ Configuration

The configuration file is located at `{TShock.SavePath}/ClassPrestige/config.json`.

All fields have sensible defaults. The file is auto-generated on first run.

### Full Configuration Reference

```json
{
  "maxLevel": 100,
  "commonMobExp": 100,
  "rareMobMinExp": 2500,
  "rareMobMaxExp": 5000,
  "bossMinExp": 10000,
  "bossMaxExp": 25000,
  "bossParticipationPercent": 5,
  "prestigeThresholds": [500000, 1500000, 4000000, 8000000],
  "rebirthCyclesRequired": 3,
  "prestigeCycleExp": 2500000,
  "maxEXPBonusPercent": 25,
  "maxRebirthCount": 4,
  "afkTimeoutMinutes": 10,
  "enableStatueProtection": true,
  "enableSpawnFarmProtection": true,
  "enableEventFarmingReduction": true,
  "eventExpMultiplier": 0.5,
  "rareMobIds": [195, 471, 473, 474, 475, 85],
  "enableLeaderboards": true,
  "leaderboardTopCount": 10,
  "leaderboardRefreshMinutes": 5,
  "databaseType": "sqlite",
  "mySQLHost": "localhost",
  "mySQLDatabase": "classprestige",
  "mySQLUser": "root",
  "mySQLPassword": "",
  "autoSaveIntervalMinutes": 5,
  "milestones": {},
  "autoGrantPlayerPermission": true,
  "enableExpNotifications": true,
  "expNotificationDefaultState": true,
  "enableFancyUI": true,
  "enableItemIcons": true
}
```

### Configuration Sections

**Leveling:**

| Field | Default | Description |
|-------|---------|-------------|
| `maxLevel` | 100 | Maximum level a class can reach |
| `commonMobExp` | 100 | Flat EXP for common mobs |
| `rareMobMinExp` | 2500 | Minimum EXP for rare mobs |
| `rareMobMaxExp` | 5000 | Maximum EXP for rare mobs |
| `bossMinExp` | 10000 | Minimum EXP for boss kills |
| `bossMaxExp` | 25000 | Maximum EXP for boss kills |
| `bossParticipationPercent` | 5 | Minimum damage % for boss EXP eligibility |

**Prestige and Rebirth:**

| Field | Default | Description |
|-------|---------|-------------|
| `prestigeThresholds` | [500K, 1.5M, 4M, 8M] | Cumulative EXP for ranks I–IV |
| `rebirthCyclesRequired` | 3 | Prestige cycles needed per rebirth |
| `prestigeCycleExp` | 2,500,000 | EXP per prestige cycle beyond rank IV |
| `maxEXPBonusPercent` | 25 | Maximum combined bonus cap |
| `maxRebirthCount` | 4 | Maximum rebirths allowed |

**Anti-Abuse:**

| Field | Default | Description |
|-------|---------|-------------|
| `afkTimeoutMinutes` | 10 | Minutes before AFK detection |
| `enableStatueProtection` | true | Reject statue-spawned NPC kills |
| `enableSpawnFarmProtection` | true | Enable diminishing returns |
| `enableEventFarmingReduction` | true | Reduce EXP during events |
| `eventExpMultiplier` | 0.5 | Event EXP multiplier (0.0–1.0) |
| `rareMobIds` | [195, 471, ...] | NPC type IDs classified as rare |

**Database:**

| Field | Default | Description |
|-------|---------|-------------|
| `databaseType` | "sqlite" | Provider: "sqlite" or "mysql" |
| `mySQLHost` | "localhost" | MySQL server hostname |
| `mySQLDatabase` | "classprestige" | MySQL database name |
| `mySQLUser` | "root" | MySQL username |
| `mySQLPassword` | "" | MySQL password |

**Leaderboards:**

| Field | Default | Description |
|-------|---------|-------------|
| `enableLeaderboards` | true | Enable leaderboard system |
| `leaderboardTopCount` | 10 | Entries per leaderboard |
| `leaderboardRefreshMinutes` | 5 | Cache refresh interval |

**Persistence:**

| Field | Default | Description |
|-------|---------|-------------|
| `autoSaveIntervalMinutes` | 5 | Batch save interval for dirty records |

**Permissions:**

| Field | Default | Description |
|-------|---------|-------------|
| `autoGrantPlayerPermission` | true | Auto-add `classprestige.player` to the default group on startup |

**EXP Notifications:**

| Field | Default | Description |
|-------|---------|-------------|
| `enableExpNotifications` | true | Master switch for EXP gain messages (server-wide) |
| `expNotificationDefaultState` | true | Default notification state for new players |

**UI:**

| Field | Default | Description |
|-------|---------|-------------|
| `enableFancyUI` | true | Enable decorated headers and colored output |
| `enableItemIcons` | true | Show Terraria item icons in command output |

---

## ◆ Database Support

### SQLite (Default)

No configuration required. The database file is created automatically at:

```
{TShock.SavePath}/ClassPrestige/classprestige.sqlite
```

Recommended for single-server deployments and development.

### MySQL

For multi-server deployments or centralized data management:

1. Create a MySQL database:

   ```sql
   CREATE DATABASE classprestige;
   ```

2. Update `config.json`:

   ```json
   {
     "databaseType": "mysql",
     "mySQLHost": "your-mysql-host",
     "mySQLDatabase": "classprestige",
     "mySQLUser": "your-user",
     "mySQLPassword": "your-password"
   }
   ```

3. Restart the server. Tables are created automatically on first initialization.

**Note:** Database settings cannot be changed via `/reloadlevels`. A server restart is required to switch providers.

---

## ◆ Performance Notes

ClassPrestige is designed for high-population servers (100+ concurrent players):

- **In-memory cache:** All active player data is stored in a `ConcurrentDictionary` for lock-free concurrent reads.
- **Synchronous hot path:** Kill event processing operates on cached data without async state machines or allocations.
- **Deferred persistence:** Database writes are batched to the auto-save timer. Only dirty records are persisted.
- **Throttled updates:** AFK position tracking is throttled to once per 500ms regardless of server tick rate.
- **Cached leaderboards:** Leaderboard queries run on a timer and serve cached results to players.
- **Cooperative cancellation:** All async operations propagate `CancellationToken` for clean shutdown.
- **Shutdown save:** All cached records are persisted synchronously on server shutdown with a 30-second timeout.

---

## ◆ Troubleshooting

### Commands say "progression data could not be found"

**Cause:** Player data was not loaded into the cache.

**Fix:**
- Ensure the player is logged in (`/login` or `/register`)
- Check console for `[ClassPrestige] OnPlayerPostLogin` messages
- Verify `classprestige.player` permission is assigned

### Commands say "You must be logged in to use this command"

**Cause:** The player has not authenticated with TShock.

**Fix:** Players must use `/login` or `/register` before ClassPrestige commands work.

### No EXP is gained from killing mobs

**Cause:** Several possible reasons:

- Player is not authenticated (check console for "Player not authenticated" in kill validation)
- Player is AFK (check for AFK notification)
- Held weapon has no positive damage or unrecognized DamageType
- NPC was statue-spawned and protection is enabled

**Fix:** Verify the player is logged in and actively moving with a damage-dealing weapon equipped.

### Prestige rank does not increment

**Cause:** Prestige evaluation was not triggered after EXP modification.

**Fix:** This was fixed in version 1.0.1. Ensure you are running the latest build. All EXP paths (mob kills, boss kills, admin commands) now call `EvaluatePrestige` after accumulating prestige EXP.

### Plugin does not load

**Cause:** Missing dependencies or wrong TShock version.

**Fix:**
- Verify TShock 6.1.0 is installed
- Verify .NET 9 runtime is available
- Check `ServerPlugins/` contains only `ClassPrestige.dll`
- Check console for error messages during startup

### Database errors

**Cause:** File permissions (SQLite) or connection issues (MySQL).

**Fix:**
- SQLite: Ensure the TShock save directory is writable
- MySQL: Verify host, credentials, and database existence
- Check console for `[ClassPrestige]` prefixed error messages

---

## ◆ Developer Notes

### Project Structure

```
ClassPrestige/
├── ClassPrestigePlugin.cs        Plugin entry point, DI wiring, hook registration
├── Permissions.cs                Static permission constants
├── Commands/
│   ├── PlayerCommands.cs         Player-facing commands
│   └── AdminCommands.cs          Administrative commands
├── Hooks/
│   ├── KillHooks.cs              NPC kill/strike event handlers
│   ├── SaveHooks.cs              Join/leave/save/update handlers
│   └── ChatHooks.cs              Chat title display, command passthrough
├── Managers/
│   ├── PlayerManager.cs          Cache, leveling, persistence
│   ├── ExpManager.cs             EXP pipeline, class detection, boss tracking
│   ├── AntiAbuseManager.cs       AFK, statue, DR, events
│   ├── PrestigeManager.cs        Rank evaluation, cycle tracking
│   ├── RebirthManager.cs         Reset logic, title assignment
│   ├── RewardManager.cs          Milestone detection, delivery, queuing
│   └── LeaderboardManager.cs     Cached rankings, periodic refresh
├── UI/
│   └── UiHelper.cs               Colors, icons, formatting utilities
├── Models/
│   ├── ClassType.cs              Enum: Melee, Ranged, Magic, Summoner
│   ├── PlayerData.cs             Complete player progression record
│   ├── BossFight.cs              Boss damage tracking state
│   ├── KillWindow.cs             Diminishing returns window
│   ├── KillValidationResult.cs   Anti-abuse validation output
│   ├── LeaderboardEntry.cs       Ranked leaderboard record
│   ├── LeaderboardCategory.cs    Leaderboard type enum
│   ├── MilestoneReward.cs        Reward definition
│   └── PendingReward.cs          Queued reward for retry
├── Database/
│   ├── SqliteDatabase.cs         SQLite IDatabase implementation
│   └── MysqlDatabase.cs          MySQL IDatabase implementation
├── Interfaces/
│   ├── IDatabase.cs              Database abstraction
│   ├── IExpSource.cs             Extensible EXP source interface
│   └── IKillValidator.cs         Kill validation interface
└── Config/
    ├── ConfigManager.cs          JSON load/save/reload
    └── PluginConfig.cs           Configuration model
```

### Architecture Principles

- **Primary constructors** for dependency injection (C# 13)
- **CancellationToken propagation** through all async chains
- **ConfigureAwait(false)** on all library-level awaits
- **try/catch isolation** in all hook handlers — failures never crash the server
- **Consistent cache key:** `player.Account.Name` everywhere
- **System.Text.Json** for all serialization (no Newtonsoft.Json dependency)

---

## ◆ Progression Philosophy

ClassPrestige is designed around three interlocking progression layers:

**Class Levels** are the primary progression path. Players naturally level all four classes through normal gameplay. Milestones reward dedication at key thresholds. Level 100 represents mastery of a class.

**Prestige** is the secondary milestone system. It accumulates passively as players earn EXP across all classes, providing incremental bonuses and cosmetic titles. Prestige IV requires substantial investment — roughly equivalent to leveling multiple classes past 50.

**Rebirth** is the long-term endgame. It requires sustained commitment past Prestige IV, rewarding players with permanent bonuses that carry through full progression resets. Achieving Ascended status represents hundreds of hours of engagement.

The system rewards dedication without introducing stat power creep. A new player and an Ascended player deal the same damage — the difference is cosmetic prestige, bonus EXP speed, and community recognition.

**Approximate Milestones (with default EXP rates):**

| Phase | Approximate Time | Achievement |
|-------|-----------------|-------------|
| Early game | First few hours | Class levels 10-15, approaching Prestige I |
| Mid game | 10-20 hours | Class levels 25-35, Prestige II-III |
| Late game | 40-60 hours | Class levels 50-60, Prestige IV |
| Endgame | 100+ hours | Class levels 75+, Rebirth I |
| Mastery | 300+ hours | Level 100s, Ascended |

---

## ◆ Roadmap

Future development directions:

| Feature | Description |
|---------|-------------|
| Mining EXP | EXP from mining ores and gems |
| Fishing EXP | EXP from fishing catches |
| Crafting EXP | EXP from crafting items |
| Party EXP Sharing | Shared EXP for nearby party members |
| Guild Progression | Group-level prestige tracking |
| Seasonal Prestige Ladders | Time-limited competitive rankings |
| PvP Integration | EXP from PvP kills with anti-boosting |
| Achievement System | Challenge-based milestones and titles |

---

## Changelog

### v1.0.0

- Per-class EXP and leveling system (Melee, Ranged, Magic, Summoner)
- Prestige ranks (I-IV) with configurable thresholds
- Rebirth system (4 tiers + Ascended) with permanent EXP bonuses
- Anti-abuse: AFK detection, statue protection, spawn farming diminishing returns, event reduction
- Boss participation tracking with shared EXP distribution
- Milestone rewards at level thresholds (items, titles, crates)
- Leaderboards (Top Levels, Top Prestige, Top Rebirth)
- SQLite and MySQL database support with automatic migration
- Auto-save system with configurable intervals
- Premium RPG-style UI with colored output and Terraria item icons
- Chat titles with parentheses format: (Prestige IV), (Ascended), (Reborn II)
- Toggleable EXP gain notifications (/exptoggle)
- First-time player tutorial (shown once on login)
- /expbonus, /rebirthinfo, /progression informational commands
- Automatic permission grant for default player group
- Hot-reload configuration via /reloadlevels
- Prestige rank auto-validation on data load
- Boss contribution percentage in EXP notifications
- Balanced progression: 500K/1.5M/4M/8M prestige thresholds with 2.5M cycle EXP

---

## License

MIT License. See [LICENSE](LICENSE) for details.
