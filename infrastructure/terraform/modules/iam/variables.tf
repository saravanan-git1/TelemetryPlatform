variable "name" { type = string }
variable "secret_arn" { type = string }
variable "log_group_arn" { type = string }
variable "ecr_repository_arns" { type = list(string) }
variable "tags" { type = map(string) }
