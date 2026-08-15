# Security Policy

## Reporting a vulnerability

Please use GitHub's [private vulnerability reporting](https://github.com/soundbyter/skillbooks-core/security/advisories/new)
(Security tab → Report a vulnerability) rather than opening a public issue, so any fix can go
out before the details are public.

## Scope

This is a single-player/co-op sandbox game mod, not a service handling untrusted network
input beyond what the base game already exposes between a server and its own connected
clients. Realistic concerns are things like a crafted asset (a trait/recipe JSON from another
installed mod) causing a crash or unexpected behavior on load, not remote code execution.
Report anything that looks worse than "causes a crash" with priority.

## Supported versions

Only the latest released version is supported. Please update before reporting an issue if
you're not already on it.
