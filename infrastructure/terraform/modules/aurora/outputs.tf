output "endpoint" {
  value      = aws_rds_cluster.this.endpoint
  depends_on = [aws_rds_cluster_instance.this]
}
output "cluster_identifier" { value = aws_rds_cluster.this.cluster_identifier }
