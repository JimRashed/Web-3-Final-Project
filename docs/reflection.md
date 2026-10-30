# Reflection Milestone 2

## Where the flow helped
- Starting with the issues then following the plan kept the scope of our project in the right direction.
- Putting the R-PIV in github copilot instructions makes every session have the same rules keeping us from going off track.
- The review found its own findings and to fix them it ran its own R-PIV cycle with its own plan.

## Where it needed correction
- When not enough context is given it takes a few rounds to get the copilot to do the exact task we need.
- The initial round of tests wasn't entirely idempotent; the context wasn't being properly reset in between tests. Thankfully, the code review agent caught this problem. This is why a second loop was necessary. A definite flaw would therefore be the AI needed time to fix its own hallucinations on occasions
