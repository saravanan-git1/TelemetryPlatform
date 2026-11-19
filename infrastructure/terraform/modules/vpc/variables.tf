variable "name" { type = string }
variable "vpc_cidr" { type = string }
variable "availability_zones" { type = list(string) }
variable "allowed_ingress_cidrs" { type = list(string) }
variable "tags" { type = map(string) }
