# Milestone 3 work allocation

Each member owned two services end to end. Every task went through its own PR into `feature/milestone-3-layered-service-development`, and a teammate reviewed it. The board is the [Milestone 3 project](https://github.com/users/CarterBeasse/projects/1) (private to its owner), and the issues are under [milestone 3](https://github.com/JimRashed/Web-3-Final-Project/issues?q=milestone%3A%22Milestone+3+%E2%80%94+Layered+Service+Development%22).

## Service slices (domain → data → application → endpoints → React → slice tests)

| Service | Owner | Feature | Tasks | Main PRs |
|---|---|---|---|---|
| Identity | Carter Beasse | #9 | #26–#31 | #76, #80, #84, #88, #102, #104 |
| Statistics | Carter Beasse | #10 | #32–#37 | #77, #81, #85, #93, #102, #104 |
| QuestionBank | Jim Rashed | #11 | #38–#43 | #91, #92, #98, #103, #106 |
| Lobby | Jim Rashed | #12 | #44–#49 | #89 |
| Gameplay | Suheng Li | #13 | #50–#55 | #75, #86, #95, #96, #100 |
| Notification | Suheng Li | #14 | #56–#61 | #74, #82, #90, #94, #99 |

## Platform and delivery tasks

| Task | Owner | PR |
|---|---|---|
| P01 board, ownership map and delivery rules (#16) | Jim Rashed | — |
| P02 service boundaries, contracts and security policies (#17) | Jim Rashed | #70 |
| P03 six-service scaffold (#18) | Jim Rashed | #72 |
| P04 service-owned databases and migrations (#19) | Carter Beasse (taken over from Jim) | #78 |
| P05 Keycloak OIDC provider and client identities (#20) | Carter Beasse | #71 |
| P06 JWT validation and scope policies in all six APIs (#21) | Carter Beasse | #73 |
| P07 React shell, OIDC session and HTTP clients (#22) | Jim Rashed | #79 |
| P08 layer agents (#23) | Jim Rashed | closed; agents dropped, R-P-I-V and Dispatcher used instead |
| P09 per-service test hosts and bearer-token fixture (#24) | Suheng Li | #83 |
| P10 CI gates (#25) | Suheng Li | #87 |
| D01 documented local run (#62), D02 legacy migration (#63), D05 traceability audit (#66), D07 milestone PR (#68), D08 submission bundle (#69) | Jim Rashed | — |
| D03 two-player journey (#64), D06 review and signoff (#67) | Suheng Li | — |
| D04 this report (#65) | Carter Beasse | this PR |

## Review pairing in practice

Every PR received at least one review from another member before merge. Reviews that ran the branch and added probe tests found the defects listed in the [report](architecture-and-reflection.md#5-agent-assistance-and-human-corrections).
