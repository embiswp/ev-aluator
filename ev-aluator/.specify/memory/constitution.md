<!-- 
Sync Impact Report:
- Version change: 0.0.0 → 1.0.0
- Modified principles: Initial creation with focus on code quality, testing, UX consistency, and performance
- Added sections: All core principles and governance structure
- Removed sections: None
- Templates requiring updates: ✅ All .specify templates validated for consistency
- Follow-up TODOs: None
-->

# EV-aluator Constitution

## Core Principles

### I. Code Quality Standards (NON-NEGOTIABLE)
All code MUST meet strict quality criteria: Type safety enforced at compile time; Linting rules with zero warnings allowed; Code coverage minimum 90% for critical paths; Documentation required for all public APIs; Consistent formatting via automated tools. No exceptions for "quick fixes" or prototypes that enter production.

**Rationale**: Quality debt compounds exponentially and becomes insurmountable in assessment systems where reliability is paramount.

### II. Test-First Development (NON-NEGOTIABLE)
TDD cycle MUST be followed: Tests written first → Implementation → Refactor. Unit tests for all business logic; Integration tests for data persistence and API contracts; End-to-end tests for critical user journeys; Performance tests for response time requirements. All tests MUST pass before merge.

**Rationale**: EV assessment accuracy depends on validated logic; untested code in production creates liability and user trust issues.

### III. User Experience Consistency
UI components MUST follow established design system patterns; Responsive design required for all screen sizes; Loading states and error messages standardized; Accessibility WCAG 2.1 AA compliance mandatory; User feedback incorporated through usability testing cycles.

**Rationale**: Assessment tools require user confidence; inconsistent interfaces undermine credibility and adoption.

### IV. Performance Requirements
API response times MUST be under 200ms for data queries; Page load times under 2 seconds on 3G connections; Database queries optimized with proper indexing; Client-side rendering optimized for 60fps; Caching strategies implemented for static and computed data.

**Rationale**: Assessment workflows are time-sensitive; performance delays impact user productivity and system adoption.

## Security Standards

Security-by-design MUST be implemented: Input validation and sanitization at all boundaries; Authentication and authorization for all endpoints; Encryption for data in transit and at rest; Regular security audits and dependency updates; No secrets in source code or logs.

Data privacy compliance with relevant regulations; Audit trails for all data modifications; Secure session management; Rate limiting and DDoS protection; Vulnerability scanning in CI/CD pipeline.

## Development Workflow

Code review MUST be completed before merge: Minimum two reviewer approval required; Automated testing pipeline MUST pass; Security scan MUST complete without high-severity findings; Documentation updated for breaking changes; Performance impact assessed for critical paths.

Feature flags used for gradual rollouts; Database migrations tested on staging environments; Rollback procedures documented and tested; Monitoring alerts configured for new features; Post-deployment validation checklists followed.

## Governance

This constitution supersedes all other development practices and guidelines. Amendments require: Technical lead approval; Impact assessment on existing codebase; Migration plan for affected components; Team consensus through documented discussion; Version control with semantic versioning.

All pull requests MUST verify compliance with these principles. Complexity additions MUST be justified with clear business value. Constitution violations are blocking issues that prevent deployment. Use `.claude/commands/*.md` for runtime development guidance that implements these principles.

**Version**: 1.0.0 | **Ratified**: 2025-09-25 | **Last Amended**: 2025-09-25