# Graph Report - Veilward  (2026-11-05)

## Corpus Check
- Corpus is ~7,978 words - fits in a single context window. You may not need a graph.

## Summary
- 516 nodes · 730 edges · 73 communities (38 shown, 35 thin omitted)
- Extraction: 98% EXTRACTED · 2% INFERRED · 0% AMBIGUOUS · INFERRED: 11 edges (avg confidence: 0.89)
- Token cost: 0 input · 0 output

## Community Hubs (Navigation)
- Built-in Utility Modules
- Game Design Architecture
- Development Package Dependencies
- UI Testing Packages
- Bootstrap Code Tests
- Serialization Pipeline Packages
- UI Media Modules
- Imaging Web Modules
- Input UI Packages
- Bootstrap Documentation Verification
- Core Manifest Modules
- Feature Manifest Dependencies
- Navigation AI Packages
- Physics Tilemap Modules
- Terrain Physics Modules
- Searcher Package Metadata
- Collaboration Proxy Package
- Rider IDE Package
- Animation Module Metadata
- Asset Bundle Module
- Director Module Metadata
- Particle System Module
- Web Request Dependencies
- Asset Bundle Web
- URP Config Package
- Shader Graph Package
- Timeline Package Metadata
- Accessibility Module Metadata
- Android JNI Module
- Audio Module Metadata
- Umbra Module Metadata
- Analytics Module Metadata
- Web Audio Module
- Legacy Web Module
- Wind Module Metadata
- Render Core Package
- URP Package Metadata
- Package Lock References
- Rider Manifest Reference
- Visual Studio Reference
- Input System Reference
- Accessibility Manifest Reference
- Adaptive Performance Reference
- Android JNI Reference
- Asset Bundle Reference
- Audio Manifest Reference
- Cloth Manifest Reference
- Director Manifest Reference
- Image Conversion Reference
- JSON Serialization Reference
- Particle System Reference
- Physics Manifest Reference
- Physics 2D Reference
- Screen Capture Reference
- Terrain Physics Reference
- Tilemap Manifest Reference
- Analytics Manifest Reference
- Asset Bundle Web Reference
- Web Audio Reference
- Web Texture Reference
- Legacy Web Reference
- Vehicles Manifest Reference
- Video Manifest Reference
- Wind Manifest Reference
- XR Manifest Reference
- Multiplayer Center Reference
- Pipeline Manifest Reference
- URP Manifest Reference
- Test Framework Reference
- Timeline Manifest Reference
- uGUI Manifest Reference
- Visual Scripting Reference
- Edit Mode Test Runner

## God Nodes (most connected - your core abstractions)
1. `com.unity.modules.jsonserialize@1.0.0` - 17 edges
2. `com.unity.modules.physics@1.0.0` - 13 edges
3. `com.unity.modules.unitywebrequest@1.0.0` - 12 edges
4. `com.unity.modules.audio@1.0.0` - 11 edges
5. `VEILWARD Spirit Defense Game Design Document` - 11 edges
6. `com.unity.mathematics@1.2.1` - 10 edges
7. `com.unity.test-framework@1.4.6` - 10 edges
8. `com.unity.modules.uielements@1.0.0` - 10 edges
9. `com.unity.modules.imgui@1.0.0` - 10 edges
10. `com.unity.modules.imageconversion` - 10 edges

## Surprising Connections (you probably didn't know these)
- `P0.1 Non-Gameplay Bootstrap Status` --semantically_similar_to--> `Reproducible iOS Landscape-First Foundation`  [INFERRED] [semantically similar]
  README.md → Docs/superpowers/plans/2026-11-04-p0-1-project-bootstrap.md
- `Non-Negotiable Scope and Quality Stop Rules` --semantically_similar_to--> `Offline-First Privacy and Asset Safety`  [INFERRED] [semantically similar]
  Docs/ROADMAP.md → AGENTS.md
- `Project Contract EditMode Smoke Tests` --semantically_similar_to--> `ProjectBootstrapTests`  [INFERRED] [semantically similar]
  README.md → Docs/superpowers/plans/2026-11-04-p0-1-project-bootstrap.md
- `Reproducible Editor and Headless Test Execution` --semantically_similar_to--> `Local and CI EditMode Test Runner`  [INFERRED] [semantically similar]
  README.md → Docs/superpowers/plans/2026-11-04-p0-1-project-bootstrap.md
- `VEILWARD Agent Rules` --references--> `Unity Project Version`  [EXTRACTED]
  AGENTS.md → ProjectSettings/ProjectVersion.txt

## Import Cycles
- None detected.

## Hyperedges (group relationships)
- **Elemental Combat System** — docs_gdd_four_elements, docs_gdd_hybrid_towers, docs_gdd_elemental_reactions [EXTRACTED 1.00]
- **Layered Battle Architecture** — docs_tech_design_layered_architecture, docs_tech_design_battle_simulation, docs_tech_design_battle_state, docs_tech_design_battle_command, docs_tech_design_config_validation [EXTRACTED 1.00]
- **Bootstrap Contract and Verification Flow** — docs_superpowers_plans_2026_11_04_p0_1_project_bootstrap_bootstraploader, docs_superpowers_plans_2026_11_04_p0_1_project_bootstrap_project_bootstrap_tests, docs_superpowers_plans_2026_11_04_p0_1_project_bootstrap_scene_launch_sequence, docs_superpowers_plans_2026_11_04_p0_1_project_bootstrap_ios_project_settings, docs_superpowers_plans_2026_11_04_p0_1_project_bootstrap_test_runner [EXTRACTED 1.00]

## Communities (73 total, 35 thin omitted)

### Community 0 - "Built-in Utility Modules"
Cohesion: 0.05
Nodes (44): dependencies, depth, source, version, dependencies, depth, source, version (+36 more)

### Community 1 - "Game Design Architecture"
Cohesion: 0.10
Nodes (7): VEILWARD Agent Rules, VEILWARD Concept Directions and Art Brief, VEILWARD Spirit Defense Game Design Document, VEILWARD: Spirit Defense, VEILWARD Development Roadmap, BattleState, VEILWARD Technical Design and Architecture

### Community 2 - "Development Package Dependencies"
Cohesion: 0.06
Nodes (40): dependencies, depth, source, url, version, dependencies, depth, source (+32 more)

### Community 3 - "UI Testing Packages"
Cohesion: 0.07
Nodes (30): dependencies, depth, source, version, dependencies, depth, source, version (+22 more)

### Community 4 - "Bootstrap Code Tests"
Cohesion: 0.11
Nodes (4): BootstrapLoader, ProjectBootstrapTests, Veilward.Presentation, Veilward.Tests.EditMode

### Community 5 - "Serialization Pipeline Packages"
Cohesion: 0.10
Nodes (20): dependencies, depth, source, url, version, dependencies, depth, source (+12 more)

### Community 6 - "UI Media Modules"
Cohesion: 0.11
Nodes (18): dependencies, depth, source, version, dependencies, depth, source, version (+10 more)

### Community 7 - "Imaging Web Modules"
Cohesion: 0.12
Nodes (17): dependencies, depth, source, version, dependencies, depth, source, version (+9 more)

### Community 8 - "Input UI Packages"
Cohesion: 0.12
Nodes (16): dependencies, depth, source, url, version, depth, source, version (+8 more)

### Community 9 - "Bootstrap Documentation Verification"
Cohesion: 0.26
Nodes (7): BootstrapLoader, P0.1 Project Bootstrap Implementation Plan, ProjectBootstrapTests, Local and CI EditMode Test Runner, Unity Project Version, Unity 6000.3.28f1 (8f082d2fe476), VEILWARD Project README

### Community 10 - "Core Manifest Modules"
Cohesion: 0.15
Nodes (12): com.unity.modules.ai@1.0.0, com.unity.modules.animation@1.0.0, com.unity.modules.imgui@1.0.0, com.unity.modules.ui@1.0.0, com.unity.modules.vectorgraphics@1.0.0, com.unity.modules.vr@1.0.0, com.unity.modules.ai, com.unity.modules.animation (+4 more)

### Community 11 - "Feature Manifest Dependencies"
Cohesion: 0.15
Nodes (13): dependencies, com.unity.ai.navigation@2.0.16, com.unity.collab-proxy@2.14.0, com.unity.modules.terrain@1.0.0, com.unity.modules.uielements@1.0.0, com.unity.modules.umbra@1.0.0, com.unity.modules.unitywebrequest@1.0.0, com.unity.modules.terrain (+5 more)

### Community 12 - "Navigation AI Packages"
Cohesion: 0.17
Nodes (12): dependencies, depth, source, url, version, dependencies, depth, source (+4 more)

### Community 13 - "Physics Tilemap Modules"
Cohesion: 0.18
Nodes (11): dependencies, depth, source, version, dependencies, depth, source, version (+3 more)

### Community 14 - "Terrain Physics Modules"
Cohesion: 0.18
Nodes (11): dependencies, depth, source, version, dependencies, depth, source, version (+3 more)

### Community 15 - "Searcher Package Metadata"
Cohesion: 0.29
Nodes (7): dependencies, depth, source, url, version, com.unity.searcher, com.unity.searcher

### Community 16 - "Collaboration Proxy Package"
Cohesion: 0.33
Nodes (6): dependencies, depth, source, url, version, com.unity.collab-proxy

### Community 17 - "Rider IDE Package"
Cohesion: 0.33
Nodes (6): dependencies, depth, source, url, version, com.unity.ide.rider

### Community 18 - "Animation Module Metadata"
Cohesion: 0.33
Nodes (6): dependencies, depth, source, version, com.unity.modules.animation@1.0.0, com.unity.modules.animation

### Community 19 - "Asset Bundle Module"
Cohesion: 0.33
Nodes (6): dependencies, depth, source, version, com.unity.modules.assetbundle, com.unity.modules.assetbundle

### Community 20 - "Director Module Metadata"
Cohesion: 0.33
Nodes (6): dependencies, depth, source, version, com.unity.modules.director@1.0.0, com.unity.modules.director

### Community 21 - "Particle System Module"
Cohesion: 0.33
Nodes (6): dependencies, depth, source, version, com.unity.modules.particlesystem@1.0.0, com.unity.modules.particlesystem

### Community 22 - "Web Request Dependencies"
Cohesion: 0.33
Nodes (6): dependencies, dependencies, depth, source, version, com.unity.modules.unitywebrequest@1.0.0

### Community 23 - "Asset Bundle Web"
Cohesion: 0.33
Nodes (6): dependencies, depth, source, version, com.unity.modules.unitywebrequestassetbundle, com.unity.modules.unitywebrequestassetbundle

### Community 24 - "URP Config Package"
Cohesion: 0.33
Nodes (6): dependencies, depth, source, version, com.unity.render-pipelines.universal-config@17.0.3, com.unity.render-pipelines.universal-config

### Community 25 - "Shader Graph Package"
Cohesion: 0.33
Nodes (6): dependencies, depth, source, version, com.unity.shadergraph@17.3.0, com.unity.shadergraph

### Community 26 - "Timeline Package Metadata"
Cohesion: 0.33
Nodes (6): dependencies, depth, source, url, version, com.unity.timeline

### Community 27 - "Accessibility Module Metadata"
Cohesion: 0.40
Nodes (5): dependencies, depth, source, version, com.unity.modules.accessibility

### Community 28 - "Android JNI Module"
Cohesion: 0.40
Nodes (5): dependencies, depth, source, version, com.unity.modules.androidjni

### Community 29 - "Audio Module Metadata"
Cohesion: 0.40
Nodes (5): dependencies, depth, source, version, com.unity.modules.audio@1.0.0

### Community 30 - "Umbra Module Metadata"
Cohesion: 0.40
Nodes (5): dependencies, depth, source, version, com.unity.modules.umbra

### Community 31 - "Analytics Module Metadata"
Cohesion: 0.40
Nodes (5): depth, source, version, dependencies, com.unity.modules.unityanalytics

### Community 32 - "Web Audio Module"
Cohesion: 0.40
Nodes (5): dependencies, depth, source, version, com.unity.modules.unitywebrequestaudio

### Community 33 - "Legacy Web Module"
Cohesion: 0.40
Nodes (5): dependencies, depth, source, version, com.unity.modules.unitywebrequestwww

### Community 34 - "Wind Module Metadata"
Cohesion: 0.40
Nodes (5): dependencies, depth, source, version, com.unity.modules.wind

### Community 35 - "Render Core Package"
Cohesion: 0.40
Nodes (5): depth, source, version, com.unity.render-pipelines.core, com.unity.render-pipelines.core

### Community 36 - "URP Package Metadata"
Cohesion: 0.40
Nodes (5): dependencies, depth, source, version, com.unity.render-pipelines.universal

### Community 37 - "Package Lock References"
Cohesion: 0.50
Nodes (3): com.unity.modules.audio, com.unity.modules.unitywebrequest, com.unity.modules.unitywebrequestaudio

## Knowledge Gaps
- **35 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `dependencies` connect `Analytics Module Metadata` to `Built-in Utility Modules`, `Development Package Dependencies`, `UI Testing Packages`, `Serialization Pipeline Packages`, `UI Media Modules`, `Imaging Web Modules`, `Input UI Packages`, `Navigation AI Packages`, `Physics Tilemap Modules`, `Terrain Physics Modules`, `Searcher Package Metadata`, `Collaboration Proxy Package`, `Rider IDE Package`, `Animation Module Metadata`, `Asset Bundle Module`, `Director Module Metadata`, `Particle System Module`, `Web Request Dependencies`, `Asset Bundle Web`, `URP Config Package`, `Shader Graph Package`, `Timeline Package Metadata`, `Accessibility Module Metadata`, `Android JNI Module`, `Audio Module Metadata`, `Umbra Module Metadata`, `Web Audio Module`, `Legacy Web Module`, `Wind Module Metadata`, `Render Core Package`, `URP Package Metadata`, `Package Lock References`?**
  _High betweenness centrality (0.394) - this node is a cross-community bridge._
- **Should `Built-in Utility Modules` be split into smaller, more focused modules?**
  _Cohesion score 0.048625792811839326 - nodes in this community are weakly interconnected._
- **Why does `com.unity.modules.jsonserialize@1.0.0` connect `Built-in Utility Modules` to `Development Package Dependencies`, `UI Testing Packages`, `Serialization Pipeline Packages`, `UI Media Modules`, `Web Request Dependencies`, `Analytics Module Metadata`?**
  _High betweenness centrality (0.020) - this node is a cross-community bridge._
- **Should `Game Design Architecture` be split into smaller, more focused modules?**
  _Cohesion score 0.09878048780487805 - nodes in this community are weakly interconnected._
- **Why does `dependencies` connect `Feature Manifest Dependencies` to `Core Manifest Modules`, `Rider Manifest Reference`, `Visual Studio Reference`, `Input System Reference`, `Accessibility Manifest Reference`, `Adaptive Performance Reference`, `Android JNI Reference`, `Asset Bundle Reference`, `Audio Manifest Reference`, `Cloth Manifest Reference`, `Director Manifest Reference`, `Image Conversion Reference`, `JSON Serialization Reference`, `Particle System Reference`, `Physics Manifest Reference`, `Physics 2D Reference`, `Screen Capture Reference`, `Terrain Physics Reference`, `Tilemap Manifest Reference`, `Analytics Manifest Reference`, `Asset Bundle Web Reference`, `Web Audio Reference`, `Web Texture Reference`, `Legacy Web Reference`, `Vehicles Manifest Reference`, `Video Manifest Reference`, `Wind Manifest Reference`, `XR Manifest Reference`, `Multiplayer Center Reference`, `Pipeline Manifest Reference`, `URP Manifest Reference`, `Test Framework Reference`, `Timeline Manifest Reference`, `uGUI Manifest Reference`, `Visual Scripting Reference`?**
  _High betweenness centrality (0.018) - this node is a cross-community bridge._
- **Should `Development Package Dependencies` be split into smaller, more focused modules?**
  _Cohesion score 0.05512820512820513 - nodes in this community are weakly interconnected._
- **Should `UI Testing Packages` be split into smaller, more focused modules?**
  _Cohesion score 0.06666666666666667 - nodes in this community are weakly interconnected._