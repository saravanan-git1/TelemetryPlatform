variable "aws_region" {
  type        = string
  description = "AWS region to deploy into."
  default     = "us-east-1"
}

variable "project_name" {
  type        = string
  description = "Short lowercase name used to tag and name resources."
  default     = "telemetry"
}

variable "environment" {
  type        = string
  description = "Deployment environment name."
  default     = "production"
}

variable "availability_zone_count" {
  type        = number
  description = "Number of availability zones; production should use at least two."
  default     = 2
  validation {
    condition     = var.availability_zone_count >= 2 && var.availability_zone_count <= 3
    error_message = "Use two or three availability zones."
  }
}

variable "vpc_cidr" {
  type        = string
  description = "CIDR range for the new VPC; avoid overlaps with connected networks."
  default     = "10.20.0.0/16"
}

variable "allowed_ingress_cidrs" {
  type        = list(string)
  description = "Trusted client CIDRs permitted to reach the ALB. Restrict this in production."
}

variable "certificate_arn" {
  type        = string
  description = "ACM certificate ARN for the HTTPS listener."
  validation {
    condition     = length(var.certificate_arn) > 0
    error_message = "A valid ACM certificate ARN is required for HTTPS."
  }
}

variable "api_image" {
  type        = string
  description = "Fully qualified API container image URI in Amazon ECR."
}

variable "worker_image" {
  type        = string
  description = "Fully qualified worker container image URI in Amazon ECR."
}

variable "telemetry_source_url" {
  type        = string
  description = "Base URL of the machine telemetry REST API."
  default     = ""
}

variable "telemetry_api_key" {
  type        = string
  description = "Optional API key for the telemetry source."
  sensitive   = true
  default     = ""
}

variable "telemetry_machines" {
  type        = map(string)
  description = "Machine identifier to REST source path mapping."
  default     = {}
}

variable "database_name" {
  type        = string
  description = "Aurora database name."
  default     = "telemetry"
}

variable "database_username" {
  type        = string
  description = "Aurora application username."
  default     = "telemetry_app"
}

variable "database_instance_class" {
  type        = string
  description = "Aurora PostgreSQL instance class."
  default     = "db.r7g.large"
}

variable "api_instance_type" {
  type        = string
  description = "EC2 instance type for the API/worker Auto Scaling Group."
  default     = "t3.medium"
}

variable "api_min_size" {
  type        = number
  description = "Minimum number of API instances."
  default     = 2
}

variable "api_max_size" {
  type        = number
  description = "Maximum number of API instances."
  default     = 6
}

variable "api_desired_capacity" {
  type        = number
  description = "Initial desired API instance count."
  default     = 2
  validation {
    condition     = var.api_min_size <= var.api_desired_capacity && var.api_desired_capacity <= var.api_max_size
    error_message = "api_desired_capacity must be between api_min_size and api_max_size."
  }
}

variable "redis_node_type" {
  type        = string
  description = "ElastiCache Redis node type."
  default     = "cache.t4g.small"
}
