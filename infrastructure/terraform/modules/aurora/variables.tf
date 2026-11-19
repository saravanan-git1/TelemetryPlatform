variable "name" { type = string }
variable "subnet_ids" { type = list(string) }
variable "security_group_id" { type = string }
variable "database_name" { type = string }
variable "database_username" { type = string }
variable "database_password" {
  type      = string
  sensitive = true
}
variable "instance_class" { type = string }
variable "availability_zones" { type = list(string) }
variable "tags" { type = map(string) }
