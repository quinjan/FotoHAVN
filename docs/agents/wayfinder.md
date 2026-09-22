# Wayfinder issue operations

Load this file only for a Wayfinder map, child ticket, dependency, claim, frontier, or resolution task.

## Model

- A map is one issue labelled `wayfinder:map` containing Notes, Decisions-so-far, and Fog.
- A child is a GitHub sub-issue labelled `wayfinder:research`, `wayfinder:prototype`, `wayfinder:grilling`, or `wayfinder:task`.
- A blocker is a native GitHub issue dependency. Use a `Blocked by: #<n>` line only when native dependencies are unavailable.
- The frontier is the first map-ordered open child with no open blocker and no assignee.

## Operations

- Create a map with `gh issue create --label wayfinder:map`.
- Link a child through GitHub's sub-issues API; fall back to a map task list plus `Part of #<map>` at the top of the child.
- Add a dependency through `repos/<owner>/<repo>/issues/<child>/dependencies/blocked_by` using the blocker's numeric database `id`, not its issue number or node ID.
- Claim a frontier ticket with `gh issue edit <number> --add-assignee @me` as the first write.
- Resolve by commenting with the answer, closing the child, and adding a concise result pointer to the map's Decisions-so-far.

Completion requires the map and child links, dependency state, assignee, and resulting open or closed state to be verified.
