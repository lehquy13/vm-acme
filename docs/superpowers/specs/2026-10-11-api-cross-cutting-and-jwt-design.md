# API pipeline, observability, and JWT design

## Goal

Route application use cases through the latest Apache-2.0 MediatR release, apply validation, structured logging, and metrics consistently, and protect work item endpoints with JWT bearer authentication.

## Design

Application requests and handlers will use MediatR 12.5.0, the latest release before the project adopted commercial licensing. FluentValidation validators and a validation behavior will reject invalid requests before handlers run. A logging behavior will emit structured request duration and failure events through `ILogger`, suitable for ingestion by Kibana/Elastic. A metrics behavior will record request counts and durations in a thread-safe in-process registry; the Prometheus text endpoint will expose these counters for Prometheus/Grafana scraping. Comments and configuration will mark the metrics adapter as a placeholder for the project's internal packages.

JWT bearer authentication will use configured issuer, audience, and signing key. Work item routes require an authenticated principal. Liveness, readiness, version, and metrics endpoints remain anonymous. Development settings will provide a local-only signing key; deployments must supply their own key through configuration/secret management. No user/account issuance endpoint is in scope.

## Scope and acceptance

- Existing work item behavior is dispatched through MediatR and all requests pass through registered behaviors.
- Request validation returns a client error and prevents invalid handler execution.
- Structured logs include request name, elapsed time, and outcome without logging request payloads.
- Prometheus-compatible metrics include request count and duration per request type, suitable for Grafana scraping.
- JWT validation checks signature, issuer, audience, and expiration; work item endpoints reject anonymous calls.
- Health, readiness, version, and metrics remain anonymously accessible.
- Runtime configuration documents required JWT settings and internal observability adapter replacement points.
