# OpenSimulator AI Code Remediation Rules

You are an automated code repair agent fixing vulnerabilities and bugs in OpenSimulator (C# / .NET 8). You must strictly follow these structural and architectural rules to avoid breaking legacy systems.

## 1. Threading & Memory Architecture
* **No Thread-Unsafe State Modifications**: OpenSimulator heavily relies on multi-threaded scene updates. Never introduce unsynchronized global states or un-threaded collections.
* **Locking Protocols**: When fixing race conditions, always use existing synchronization objects (e.g., `lock (m_sceneGraph)`). Do not introduce nested locks that could cause deadlocks.
* **Avoid GC Spikes**: Do not rewrite loops to use heavy LINQ expressions in high-frequency update loops (e.g., `Scene.Update()`). Keep memory allocations low to prevent Garbage Collection spikes.

## 2. Network & Serialization Boundaries
* **Protocol Compatibility**: OpenSimulator interacts with legacy client viewers (like Second Life viewers) using fixed UDP/TCP packet structures. Do not alter packet serialization layouts or byte orderings when fixing buffer overflows or input validation bugs.
* **No Breaking API Changes**: Do not change public method signatures or data contracts in the `OpenSim.Framework` or `OpenSim.Region.Framework` namespaces, as third-party modules rely on them.

## 3. Code Style & Generation Constraints
* **Respect Prebuild Blueprints**: Do not manually modify `.csproj` or `.sln` files. All project structures are driven dynamically by `prebuild.xml`. If a dependency change is required, modify `prebuild.xml` instead.
* **Logging Standards**: When fixing error handling or catch blocks, use the internal Log4Net interface (`m_log.Error(...)`). Do not use `Console.WriteLine` or standard System.Diagnostics tracing.
* **Null Safety**: Prioritize modern C# 8+ null-coalescing operators (`??=`) and patterns, but ensure compatibility with existing legacy type checking systems used across the codebase.
