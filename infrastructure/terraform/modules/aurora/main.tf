resource "aws_db_subnet_group" "this" {
  name       = "${var.name}-aurora"
  subnet_ids = var.subnet_ids
  tags       = merge(var.tags, { Name = "${var.name}-aurora" })
}

resource "aws_rds_cluster" "this" {
  cluster_identifier                  = "${var.name}-aurora"
  engine                              = "aurora-postgresql"
  engine_version                      = "16.6"
  database_name                       = var.database_name
  master_username                     = var.database_username
  master_password                     = var.database_password
  db_subnet_group_name                = aws_db_subnet_group.this.name
  vpc_security_group_ids              = [var.security_group_id]
  availability_zones                  = var.availability_zones
  storage_encrypted                   = true
  backup_retention_period             = 14
  preferred_backup_window             = "07:00-09:00"
  preferred_maintenance_window        = "sun:09:00-sun:10:00"
  enabled_cloudwatch_logs_exports     = ["postgresql"]
  deletion_protection                 = true
  copy_tags_to_snapshot               = true
  skip_final_snapshot                 = false
  final_snapshot_identifier           = "${var.name}-final"
  apply_immediately                   = false
  iam_database_authentication_enabled = true
  tags                                = var.tags
}

resource "aws_rds_cluster_instance" "this" {
  count                        = 2
  identifier                   = "${var.name}-aurora-${count.index + 1}"
  cluster_identifier           = aws_rds_cluster.this.id
  instance_class               = var.instance_class
  engine                       = aws_rds_cluster.this.engine
  engine_version               = aws_rds_cluster.this.engine_version
  db_subnet_group_name         = aws_db_subnet_group.this.name
  auto_minor_version_upgrade   = true
  performance_insights_enabled = true
  monitoring_interval          = 60
  monitoring_role_arn          = aws_iam_role.enhanced_monitoring.arn
  tags                         = var.tags
}

resource "aws_iam_role" "enhanced_monitoring" {
  name_prefix = "${var.name}-rds-monitoring-"
  assume_role_policy = jsonencode({
    Version = "2012-10-17"
    Statement = [{
      Effect    = "Allow"
      Principal = { Service = "monitoring.rds.amazonaws.com" }
      Action    = "sts:AssumeRole"
    }]
  })
  tags = var.tags
}

resource "aws_iam_role_policy_attachment" "enhanced_monitoring" {
  role       = aws_iam_role.enhanced_monitoring.name
  policy_arn = "arn:aws:iam::aws:policy/service-role/AmazonRDSEnhancedMonitoringRole"
}
