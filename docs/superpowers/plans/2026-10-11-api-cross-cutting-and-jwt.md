# API Cross-Cutting and JWT Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add MediatR dispatch with validation, Kibana-friendly structured logging, Grafana-ready metrics, and JWT protection for work item routes.

**Architecture:** Application owns MediatR requests, handlers, validators, and pipeline behaviors. Web API owns JWT bearer configuration, route authorization, and a Prometheus scrape endpoint backed by a metrics abstraction. The in-process metrics implementation is explicitly replaceable by internal packages.

**Tech Stack:** .NET 10, MediatR 12.5.0 (Apache-2.0), FluentValidation 12.2.0, ASP.NET Core JWT bearer, in-process metrics registry.

**Spec:** `docs/superpowers/specs/2026-10-11-api-cross-cutting-and-jwt-design.md`

## Global Constraints

- Keep persistence and domain behavior unchanged.
- Do not log request payloads or credentials.
- Keep probe and scrape endpoints anonymous.
- Pin MediatR to 12.5.0.
- Mark placeholder observability integrations for replacement with internal packages.

## Review Focus

- Invalid requests stop before handlers; ensure validators execute through MediatR.
- JWT tokens with bad signature, issuer, audience, or expiry are rejected by bearer middleware.
- Metrics remain valid Prometheus text and distinguish request types.
- Avoid exposing secrets through logs or committed production configuration.

### Task 1: MediatR requests and pipeline

**Files:** Application project and dependency injection; add request/handler and behavior files; migrate API endpoint dispatch.

- [x] Add MediatR 12.5.0 and FluentValidation dependency registrations.
- [x] Convert create/list/complete operations into MediatR requests and handlers, preserving domain and repository behavior.
- [x] Add validators and validation, logging, and metrics pipeline behaviors.
- [x] Route endpoints and readiness check through `ISender`.

### Task 2: Metrics exposure

**Files:** API startup and a focused metrics endpoint/service file.

- [x] Back request count and duration with an in-process metrics registry.
- [x] Expose Prometheus-compatible text at `/metrics` and retain uptime measurement.
- [x] Document replacement with project internal metrics packages.

### Task 3: JWT bearer authorization

**Files:** API startup, endpoint mapping, appsettings, deployment configuration/docs.

- [x] Configure JWT bearer validation for issuer, audience, signing key, and lifetime.
- [x] Require authorization for work item endpoint group; mark operational endpoints anonymous.
- [x] Add local development configuration and document deployment secret requirements.

### Task 4: Review and verification

- [x] Inspect changed files and configuration for behavior regressions and secret exposure.
- [x] Build the solution and report the result; do not run tests unless requested.
