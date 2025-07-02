locals {
  config_file_path = coalesce(var.config_file_path, "${path.root}/benchmark_configs/localstorage.json")
}
