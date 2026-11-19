data "aws_availability_zones" "available" {
  state = "available"
}

resource "aws_cloudwatch_log_group" "containers" {
  name              = "/${var.project_name}-${var.environment}/containers"
  retention_in_days = 30
  tags = {
    Project     = var.project_name
    Environment = var.environment
    ManagedBy   = "Terraform"
  }
}

locals {
  name = "${var.project_name}-${var.environment}"
  availability_zones = slice(
    data.aws_availability_zones.available.names,
    0,
    var.availability_zone_count
  )
  tags = {
    Project     = var.project_name
    Environment = var.environment
    ManagedBy   = "Terraform"
  }
}

resource "random_password" "database" {
  length  = 32
  special = false
}

resource "random_password" "redis" {
  length  = 40
  special = false
}

module "vpc" {
  source = "./modules/vpc"

  name                  = local.name
  vpc_cidr              = var.vpc_cidr
  availability_zones    = local.availability_zones
  allowed_ingress_cidrs = var.allowed_ingress_cidrs
  tags                  = local.tags
}

module "aurora" {
  source = "./modules/aurora"

  name               = local.name
  subnet_ids         = module.vpc.private_subnet_ids
  security_group_id  = module.vpc.database_security_group_id
  database_name      = var.database_name
  database_username  = var.database_username
  database_password  = random_password.database.result
  instance_class     = var.database_instance_class
  availability_zones = local.availability_zones
  tags               = local.tags
}

module "redis" {
  source = "./modules/redis"

  name              = local.name
  subnet_ids        = module.vpc.private_subnet_ids
  security_group_id = module.vpc.redis_security_group_id
  auth_token        = random_password.redis.result
  node_type         = var.redis_node_type
  tags              = local.tags
}

module "secrets" {
  source = "./modules/secrets"

  name = "${local.name}/application"
  secret_json = jsonencode({
    postgres_connection_string = "Host=${module.aurora.endpoint};Port=5432;Database=${var.database_name};Username=${var.database_username};Password=${random_password.database.result};SSL Mode=VerifyFull;Root Certificate=/etc/ssl/certs/rds-combined-ca-bundle.pem;Trust Server Certificate=false"
    redis_connection_string    = "${module.redis.primary_endpoint_address}:${module.redis.port},password=${random_password.redis.result},ssl=True,abortConnect=False"
    telemetry_source_url       = var.telemetry_source_url
    telemetry_api_key          = var.telemetry_api_key
    telemetry_machines         = var.telemetry_machines
  })
  tags = local.tags
}

module "iam" {
  source = "./modules/iam"

  name                = local.name
  secret_arn          = module.secrets.secret_arn
  log_group_arn       = aws_cloudwatch_log_group.containers.arn
  ecr_repository_arns = ["arn:aws:ecr:${var.aws_region}:*:repository/*"]
  tags                = local.tags
}

module "alb" {
  source = "./modules/alb"

  name                  = local.name
  vpc_id                = module.vpc.vpc_id
  public_subnet_ids     = module.vpc.public_subnet_ids
  alb_security_group_id = module.vpc.alb_security_group_id
  certificate_arn       = var.certificate_arn
  tags                  = local.tags
}

module "compute" {
  source = "./modules/ec2"

  name                  = local.name
  subnet_ids            = module.vpc.private_subnet_ids
  security_group_id     = module.vpc.application_security_group_id
  instance_profile_name = module.iam.instance_profile_name
  target_group_arns     = [module.alb.target_group_arn]
  instance_type         = var.api_instance_type
  min_size              = var.api_min_size
  max_size              = var.api_max_size
  desired_capacity      = var.api_desired_capacity
  api_image             = var.api_image
  worker_image          = var.worker_image
  secret_arn            = module.secrets.secret_arn
  log_group_name        = aws_cloudwatch_log_group.containers.name
  aws_region            = var.aws_region
  tags                  = local.tags
}

module "cloudwatch" {
  source = "./modules/cloudwatch"

  name                       = local.name
  log_group_name             = aws_cloudwatch_log_group.containers.name
  autoscaling_group_name     = module.compute.autoscaling_group_name
  load_balancer_arn_suffix   = module.alb.load_balancer_arn_suffix
  target_group_arn_suffix    = module.alb.target_group_arn_suffix
  aurora_cluster_identifier  = module.aurora.cluster_identifier
  redis_replication_group_id = module.redis.replication_group_id
  tags                       = local.tags
}
