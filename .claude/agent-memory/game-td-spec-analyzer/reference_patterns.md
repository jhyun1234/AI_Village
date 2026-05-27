---
name: reference_patterns
description: Reusable risk patterns and schema patterns for bullet-heaven / roguelite genre games
type: reference
---

## Common Risk Patterns — Bullet Heaven / Roguelite

1. Object count explosion: Phase 3 with 1,000+ enemies requires Spatial Hashing (NativeParallelMultiHashMap) + Job System + Burst Compiler. Quad-Tree ruled out (pointer-based, incompatible with Burst). Uniform Grid is fallback if team lacks Job System experience. Pool alone is insufficient if collision checks are O(n^2).
2. Overlap/OverlapCircleAll hot-path: calling Physics2D.OverlapCircleAll every frame per projectile is a known performance killer at scale.
3. Remote Config race condition: game loads before remote config fetch completes — need a loading gate or fallback to bundled default values.
4. Dual-currency exploit surface: any client-side currency mutation without server validation is an exploit vector, especially on PC (Steam).
5. Cross-platform shader compatibility: URP Shader Graph features that work on PC may not compile correctly for OpenGL ES 3.0 (Android) without explicit fallback passes.

## Reusable Schema Patterns

- player_profiles: id, platform_user_id, display_name, created_at, last_login_at
- player_currency: player_id (FK), gold, core, updated_at — always use server-side increment, never trust client value
- run_sessions: session_id, player_id, start_at, end_at, survival_seconds, is_cleared, boss_killed
- weapon_snapshots (JSONB array inside run_sessions): weapon_id, level, evolved_to
- skill_tree_unlocks: player_id, node_id, unlocked_at, currency_type_spent, amount_spent
- balance_config_versions: version_id, applied_at, config_json — for audit trail of remote balance patches

## Asset Naming Conventions (established for this project)
- UI sprites: ui_[screen]_[element]_[state] (e.g., ui_lobby_btn_start_normal)
- Enemy sprites: enemy_[type]_[anim_state] (e.g., enemy_slime_walk)
- Projectile VFX: vfx_proj_[weapon_id] (e.g., vfx_proj_fireball)
- SFX: sfx_[category]_[action] (e.g., sfx_weapon_fire_shotgun)
- BGM: bgm_[phase] (e.g., bgm_phase2_elite)

## Object Pooling Architecture Decisions (established 2026-03-27)

### Chosen Pattern: 3-Layer Hierarchical Pool Manager
- Layer 1: PoolManager Singleton (external interface, Dictionary<PoolKey, IPool>)
- Layer 2: ObjectPool<T> per type (Stack<T> internally for cache-locality)
- Layer 3: Pre-warmed prefab instances (created at LoadingScene, never Instantiated at runtime)
- PoolKey: enum (not string) to avoid hash collision and typo bugs

### Overflow Strategies per Type
- Enemies: Recycle (oldest active first, using circular index buffer)
- Projectiles: Recycle (farthest from player first)
- VFX: Reject (silent discard, pool sized generously)
- SFX: Reject + 3-stage filter (cooldown gate → polyphony limit → audio pool)

### Warm-up Formula
  Pool size = (spawn rate/sec) x (avg lifetime sec) x 1.2 safety factor
  Frame-distributed via UniTask.Yield() batches during LoadingScene
  Config driven by PoolConfigSO (ScriptableObject) — designer-editable in Inspector

### Audio Pool — 3-Stage Filter for high-frequency SFX
  Stage 1: Cooldown gate — same key within 30ms is silently rejected
  Stage 2: Polyphony limit — sfx_enemy_hit capped at 4 concurrent channels
  Stage 3: AudioSource pool — returned automatically after clip.length seconds
  Mobile: AudioClip LoadType = CompressedInMemory for ambient, DecompressOnLoad for short SFX

### Known Pitfalls (validated)
- SetActive cost: prefer transform.position = offScreenPos for very high-frequency show/hide
- Dirty state: IPoolable.OnGet() must zero-out ALL fields including StopAllCoroutines()
- Pool leak: every projectile needs a max-lifetime timer as fallback Release() trigger
- Transform.SetParent: use worldPositionStays=false to skip world-position recalc
- Multi-scene: separate GlobalPool vs ScenePool; dispose ScenePool on sceneUnloaded event
- Async warmup race: PoolManager.IsReady must gate GameStateMachine Loading→Gameplay transition

### Memory Layout
- Hot data (per-frame access): EnemyData struct <= 28 bytes, stored in NativeArray<EnemyData>
- NativeArray: Allocator.Persistent for scene lifetime, must Dispose() in OnDestroy
- struct field order: position(float2,8) + velocity(float2,8) + hp(4) + speed(4) + poolIndex(4) = 28 bytes
- Target: 2 EnemyData per 64-byte CPU cache line
