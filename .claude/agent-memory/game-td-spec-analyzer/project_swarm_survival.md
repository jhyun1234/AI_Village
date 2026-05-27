---
name: project_swarm_survival
description: Core technical decisions and facts established from the Swarm Survival GDD v1.0.0
type: project
---

Project: Swarm Survival
GDD Version: v1.0.0 (analyzed 2026-03-27)
Genre: Top-down Roguelite Bullet Heaven
Target Platform: PC (Steam) + Mobile (iOS/Android) cross-platform
Engine: Unity URP

Key technical decisions established:
- Data-driven architecture via ScriptableObject + Remote Config (AWS EC2 / Supabase)
- Object pooling mandatory — Instantiate/Destroy forbidden at runtime
- GPU Instancing required for 1,000+ enemies on screen
- Dual currency: Gold (in-game grind) / Core (boss clear reward)
- Backend: Supabase (Postgres) for player data + analytics logging
- Wave Editor Tool: custom Unity Editor Scripting tool with timeline visualization
- Analytics: game-over payload (survival time, weapon list, cumulative damage) sent to backend

Milestone schedule:
- Week 1-2: Core engine & optimization foundation
- Week 3-4: Data structures & combat
- Week 5-6: Custom tools & wave system
- Week 7-8: Meta progression & polish, DB integration

Why: These decisions were made to support the USP of 60FPS with massive entity counts and no-downtime balance patching.
How to apply: Always reference these constraints when evaluating new feature requests — any suggestion that adds per-frame Instantiate calls or bypasses the pool should be flagged as a critical risk.

## Collision Detection Architecture (decided 2026-03-27)
- Collision strategy: Custom Spatial Hashing via NativeParallelMultiHashMap (NOT Physics2D.OverlapCircleAll)
- Parallelism: Unity Job System + Burst Compiler (IJobParallelFor) for enemy-projectile hit queries
- Layer strategy: Enemy layer Physics2D collision disabled entirely; custom query replaces it
- Projectile management: Single ProjectileManager MonoBehaviour owns NativeArray<ProjectileData>
- Tick rate: Update()-based variable tick + LOD tick reduction for off-screen projectiles
- Tunneling fix: Bresenham-based Spatial Hash Sweep only for high-speed projectiles
- DOTS ECS decision: Deferred to post-launch v2. Job+Burst hybrid sufficient for 1,000-enemy target.
  Threshold to reconsider: 5,000+ enemies OR Job+Burst optimizations exhausted.
- Cell size formula: max(largest projectile radius, largest enemy hitbox radius) × 2
- Estimated frame time (Snapdragon 865): ~3-4ms for collision with Job+Burst vs ~28ms naive OverlapCircleAll
