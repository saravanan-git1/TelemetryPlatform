variable "name" { type = string }
variable "vpc_id" { type = string }
variable "public_subnet_ids" { type = list(string) }
variable "alb_security_group_id" { type = string }
variable "certificate_arn" {
  type        = string
  description = "Optional ACM certificate ARN for the HTTPS listener."
  default     = ""
}
variable "tags" { type = map(string) }
