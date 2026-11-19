variable "name" { type = string }
variable "log_group_name" { type = string }
variable "autoscaling_group_name" { type = string }
variable "load_balancer_arn_suffix" { type = string }
variable "target_group_arn_suffix" { type = string }
variable "aurora_cluster_identifier" { type = string }
variable "redis_replication_group_id" { type = string }
variable "tags" { type = map(string) }
