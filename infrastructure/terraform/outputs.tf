output "api_url" {
  description = "Public HTTPS API endpoint."
  value       = "https://${module.alb.dns_name}"
}

output "aurora_endpoint" {
  description = "Aurora PostgreSQL writer endpoint."
  value       = module.aurora.endpoint
}

output "redis_endpoint" {
  description = "ElastiCache Redis primary endpoint."
  value       = module.redis.primary_endpoint_address
}

output "application_secret_arn" {
  description = "Secrets Manager secret ARN injected on EC2 instances."
  value       = module.secrets.secret_arn
}

output "autoscaling_group_name" {
  description = "EC2 Auto Scaling Group name."
  value       = module.compute.autoscaling_group_name
}
