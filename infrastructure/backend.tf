# Leaving this here in case the destroy ever fails in a workflow..
# for run id either look at aws s3 ls s3://armonik-microbench-backend-tfstate/microbench/ OR at workflow
# terraform {
#   backend "s3" {
#     bucket = "armonik-microbench-backend-tfstate"
#     key    = "microbench/<benchmark-run-id>/terraform.tfstate" 
#     profile = "default"
#     region = "us-east-1"
#   }
# }
