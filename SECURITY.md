# Security policy

## Supported versions

Only the latest release (the `v*` tag deployed to production) receives fixes.

## Reporting a vulnerability

Please **do not open a public issue**. Use GitHub's private reporting instead:
**Security → Report a vulnerability** on this repository. Include what you found, how to reproduce
it and what an attacker could do with it.

You can expect an acknowledgement within a few days and a fix or a decision within two weeks for
anything confirmed. Reports about the multi-tenant isolation (one group reading or changing another
group's data), authentication, or the anonymous public standings link are the most valuable.

## Scope notes

- The development admin (`admin@palpitao.local`) and its password are for local development only;
  real environments must not use them.
- Secrets never live in the repository: configuration files carry placeholders, and real values come
  from environment variables, user-secrets or GitHub environment secrets
  (see [docs/operations.md](docs/operations.md#secrets-and-security-hardening)).
- Dependencies are watched by Dependabot and the code by CodeQL.
