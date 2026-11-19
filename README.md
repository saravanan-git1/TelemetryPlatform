# Telemetry Platform

A .NET 10 Clean Architecture starter for polling machine telemetry from configurable REST/JSON endpoints, persisting readings to PostgreSQL, coordinating worker instances with Redis, and broadcasting updates over SignalR.

## Solution

- `TelemetryPlatform.Domain`: domain entities and value objects.
- `TelemetryPlatform.Application`: use cases and persistence/source contracts.
- `TelemetryPlatform.Infrastructure`: Dapper/PostgreSQL and Redis adapters, HTTP source, and retry policies.
- `TelemetryPlatform.Api`: ASP.NET Core API, SignalR, and health endpoints.
- `TelemetryPlatform.Worker`: hosted polling service.
- `tests/`: unit and integration test projects.

## Local development

Requirements: .NET 10 SDK and Docker Compose.

1. Copy `.env.example` to `.env`; configure `TELEMETRY_SOURCE_URL`, optional `TELEMETRY_API_KEY`, and at least one `Telemetry__Machines__<machine-id>=<relative-path>` entry.
2. Start dependencies and applications with `docker compose up --build`.
3. API: `http://localhost:8080`; latest reading endpoints: `/api/telemetry/{machineId}/latest` and `/api/telemetry/latest`; health endpoints: `/health/live` and `/health/ready`; SignalR hub: `/hubs/telemetry`.
4. Run checks with `dotnet test TelemetryPlatform.sln`. The PostgreSQL integration test is explicitly skipped unless `ConnectionStrings__Postgres` is set.

The REST source integration is configurable because machine vendors expose different paths and JSON payloads. Set `TELEMETRY_SOURCE_URL` and `TELEMETRY_API_KEY` in `.env`, then add one .NET configuration entry per machine, for example `Telemetry__Machines__machine-001=/api/machines/001/telemetry`. The service requests each configured relative path and stores the returned JSON document. Do not commit credentials; use environment variables locally and AWS Secrets Manager in AWS.

## Database

`database/001_initial_schema.sql` is applied automatically by the local Compose PostgreSQL service. Apply it through your normal migration process when provisioning Aurora PostgreSQL. The schema stores raw JSON payloads with a unique `(machine_id, observed_at)` key and tracks each machine's last poll.

## AWS infrastructure

Terraform modules are under `infrastructure/terraform/modules`. The root stack composes a VPC, ALB, EC2 Auto Scaling Group, Aurora PostgreSQL, ElastiCache Redis, IAM, CloudWatch, and Secrets Manager. Review defaults and access CIDRs, provide deployment-specific container image URIs and secrets, and run `terraform plan` before applying. Never commit `terraform.tfvars` or state files.

The AWS deployment uses EC2 Auto Scaling as requested. Provision an encrypted, versioned S3 state bucket and an ACM certificate first. Set `api_image`, `worker_image`, `certificate_arn`, and trusted `allowed_ingress_cidrs` in a protected Terraform variables file. Store state remotely and restrict the plan/apply role and registry permissions. Terraform state contains generated database and Redis credentials; protect it as sensitive data.

`azure-pipelines.yml` runs CI on `main` and pull requests. The manually triggered `azure-pipelines-production.yml` builds/tests, publishes API and worker images to pre-created ECR repositories, and applies Terraform. It requires the Azure DevOps AWS Toolkit and Terraform extensions, an AWS service connection with narrowly scoped deployment permissions, and a protected variable group named `telemetry-production` containing `awsServiceConnection`, `awsRegion`, `ecrRegistry`, `apiEcrRepository`, `workerEcrRepository`, `certificateArn`, `allowedIngressCidrs` (comma-separated), `terraformStateBucket`, `terraformStateKey`, `telemetrySourceUrl`, `telemetryMachinesJson` (JSON map, or `{}`), and `telemetryApiKey` (secret; use an empty value if not required). Add an Azure DevOps environment approval/check before production use.

Linux service unit and install guidance are under `deploy/systemd`.

## Configuration and operations

The API and worker use standard .NET configuration providers. Connection strings and secrets must be injected through the hosting environment. OpenTelemetry traces/metrics are instrumented in both processes and exported to console, which the EC2 CloudWatch agent collects with container logs. Configure a production collector/exporter, sampling, retention, alert actions, TLS, backups, network allowlists, and an authentication/authorization layer for the API and SignalR hub before rollout; the API routes and hub subscriptions in this baseline are not authenticated.
