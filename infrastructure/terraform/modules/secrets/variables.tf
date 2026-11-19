variable "name" { type = string }
variable "secret_json" {
  type      = string
  sensitive = true
}
variable "tags" { type = map(string) }
