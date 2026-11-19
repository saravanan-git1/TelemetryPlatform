resource "aws_secretsmanager_secret" "application" {
  name                    = var.name
  description             = "Telemetry Platform runtime configuration."
  recovery_window_in_days = 30
  tags                    = var.tags
}

resource "aws_secretsmanager_secret_version" "application" {
  secret_id     = aws_secretsmanager_secret.application.id
  secret_string = var.secret_json
}
