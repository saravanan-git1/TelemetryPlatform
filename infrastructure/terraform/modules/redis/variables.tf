variable "name" { type = string }
variable "subnet_ids" { type = list(string) }
variable "security_group_id" { type = string }
variable "auth_token" {
  type      = string
  sensitive = true
}
variable "node_type" { type = string }
variable "tags" { type = map(string) }
